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
                // A non-2xx means PaymentAPI never gave us a business answer, so this is treated as
                // "service unavailable" rather than a refusal. A genuine refusal (not enough
                // credits) comes back as 2xx with Success=false in the body, below.
                return new CreditDeductionResponse
                {
                    Success = false,
                    ServiceAvailable = false,
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
            // Transport-level failure (DNS, refused connection, timeout, open circuit). Message is
            // kept for the server log only — ServiceAvailable=false tells callers not to show it.
            return new CreditDeductionResponse
            {
                Success = false,
                ServiceAvailable = false,
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
                // Same classification as deduct: no business answer means unavailable, not refused.
                return new CreditDeductionResponse
                {
                    Success = false,
                    ServiceAvailable = false,
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
            // Transport-level failure — detail is for the server log only, never the browser.
            return new CreditDeductionResponse
            {
                Success = false,
                ServiceAvailable = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<CreditAwardResponse> AwardCreditAsync(
        Guid userId, int amount, Guid referenceId, string description, CancellationToken ct)
    {
        try
        {
            var request = new
            {
                UserId = userId,
                Amount = amount,
                ReferenceId = referenceId,
                Description = description
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/internal/credit/award", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "PaymentAPI award credit failed: status={StatusCode}, userId={UserId}, amount={Amount}",
                    response.StatusCode, userId, amount);
                return new CreditAwardResponse
                {
                    Success = false,
                    Message = $"PaymentAPI error: {response.StatusCode}"
                };
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var result = await JsonSerializer.DeserializeAsync<CreditAwardResponse>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct)
                ?? new CreditAwardResponse { Success = false, Message = "Empty response" };

            _logger.LogInformation(
                "PaymentAPI award credit result: userId={UserId}, amount={Amount}, success={Success}, awarded={Awarded}, balanceAfter={Balance}",
                userId, amount, result.Success, result.Awarded, result.BalanceAfter);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PaymentAPI award credit exception: userId={UserId}, amount={Amount}", userId, amount);
            return new CreditAwardResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<CreditBalanceResult> GetBalanceAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/credits/balance");
            request.Headers.Add("X-User-Id", userId.ToString());
            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetBalance failed: status={StatusCode}, userId={UserId}",
                    response.StatusCode, userId);
                return CreditBalanceResult.Unknown($"PaymentAPI returned {(int)response.StatusCode}.");
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var jsonDoc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            // Case-INSENSITIVE lookup. PaymentAPI's GET /credits/balance is a minimal-API
            // endpoint returning `new { Balance = ... }`, and ASP.NET Core's default web
            // serializer camel-cases that on the wire: the body is {"balance":0,...}.
            // JsonElement.GetProperty("Balance") is case-SENSITIVE, so it threw
            // KeyNotFoundException on every single call — swallowed by the catch-all below and
            // reported as Unknown ("balance not known right now"). Every caller then treated a
            // perfectly healthy PaymentAPI as an outage; for the credit gates in SessionService
            // that meant Available=false, which those gates deliberately do NOT block on, so a
            // user with 0 credits walked through the enrolment/policy steps unchallenged.
            // Nothing distinguished this from a real outage because the failure mode is
            // identical, which is why it survived. Do not "simplify" this back to GetProperty.
            foreach (var property in jsonDoc.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("balance", StringComparison.OrdinalIgnoreCase))
                    return CreditBalanceResult.Known(property.Value.GetInt32());
            }

            _logger.LogError(
                "GetBalance: PaymentAPI response had no 'balance' property, userId={UserId}", userId);
            return CreditBalanceResult.Unknown("Credit service returned an unexpected response.");
        }
        // A caller-cancelled request is not a PaymentAPI failure; let it propagate so it is not
        // reported as "balance unavailable" to a user who simply navigated away.
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Covers connection failures, HttpClient timeouts, malformed payloads, and
            // BrokenCircuitException once the breaker has opened. All mean the same thing to a
            // caller: the balance is not known right now. Never 0 — that is a real number.
            _logger.LogError(ex, "GetBalance exception: userId={UserId}", userId);
            return CreditBalanceResult.Unknown("Credit service is unavailable.");
        }
    }
}
