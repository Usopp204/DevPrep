using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models.Evaluation;
using WebClient.Models.Interview;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Interview;

public interface IInterviewService
{
    Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken);

    Task<StartInterviewResult> StartInterviewAsync(
        int durationMinutes, string level, string position, string? jd, Guid? jdId, List<string> focusAreas,
        CancellationToken cancellationToken);

    Task<(bool Success, CatalogResponse? Catalog, string? Error)> GetCatalogAsync(CancellationToken cancellationToken);

    Task<(bool Success, RoomViewModel? Room, string? Error)> GetRoomAsync(Guid sessionId, CancellationToken cancellationToken);

    // Materializes the current/first question (SessionQuestion + AI chat message) for the Room.
    // Safe to call on every Room entry (server-side idempotent — see GenerateQuestionAsync).
    // Must only be called after the biometric entry gate has passed.
    Task<(bool Success, string? Error)> GenerateQuestionAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<(bool Success, MessageResponse? Message, string? Error)> SendMessageAsync(
        Guid sessionId, SendMessageRequest request, CancellationToken cancellationToken);

    Task<(bool Success, EndResponse? Completion, string? Error)> CompleteSessionAsync(
        Guid sessionId, CancellationToken cancellationToken);

    Task<(bool Success, EndResponse? Cancellation, string? Error)> EndSessionAsync(
        Guid sessionId, CancellationToken cancellationToken);

    Task<(bool Success, PauseResponse? Pause, string? Error)> PauseSessionAsync(
        Guid sessionId, CancellationToken cancellationToken);

    Task<(bool Success, UnpauseResponse? Unpause, string? Error)> UnpauseSessionAsync(
        Guid sessionId, CancellationToken cancellationToken);

    // Reports one idempotent camera incident after the reconnect grace period.
    Task<(bool Success, CameraViolationResultModel? Result, string? Error)> ReportCameraViolationAsync(
        Guid sessionId, Guid incidentId, string reason, CancellationToken cancellationToken);

    Task<(bool Success, string? Error)> MarkRoomTourSeenAsync(CancellationToken cancellationToken);

    Task<(bool Success, byte[]? Audio, string? Error)> GetTtsAudioAsync(string text, CancellationToken cancellationToken);

    Task<(bool Success, SttResponse? Result, string? Error)> TranscribeAudioAsync(
        Guid sessionId, Guid checkId, byte[] audio, string fileName, string contentType,
        CancellationToken cancellationToken);

    Task<(bool Success, string? Hint, string? Error)> GetHintAsync(string question, CancellationToken cancellationToken);

    Task<HistoryViewModel> GetHistoryAsync(
        int page, int pageSize, string? status, string? position, string? keyword, CancellationToken cancellationToken);

    Task<ApiCallResult<SessionHistoryResult>> GetRecentSessionsAsync(
        int page, int pageSize, CancellationToken cancellationToken);

    Task<SessionDetailViewModel> GetSessionDetailAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<(bool Success, string? Error, int StatusCode)> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<ApiCallResult<FeedbackEligibilityResponse>> GetFeedbackEligibilityAsync(CancellationToken cancellationToken);

    Task<ApiCallResult<SessionFeedbackEligibilityResponse>> GetSessionFeedbackEligibilityAsync(
        Guid sessionId, CancellationToken cancellationToken);

    Task<ApiCallResult<IntegrityPolicyStatusResponse>> GetIntegrityPolicyStatusAsync(
        Guid sessionId, CancellationToken cancellationToken);

    Task<ApiCallResult<IntegrityPolicyStatusResponse>> AcceptIntegrityPolicyAsync(
        Guid sessionId, CancellationToken cancellationToken);
}

