using System.Net.Http.Json;
using System.Text.Json;

namespace InterviewAPI.Clients;

public class PaymentApiClient : IPaymentApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymentApiClient> _logger;

    public PaymentApiClient(HttpClient httpClient, ILogger<PaymentApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CreditDeductionResponse> DeductCreditAsync(
        Guid userId, int amount, Guid sessionId, string description, CancellationToken ct)
    {
        try
        {
            var request = new
            {
                UserId = userId,
                Amount = amount,
                ReferenceId = sessionId,
                Description = description
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/internal/credit/deduct", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "PaymentAPI deduct credit failed: status={StatusCode}, userId={UserId}, amount={Amount}",
                    response.StatusCode, userId, amount);
                return new CreditDeductionResponse
                {
                    Success = false,
                    Message = $"PaymentAPI error: {response.StatusCode}"
                };
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var result = await JsonSerializer.DeserializeAsync<CreditDeductionResponse>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct)
                ?? new CreditDeductionResponse { Success = false, Message = "Empty response" };

            _logger.LogInformation(
                "PaymentAPI deduct credit success: userId={UserId}, amount={Amount}, balanceAfter={Balance}",
                userId, amount, result.BalanceAfter);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PaymentAPI deduct credit exception: userId={UserId}, amount={Amount}", userId, amount);
            return new CreditDeductionResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<CreditDeductionResponse> RefundCreditAsync(
        Guid userId, int amount, Guid sessionId, string description, CancellationToken ct)
    {
        try
        {
            var request = new
            {
                UserId = userId,
                Amount = amount,
                ReferenceId = sessionId,
                Description = description
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/internal/credit/refund", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "PaymentAPI refund credit failed: status={StatusCode}, userId={UserId}, amount={Amount}",
                    response.StatusCode, userId, amount);
                return new CreditDeductionResponse
                {
                    Success = false,
                    Message = $"PaymentAPI error: {response.StatusCode}"
                };
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var result = await JsonSerializer.DeserializeAsync<CreditDeductionResponse>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct)
                ?? new CreditDeductionResponse { Success = false, Message = "Empty response" };

            _logger.LogInformation(
                "PaymentAPI refund credit success: userId={UserId}, amount={Amount}, balanceAfter={Balance}",
                userId, amount, result.BalanceAfter);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PaymentAPI refund credit exception: userId={UserId}, amount={Amount}", userId, amount);
            return new CreditDeductionResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<int> GetBalanceAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/credits/balance?userId={userId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetBalance failed: status={StatusCode}, userId={UserId}",
                    response.StatusCode, userId);
                return 0;
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var jsonDoc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return jsonDoc.RootElement.GetProperty("Balance").GetInt32();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetBalance exception: userId={UserId}", userId);
            return 0;
        }
    }
}
