using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models.Payment;
using WebClient.Services.Shared;

namespace WebClient.Services.Payment;

public interface IPaymentService
{
    Task<BuyCreditsViewModel> GetBuyCreditsAsync(CancellationToken ct = default);
    Task<CreateOrderResponse> CreateOrderAsync(Guid packageId, Guid userId, CancellationToken ct = default);
    // null means "we could not find out" (PaymentAPI down, timed out, bad payload) — NOT "zero
    // credits". Callers must decide how to show the unknown case; they must not render it as 0.
    Task<CreditBalanceDto?> GetCreditBalanceAsync(Guid userId, CancellationToken ct = default);
    Task<TransactionHistoryViewModel> GetTransactionHistoryAsync(Guid userId, int page = 1, int pageSize = 10, CancellationToken ct = default);
    Task<CreditHistoryViewModel> GetCreditHistoryAsync(Guid userId, int page = 1, int pageSize = 10, string? type = null, CancellationToken ct = default);
}

public sealed class PaymentService(IApiClient apiClient, ILogger<PaymentService> logger) : IPaymentService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Shown to the user when a payment call fails. Deliberately fixed text: `ex.Message` here is
    // raw transport detail ("Response status code does not indicate success: 502 (Bad Gateway)")
    // which tells a user nothing they can act on and exposes internals. The exception itself goes
    // to the log, where it is actually useful.
    private const string UnavailableMessage =
        "Credit service is temporarily unavailable. Please try again shortly.";

    public async Task<BuyCreditsViewModel> GetBuyCreditsAsync(CancellationToken ct = default)
    {
        try
        {
            var packages = await apiClient.HttpClient
                .GetFromJsonAsync<List<CreditPackageDto>>("api/payment/packages", ct);
            return new BuyCreditsViewModel { Packages = packages ?? [] };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Loading credit packages failed.");
            return new BuyCreditsViewModel { ErrorMessage = UnavailableMessage };
        }
    }

    public async Task<CreateOrderResponse> CreateOrderAsync(Guid packageId, Guid userId, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/payment/vnpay/order")
        {
            Content = JsonContent.Create(new CreateOrderRequest { PackageId = packageId })
        };
        request.Headers.Add("X-User-Id", userId.ToString());
        var response = await apiClient.HttpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateOrderResponse>(ct)
               ?? throw new InvalidOperationException("Empty response from payment API.");
    }

    public async Task<CreditBalanceDto?> GetCreditBalanceAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "api/payment/credits/balance");
            request.Headers.Add("X-User-Id", userId.ToString());
            var response = await apiClient.HttpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CreditBalanceDto>(JsonOpts, ct);
        }
        // Returning `new CreditBalanceDto()` here used to turn every failure into a confident
        // "0 credits" in the navbar dropdown — a wrong number shown to the user as if it were
        // real. null means unknown, and the caller renders it as such.
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Credit balance lookup failed; reporting balance as unknown.");
            return null;
        }
    }

    public async Task<TransactionHistoryViewModel> GetTransactionHistoryAsync(
        Guid userId, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        try
        {
            var balanceTask = GetCreditBalanceAsync(userId, ct);

            var request = new HttpRequestMessage(HttpMethod.Get,
                $"api/payment/transactions?page={page}&pageSize={pageSize}");
            request.Headers.Add("X-User-Id", userId.ToString());
            var response = await apiClient.HttpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TransactionPageResult>(JsonOpts, ct);
            var balance = await balanceTask;

            return new TransactionHistoryViewModel
            {
                // Left nullable on purpose: the ledger call can succeed while the balance lookup
                // fails, and the page should then show the table with a dash in the balance card
                // rather than inventing a 0.
                Balance    = balance,
                Items      = result?.Items ?? [],
                TotalCount = result?.TotalCount ?? 0,
                Page       = page,
                PageSize   = pageSize
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Loading transaction history failed for page {Page}.", page);
            return new TransactionHistoryViewModel { ErrorMessage = UnavailableMessage };
        }
    }

    public async Task<CreditHistoryViewModel> GetCreditHistoryAsync(
        Guid userId, int page = 1, int pageSize = 10, string? type = null, CancellationToken ct = default)
    {
        try
        {
            var balanceTask = GetCreditBalanceAsync(userId, ct);

            var url = $"api/payment/credits/ledger?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(type)) url += $"&type={Uri.EscapeDataString(type)}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-User-Id", userId.ToString());
            var response = await apiClient.HttpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LedgerPageResult>(JsonOpts, ct);
            var balance = await balanceTask;

            return new CreditHistoryViewModel
            {
                // Nullable on purpose — see GetTransactionHistoryAsync. TotalUsed/TotalRefunded do
                // come from the ledger call that just succeeded, so they are real numbers here.
                Balance       = balance,
                TotalUsed     = result?.TotalUsed ?? 0,
                TotalRefunded = result?.TotalRefunded ?? 0,
                Type          = string.IsNullOrWhiteSpace(type) ? null : type,
                Items         = result?.Items ?? [],
                TotalCount    = result?.TotalCount ?? 0,
                Page          = page,
                PageSize      = pageSize
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Loading credit history failed for page {Page}, type {Type}.", page, type);
            return new CreditHistoryViewModel { ErrorMessage = UnavailableMessage };
        }
    }

    private sealed class TransactionPageResult
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<TransactionDto> Items { get; set; } = [];
    }

    private sealed class LedgerPageResult
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalUsed { get; set; }
        public int TotalRefunded { get; set; }
        public List<CreditLedgerItemDto> Items { get; set; } = [];
    }
}