public sealed class InterviewService(IApiClient apiClient) : IInterviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string InteractionMode = "Text";
    private const int RecentSessionsPageSize = 6;

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var sessions = await GetRecentSessionsAsync(page: 1, RecentSessionsPageSize, cancellationToken);
        var recent = sessions.Data?.Items ?? new List<SessionHistoryItemDto>();
        return new DashboardViewModel
        {
            RecentSessions = recent,
            Sidebar = new InterviewSidebarViewModel
            {
                ActiveItem = "New",
                RecentSessions = recent,
                RecentError = sessions.Success ? null : sessions.Error
            },
            ErrorMessage = sessions.Success ? null : sessions.Error
        };
    }

    public async Task<(bool Success, CatalogResponse? Catalog, string? Error)> GetCatalogAsync(
        CancellationToken cancellationToken)
    {
        var result = await SendAsync<CatalogResponse>(HttpMethod.Get,
            "api/interview/catalog", body: null, cancellationToken);
        return (result.Success, result.Data, result.Error);
    }

    public async Task<StartInterviewResult> StartInterviewAsync(
        int durationMinutes, string level, string position, string? jd, Guid? jdId, List<string> focusAreas,
        CancellationToken cancellationToken)
    {
        var duration = await SendAsync<ConfigResponse>(HttpMethod.Put,
            "api/interview/sessions/config/duration",
            new ConfigDurationRequest { DurationMinutes = durationMinutes },
            cancellationToken);
        if (!duration.Success || duration.Data is null)
            return StartInterviewResult.Failure(duration.Error ?? "Could not configure duration.");

        var sessionId = duration.Data.SessionId;

        var levelConfig = await SendAsync<ConfigResponse>(HttpMethod.Put,
            "api/interview/sessions/config/level",
            new ConfigLevelRequest { SessionId = sessionId, Level = level },
            cancellationToken);
        if (!levelConfig.Success)
            return StartInterviewResult.Failure(levelConfig.Error ?? "Could not configure level.");

        var interaction = await SendAsync<ConfigResponse>(HttpMethod.Put,
            "api/interview/sessions/config/interaction",
            new ConfigInteractionRequest { SessionId = sessionId, Mode = InteractionMode },
            cancellationToken);
        if (!interaction.Success)
            return StartInterviewResult.Failure(interaction.Error ?? "Could not configure interaction mode.");

        var init = await SendAsync<InitResponse>(HttpMethod.Post,
            "api/interview/sessions/init",
            new InitRequest { SessionId = sessionId, Position = position, Jd = jd, JdId = jdId, FocusAreas = focusAreas },
            cancellationToken);
        if (!init.Success || init.Data is null)
            return StartInterviewResult.Failure(init.Error ?? "Could not initialize the session.");

        // Question generation (materializing the first question + credit deduction) happens
        // later, in the Room action, once the biometric entry gate has passed — NOT here.
        // Calling it at Start time would always fail (no session can be verified yet).
        return StartInterviewResult.Ok(sessionId);
    }

    public async Task<(bool Success, string? Error)> GenerateQuestionAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await SendAsync<GenerateQuestionResponse>(HttpMethod.Post,
            $"api/interview/sessions/{sessionId}/questions/generate",
            body: null,
            cancellationToken);
        return (result.Success, result.Error);
    }

    public async Task<(bool Success, RoomViewModel? Room, string? Error)> GetRoomAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var resume = await SendAsync<ResumeResponse>(HttpMethod.Get,
            $"api/interview/sessions/{sessionId}/resume",
            body: null,
            cancellationToken);
        if (!resume.Success || resume.Data is null)
            return (false, null, resume.Error ?? "Could not load the session.");

        var room = new RoomViewModel
        {
            SessionId = sessionId,
            Status = resume.Data.Status,
            Position = resume.Data.Position,
            Level = resume.Data.Level,
            QuestionType = resume.Data.QuestionType,
            RemainingSeconds = resume.Data.RemainingSeconds,
            TotalQuestions = resume.Data.TotalQuestions,
            AnsweredQuestions = resume.Data.AnsweredQuestions,
            CurrentQuestionId = resume.Data.CurrentQuestion?.SessionQuestionId,
            VerificationCheckId = resume.Data.CurrentQuestion?.VerificationCheckId,
            VerificationCheckExpiresAt = resume.Data.CurrentQuestion?.VerificationCheckExpiresAt,
            IdleReminderSeconds = resume.Data.CurrentQuestion?.IdleReminderSeconds ?? 0,
            IdleHardSeconds = resume.Data.CurrentQuestion?.IdleHardSeconds ?? 0,
            IdleElapsedSeconds = resume.Data.CurrentQuestion?.IdleElapsedSeconds ?? 0,
            PauseCount = resume.Data.PauseCount,
            PausesRemaining = resume.Data.PausesRemaining,
            PausedSecondsRemaining = resume.Data.PausedSecondsRemaining,
            HasSeenRoomTour = resume.Data.HasSeenRoomTour,
            ChatHistory = resume.Data.ChatHistory
        };
        return (true, room, null);
    }

    public async Task<(bool Success, MessageResponse? Message, string? Error)> SendMessageAsync(
        Guid sessionId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        var result = await SendAsync<MessageResponse>(HttpMethod.Post,
            $"api/interview/sessions/{sessionId}/message",
            request,
            cancellationToken);
        return (result.Success, result.Data, result.Error);
    }

    public async Task<(bool Success, EndResponse? Completion, string? Error)> CompleteSessionAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await SendAsync<EndResponse>(HttpMethod.Post,
            $"api/interview/sessions/{sessionId}/complete",
            body: null,
            cancellationToken);
        return (result.Success, result.Data, result.Error);
    }

    public async Task<(bool Success, EndResponse? Cancellation, string? Error)> EndSessionAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await SendAsync<EndResponse>(HttpMethod.Post,
            $"api/interview/sessions/{sessionId}/end",
            body: null,
            cancellationToken);
        return (result.Success, result.Data, result.Error);
    }

    public async Task<(bool Success, PauseResponse? Pause, string? Error)> PauseSessionAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await SendAsync<PauseResponse>(HttpMethod.Post,
            $"api/interview/sessions/{sessionId}/pause",
            body: null,
            cancellationToken);
        return (result.Success, result.Data, result.Error);
    }

    public async Task<(bool Success, UnpauseResponse? Unpause, string? Error)> UnpauseSessionAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await SendAsync<UnpauseResponse>(HttpMethod.Post,
            $"api/interview/sessions/{sessionId}/unpause",
            body: null,
            cancellationToken);
        return (result.Success, result.Data, result.Error);
    }

    public async Task<(bool Success, CameraViolationResultModel? Result, string? Error)> ReportCameraViolationAsync(
        Guid sessionId, Guid incidentId, string reason, CancellationToken cancellationToken)
    {
        var result = await SendAsync<CameraViolationResultModel>(HttpMethod.Post,
            $"api/interview/sessions/{sessionId}/camera-violation",
            new { incidentId, reason },
            cancellationToken);
        return (result.Success, result.Data, result.Error);
    }

    public async Task<(bool Success, string? Error)> MarkRoomTourSeenAsync(CancellationToken cancellationToken)
    {
        var result = await SendAsync<object>(HttpMethod.Post,
            "api/interview/room-tour/seen",
            body: null,
            cancellationToken);
        return (result.Success, result.Error);
    }

    public async Task<(bool Success, byte[]? Audio, string? Error)> GetTtsAudioAsync(
        string text, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/interview/sessions/tts");
        request.Content = JsonContent.Create(new { text }, options: JsonOptions);

        using var response = await apiClient.HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, $"TTS request failed ({(int)response.StatusCode}).");
        }

        var audio = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return (true, audio, null);
    }

    public async Task<(bool Success, SttResponse? Result, string? Error)> TranscribeAudioAsync(
        Guid sessionId, Guid checkId, byte[] audio, string fileName, string contentType,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(audio);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "audio", fileName);
        content.Add(new StringContent(checkId.ToString()), "checkId");

        using var response = await apiClient.HttpClient.PostAsync(
            $"api/interview/sessions/{sessionId}/verify/voice-answer", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, $"Transcription failed ({(int)response.StatusCode}).");
        }

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<SttResponse>>(JsonOptions, cancellationToken);
        if (envelope is null || !envelope.Success || envelope.Data is null)
        {
            return (false, null, envelope?.Message ?? "Transcription failed.");
        }

        return (true, envelope.Data, null);
    }

    public async Task<(bool Success, string? Hint, string? Error)> GetHintAsync(
        string question, CancellationToken cancellationToken)
    {
        var result = await SendAsync<HintResponse>(HttpMethod.Post,
            "api/interview/sessions/hint",
            new { question },
            cancellationToken);
        return (result.Success, result.Data?.Hint, result.Error);
    }

    public async Task<HistoryViewModel> GetHistoryAsync(
        int page, int pageSize, string? status, string? position, string? keyword, CancellationToken cancellationToken)
    {
        var result = await GetSessionHistoryPageAsync(page, pageSize, status, position, keyword, cancellationToken);
        var recent = await GetRecentSessionsAsync(1, RecentSessionsPageSize, cancellationToken);
        return new HistoryViewModel
        {
            Sessions = result.Data ?? new SessionHistoryResult { Page = page, PageSize = pageSize },
            Page = page,
            PageSize = pageSize,
            Status = status,
            Position = position,
            Keyword = keyword,
            ErrorMessage = result.Success ? null : result.Error,
            Sidebar = new InterviewSidebarViewModel
            {
                ActiveItem = "History",
                RecentSessions = recent.Data?.Items ?? new List<SessionHistoryItemDto>(),
                RecentError = recent.Success ? null : recent.Error
            }
        };
    }

    public Task<ApiCallResult<SessionHistoryResult>> GetRecentSessionsAsync(
        int page, int pageSize, CancellationToken cancellationToken) =>
        GetSessionHistoryPageAsync(page, pageSize, status: null, position: null, keyword: null, cancellationToken);

    public async Task<SessionDetailViewModel> GetSessionDetailAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var model = new SessionDetailViewModel
        {
            SessionId = sessionId,
            FeedbackForm = new WebClient.Models.Feedback.SubmitInterviewFeedbackRequest { SessionId = sessionId }
        };

        var recent = await GetRecentSessionsAsync(1, RecentSessionsPageSize, cancellationToken);
        model.Sidebar = new InterviewSidebarViewModel
        {
            ActiveItem = "Recent",
            ActiveSessionId = sessionId,
            RecentSessions = recent.Data?.Items ?? new List<SessionHistoryItemDto>(),
            RecentError = recent.Success ? null : recent.Error
        };

        model.Session = await FindSessionMetadataAsync(sessionId, cancellationToken);

        var chat = await SendAsync<SessionChatLogResponse>(HttpMethod.Get,
            $"api/interview/sessions/{sessionId}/chat-log",
            body: null,
            cancellationToken);
        if (!chat.Success || chat.Data is null)
        {
            model.ErrorMessage = ToUserFacingError(chat.StatusCode, chat.Error, "Could not load the chat log.");
            return model;
        }

        model.ChatLog = chat.Data;

        var eligibility = await GetSessionFeedbackEligibilityAsync(sessionId, cancellationToken);
        if (eligibility.Success)
        {
            model.FeedbackEligibility = eligibility.Data;
        }

        if (!model.IsCompleted)
        {
            model.ReportMessage = model.IsCancelled
                ? "This interview was cancelled, so no evaluation report was generated."
                : "This interview expired, so no evaluation report was generated.";
            return model;
        }

        var performance = await SendAsync<PerformanceReportResponse>(HttpMethod.Get,
            $"api/evaluation/sessions/{sessionId}/performance",
            body: null,
            cancellationToken);
        if (performance.Success)
        {
            model.PerformanceReport = performance.Data;
        }
        else if (performance.StatusCode == StatusCodes.Status404NotFound)
        {
            model.ReportMessage = "Report not available.";
        }
        else if (performance.StatusCode == StatusCodes.Status409Conflict)
        {
            model.ReportMessage = "Report not available for this session.";
        }
        else
        {
            model.ReportMessage = ToUserFacingError(performance.StatusCode, performance.Error, "Report not available.");
        }

        return model;
    }

    public async Task<(bool Success, string? Error, int StatusCode)> DeleteSessionAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/interview/sessions/{sessionId}");
        using var response = await apiClient.HttpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return (true, null, (int)response.StatusCode);
        }

        var error = await ReadErrorMessageAsync(response, cancellationToken);
        return (false, ToUserFacingError((int)response.StatusCode, error, "Could not delete this session."), (int)response.StatusCode);
    }

    public Task<ApiCallResult<FeedbackEligibilityResponse>> GetFeedbackEligibilityAsync(CancellationToken cancellationToken) =>
        SendAsync<FeedbackEligibilityResponse>(HttpMethod.Get, "api/interview/sessions/feedback-eligibility", body: null, cancellationToken);

    public Task<ApiCallResult<SessionFeedbackEligibilityResponse>> GetSessionFeedbackEligibilityAsync(
        Guid sessionId, CancellationToken cancellationToken) =>
        SendAsync<SessionFeedbackEligibilityResponse>(HttpMethod.Get,
            $"api/interview/sessions/{sessionId}/feedback-eligibility", body: null, cancellationToken);

    public Task<ApiCallResult<IntegrityPolicyStatusResponse>> GetIntegrityPolicyStatusAsync(
        Guid sessionId, CancellationToken cancellationToken) =>
        SendAsync<IntegrityPolicyStatusResponse>(HttpMethod.Get,
            $"api/interview/sessions/{sessionId}/integrity-policy", body: null, cancellationToken);

    public Task<ApiCallResult<IntegrityPolicyStatusResponse>> AcceptIntegrityPolicyAsync(
        Guid sessionId, CancellationToken cancellationToken) =>
        SendAsync<IntegrityPolicyStatusResponse>(HttpMethod.Post,
            $"api/interview/sessions/{sessionId}/integrity-policy/accept", body: null, cancellationToken);


    private async Task<ApiCallResult<SessionHistoryResult>> GetSessionHistoryPageAsync(
        int page, int pageSize, string? status, string? position, string? keyword, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(position)) query.Add($"position={Uri.EscapeDataString(position)}");
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");

        return await SendAsync<SessionHistoryResult>(
            HttpMethod.Get,
            "api/interview/sessions/history?" + string.Join("&", query),
            body: null,
            cancellationToken);
    }

    private async Task<SessionHistoryItemDto?> FindSessionMetadataAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        const int pageSize = 50;
        var page = 1;
        var totalPages = 1;

        while (page <= totalPages && page <= 20)
        {
            var result = await GetSessionHistoryPageAsync(page, pageSize, null, null, null, cancellationToken);
            if (!result.Success || result.Data is null)
            {
                return null;
            }

            var found = result.Data.Items.FirstOrDefault(x => x.SessionId == sessionId);
            if (found is not null)
            {
                return found;
            }

            totalPages = Math.Max(1, result.Data.TotalPages);
            page++;
        }

        return null;
    }

    private async Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: JsonOptions);
            }

            using var response = await apiClient.HttpClient.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorMessageAsync(response, raw, cancellationToken);
                return ApiCallResult<T>.Fail(error ?? $"Request failed ({statusCode}).", statusCode);
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return ApiCallResult<T>.Ok(default, statusCode);
            }

            ApiResponse<T>? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<ApiResponse<T>>(raw, JsonOptions);
            }
            catch (JsonException)
            {
                return ApiCallResult<T>.Fail($"Request to {path} returned an unreadable response ({statusCode}).", statusCode);
            }

            if (envelope is null || !envelope.Success)
            {
                return ApiCallResult<T>.Fail(envelope?.Message ?? $"Request failed ({statusCode}).", statusCode);
            }

            return ApiCallResult<T>.Ok(envelope.Data, statusCode);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<T>.Fail("The request timed out. Please try again.", StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<T>.Fail("Cannot reach the server. Please make sure the API Gateway is running.", StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<string?> ReadErrorMessageAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        return await ReadErrorMessageAsync(response, raw, cancellationToken);
    }

    private static Task<string?> ReadErrorMessageAsync(
        HttpResponseMessage response, string raw, CancellationToken cancellationToken)
    {
        _ = response;
        _ = cancellationToken;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<ApiResponse<object>>(raw, JsonOptions);
            return Task.FromResult<string?>(envelope?.Message ?? raw);
        }
        catch (JsonException)
        {
            return Task.FromResult<string?>(raw.Length > 200 ? raw[..200] : raw);
        }
    }

    private static string ToUserFacingError(int statusCode, string? backendMessage, string fallback) =>
        statusCode switch
        {
            StatusCodes.Status401Unauthorized => "Please log in to continue.",
            StatusCodes.Status403Forbidden => "You do not have permission to view this session.",
            StatusCodes.Status404NotFound => "Session not found.",
            StatusCodes.Status409Conflict => backendMessage ?? fallback,
            StatusCodes.Status503ServiceUnavailable => "The service is temporarily unavailable. Please try again later.",
            StatusCodes.Status504GatewayTimeout => "The request timed out. Please try again.",
            _ => string.IsNullOrWhiteSpace(backendMessage) ? fallback : backendMessage
        };
}
