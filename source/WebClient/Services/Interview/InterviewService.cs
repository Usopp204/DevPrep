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
        int durationMinutes, string level, string position, string? jd, List<string> focusAreas,
        CancellationToken cancellationToken);

    Task<(bool Success, CatalogResponse? Catalog, string? Error)> GetCatalogAsync(CancellationToken cancellationToken);

    Task<(bool Success, RoomViewModel? Room, string? Error)> GetRoomAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<(bool Success, MessageResponse? Message, string? Error)> SendMessageAsync(
        Guid sessionId, SendMessageRequest request, CancellationToken cancellationToken);

    Task<(bool Success, string? Error)> EndSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<(bool Success, byte[]? Audio, string? Error)> GetTtsAudioAsync(string text, CancellationToken cancellationToken);

    Task<(bool Success, string? Text, string? Error)> TranscribeAudioAsync(
        byte[] audio, string fileName, string contentType, CancellationToken cancellationToken);

    Task<(bool Success, string? Hint, string? Error)> GetHintAsync(string question, CancellationToken cancellationToken);

    Task<HistoryViewModel> GetHistoryAsync(Guid? selectedSessionId, CancellationToken cancellationToken);
}

public sealed class InterviewService(IApiClient apiClient) : IInterviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Voice mode is not wired up in this UI (mic/TTS controls are not exposed through the
    // Gateway), so every session is configured as Text.
    private const string InteractionMode = "Text";

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken)
    {
        // Reuse the same list endpoint History uses; show only the 5 most recent in the sidebar.
        var sessions = await SendAsync<List<SessionListItemDto>>(HttpMethod.Get,
            "api/interview/sessions", body: null, cancellationToken);

        return new DashboardViewModel
        {
            RecentSessions = (sessions.Data ?? new List<SessionListItemDto>()).Take(5).ToList()
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
        int durationMinutes, string level, string position, string? jd, List<string> focusAreas,
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
            new InitRequest { SessionId = sessionId, Position = position, Jd = jd, FocusAreas = focusAreas },
            cancellationToken);
        if (!init.Success || init.Data is null)
            return StartInterviewResult.Failure(init.Error ?? "Could not initialize the session.");

        // Init only returns the first question as plain text (no id). Call generate so the
        // first question is materialized as a real SessionQuestion + AI chat message, the same
        // way every later question is (Room fetches all of this back via Resume).
        var firstQuestion = await SendAsync<GenerateQuestionResponse>(HttpMethod.Post,
            $"api/interview/sessions/{sessionId}/questions/generate",
            body: null,
            cancellationToken);
        if (!firstQuestion.Success || firstQuestion.Data is null)
            return StartInterviewResult.Failure(firstQuestion.Error ?? "Could not load the first question.");

        return StartInterviewResult.Ok(sessionId);
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
            RemainingSeconds = resume.Data.RemainingSeconds,
            TotalQuestions = resume.Data.TotalQuestions,
            AnsweredQuestions = resume.Data.AnsweredQuestions,
            CurrentQuestionId = resume.Data.CurrentQuestion?.SessionQuestionId,
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

    public async Task<(bool Success, string? Error)> EndSessionAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await SendAsync<EndResponse>(HttpMethod.Post,
            $"api/interview/sessions/{sessionId}/end",
            body: null,
            cancellationToken);
        return (result.Success, result.Error);
    }

    // Binary proxy (NOT SendAsync, which parses JSON): forwards to the Gateway TTS endpoint and
    // returns the raw mp3 bytes so the Room can play them.
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

    // Multipart proxy (NOT SendAsync JSON): forwards the recorded audio to the Gateway STT endpoint
    // and returns the transcribed text.
    public async Task<(bool Success, string? Text, string? Error)> TranscribeAudioAsync(
        byte[] audio, string fileName, string contentType, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(audio);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "audio", fileName);

        using var response = await apiClient.HttpClient.PostAsync(
            "api/interview/sessions/stt", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, $"Transcription failed ({(int)response.StatusCode}).");
        }

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<SttResponse>>(JsonOptions, cancellationToken);
        if (envelope is null || !envelope.Success || envelope.Data is null)
        {
            return (false, null, envelope?.Message ?? "Transcription failed.");
        }

        return (true, envelope.Data.Text, null);
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

    public async Task<HistoryViewModel> GetHistoryAsync(Guid? selectedSessionId, CancellationToken cancellationToken)
    {
        var sessions = await SendAsync<List<SessionListItemDto>>(HttpMethod.Get,
            "api/interview/sessions",
            body: null,
            cancellationToken);

        var sessionList = sessions.Data ?? new List<SessionListItemDto>();
        var model = new HistoryViewModel
        {
            Sessions = sessionList,
            ErrorMessage = sessions.Success ? null : sessions.Error
        };

        var sessionId = selectedSessionId ?? sessionList.FirstOrDefault()?.SessionId;
        if (sessionId is null)
        {
            return model;
        }

        model.SelectedSessionId = sessionId;

        var transcript = await SendAsync<TranscriptResponse>(HttpMethod.Get,
            $"api/interview/sessions/{sessionId}/transcript",
            body: null,
            cancellationToken);
        if (!transcript.Success || transcript.Data is null)
        {
            model.ErrorMessage = transcript.Error ?? "Could not load the session transcript.";
            return model;
        }

        // Read-only here: unlike the Report page, History never calls POST /report — it only
        // shows one if it was already built elsewhere.
        var report = await SendAsync<ReportResponse>(HttpMethod.Get,
            $"api/evaluation/sessions/{sessionId}/report",
            body: null,
            cancellationToken);

        var evaluations = await SendAsync<List<EvaluateAnswerResponse>>(HttpMethod.Get,
            $"api/evaluation/sessions/{sessionId}/answers",
            body: null,
            cancellationToken);

        model.Detail = new HistorySessionDetailViewModel
        {
            SessionId = sessionId.Value,
            Position = transcript.Data.Position,
            Level = transcript.Data.Level,
            Status = transcript.Data.Status,
            TotalQuestions = transcript.Data.TotalQuestions,
            AnsweredQuestions = transcript.Data.AnsweredQuestions,
            StartedAt = transcript.Data.StartedAt,
            EndedAt = transcript.Data.EndedAt,
            Questions = transcript.Data.Questions,
            ChatHistory = transcript.Data.ChatHistory,
            Report = report.Data,
            Evaluations = evaluations.Data ?? new List<EvaluateAnswerResponse>()
        };
        return model;
    }

    private async Task<(bool Success, T? Data, string? Error)> SendAsync<T>(
        HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await apiClient.HttpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        // A non-2xx or empty body (e.g. a 401 from the auth middleware, which returns no JSON)
        // is not parseable — deserializing it throws "The input does not contain any JSON tokens".
        // Surface the HTTP status (and any body snippet) instead of crashing.
        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(raw))
        {
            var snippet = string.IsNullOrWhiteSpace(raw)
                ? string.Empty
                : " " + (raw.Length > 200 ? raw[..200] : raw).Trim();
            return (false, default, $"Request to {path} failed ({(int)response.StatusCode}).{snippet}");
        }

        ApiResponse<T>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ApiResponse<T>>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            return (false, default, $"Request to {path} returned an unreadable response ({(int)response.StatusCode}).");
        }

        if (envelope is null || !envelope.Success)
        {
            return (false, default, envelope?.Message ?? $"Request to {path} failed ({(int)response.StatusCode}).");
        }

        return (true, envelope.Data, null);
    }
}
