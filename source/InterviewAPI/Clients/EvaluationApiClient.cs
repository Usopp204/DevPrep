using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewAPI.DTOs;
using InterviewAPI.DTOs.Evaluation;

namespace InterviewAPI.Clients;

public class EvaluationApiClient : IEvaluationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EvaluationApiClient> _logger;

    public EvaluationApiClient(HttpClient httpClient, ILogger<EvaluationApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<EvaluateAnswerResult> EvaluateAnswerAsync(
        Guid sessionId, Guid sessionQuestionId, EvaluateAnswerPayload payload,
        string? bearerToken, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"api/evaluation/sessions/{sessionId}/answers/{sessionQuestionId}");
            request.Content = JsonContent.Create(payload);
            if (!string.IsNullOrEmpty(bearerToken))
                request.Headers.TryAddWithoutValidation("Authorization", bearerToken);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw CreateStatusFailure(sessionId, sessionQuestionId, response.StatusCode);

            ApiResponse<EvaluateAnswerResult>? envelope;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<ApiResponse<EvaluateAnswerResult>>(ct);
            }
            catch (JsonException ex)
            {
                throw CreateDependencyResponseFailure(sessionId, sessionQuestionId, response.StatusCode, ex);
            }
            catch (NotSupportedException ex)
            {
                throw CreateDependencyResponseFailure(sessionId, sessionQuestionId, response.StatusCode, ex);
            }

            if (envelope?.Success != true
                || envelope.Data is null
                || envelope.Data.EvaluationId == Guid.Empty
                || envelope.Data.SessionId != sessionId
                || envelope.Data.SessionQuestionId != sessionQuestionId)
            {
                throw CreateDependencyResponseFailure(sessionId, sessionQuestionId, response.StatusCode);
            }

            return envelope.Data;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (EvaluationApiCallException)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw LogFailure(
                sessionId,
                sessionQuestionId,
                new EvaluationApiCallException(
                    EvaluationApiFailureCategory.Timeout,
                    isRetryable: true,
                    innerException: ex));
        }
        catch (TimeoutException ex)
        {
            throw LogFailure(
                sessionId,
                sessionQuestionId,
                new EvaluationApiCallException(
                    EvaluationApiFailureCategory.Timeout,
                    isRetryable: true,
                    innerException: ex));
        }
        catch (HttpRequestException ex)
        {
            throw LogFailure(
                sessionId,
                sessionQuestionId,
                new EvaluationApiCallException(
                    EvaluationApiFailureCategory.Transport,
                    isRetryable: true,
                    ex.StatusCode,
                    ex));
        }
        catch (Exception ex)
        {
            throw LogFailure(
                sessionId,
                sessionQuestionId,
                new EvaluationApiCallException(
                    EvaluationApiFailureCategory.InvalidDependencyResponse,
                    isRetryable: true,
                    innerException: ex));
        }
    }

    private EvaluationApiCallException CreateStatusFailure(
        Guid sessionId,
        Guid sessionQuestionId,
        HttpStatusCode statusCode)
    {
        var numericStatus = (int)statusCode;
        var failure = statusCode switch
        {
            HttpStatusCode.RequestTimeout => new EvaluationApiCallException(
                EvaluationApiFailureCategory.Timeout, true, statusCode),
            (HttpStatusCode)429 => new EvaluationApiCallException(
                EvaluationApiFailureCategory.RateLimited, true, statusCode),
            HttpStatusCode.BadRequest => new EvaluationApiCallException(
                EvaluationApiFailureCategory.InvalidRequest, false, statusCode),
            HttpStatusCode.Unauthorized => new EvaluationApiCallException(
                EvaluationApiFailureCategory.Authentication, false, statusCode),
            HttpStatusCode.Forbidden => new EvaluationApiCallException(
                EvaluationApiFailureCategory.Ownership, false, statusCode),
            HttpStatusCode.NotFound => new EvaluationApiCallException(
                EvaluationApiFailureCategory.NotFound, false, statusCode),
            _ when numericStatus >= 500 => new EvaluationApiCallException(
                EvaluationApiFailureCategory.DependencyUnavailable, true, statusCode),
            _ when numericStatus >= 400 => new EvaluationApiCallException(
                EvaluationApiFailureCategory.InvalidRequest, false, statusCode),
            _ => new EvaluationApiCallException(
                EvaluationApiFailureCategory.InvalidDependencyResponse, true, statusCode)
        };

        return LogFailure(sessionId, sessionQuestionId, failure);
    }

    private EvaluationApiCallException CreateDependencyResponseFailure(
        Guid sessionId,
        Guid sessionQuestionId,
        HttpStatusCode statusCode,
        Exception? innerException = null) =>
        LogFailure(
            sessionId,
            sessionQuestionId,
            new EvaluationApiCallException(
                EvaluationApiFailureCategory.InvalidDependencyResponse,
                isRetryable: true,
                statusCode,
                innerException));

    private EvaluationApiCallException LogFailure(
        Guid sessionId,
        Guid sessionQuestionId,
        EvaluationApiCallException failure)
    {
        _logger.LogWarning(
            "Evaluation dependency call failed for session {SessionId}, question {SessionQuestionId}. " +
            "Category {FailureCategory}; Retryable {Retryable}; StatusCode {StatusCode}.",
            sessionId,
            sessionQuestionId,
            failure.Category,
            failure.IsRetryable,
            failure.StatusCode.HasValue ? (int)failure.StatusCode.Value : null);
        return failure;
    }
}