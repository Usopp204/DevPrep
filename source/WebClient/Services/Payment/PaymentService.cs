using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models.Payment;
using WebClient.Services.Shared;

namespace WebClient.Services.Payment;

public interface IPaymentService
{
    Task<BuyCreditsViewModel> GetBuyCreditsAsync(CancellationToken ct = default);
    Task<CreateOrderResponse> CreateOrderAsync(Guid packageId, Guid userId, CancellationToken ct = default);
    Task<CreditBalanceDto> GetCreditBalanceAsync(Guid userId, CancellationToken ct = default);
    Task<TransactionHistoryViewModel> GetTransactionHistoryAsync(Guid userId, int page = 1, int pageSize = 10, CancellationToken ct = default);
}

public sealed class PaymentService(IApiClient apiClient) : IPaymentService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

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
            return new BuyCreditsViewModel { ErrorMessage = ex.Message };
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

    public async Task<CreditBalanceDto> GetCreditBalanceAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "api/payment/credits/balance");
            request.Headers.Add("X-User-Id", userId.ToString());
            var response = await apiClient.HttpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CreditBalanceDto>(JsonOpts, ct) ?? new();
        }
        catch
        {
            return new CreditBalanceDto();
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
                Balance    = balance,
                Items      = result?.Items ?? [],
                TotalCount = result?.TotalCount ?? 0,
                Page       = page,
                PageSize   = pageSize
            };
        }
        catch (Exception ex)
        {
            return new TransactionHistoryViewModel { ErrorMessage = ex.Message };
        }
    }

    private sealed class TransactionPageResult
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<TransactionDto> Items { get; set; } = [];
    }
}
