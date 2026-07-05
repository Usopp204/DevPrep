using System.Net;
using System.Net.Http.Json;
using WebClient.Models.Payment;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Payment;

public interface IPaymentService
{
    PaymentViewModel GetOverview();
    Task<TransactionHistoryViewModel> GetTransactionHistoryAsync(int page, int pageSize = 10, CancellationToken ct = default);
}

public sealed class PaymentService(IApiClient apiClient) : IPaymentService
{
    private const string CannotReach =
        "Cannot reach the server. Please make sure the API Gateway is running and try again.";

    public PaymentViewModel GetOverview()
    {
        _ = apiClient;

        return new PaymentViewModel
        {
            ModuleName = "Payment & Service",
            Epic = "Payment & Service",
            Summary = "Payment and service placeholder for future API Gateway integration."
        };
    }

    public async Task<TransactionHistoryViewModel> GetTransactionHistoryAsync(int page, int pageSize = 10, CancellationToken ct = default)
    {
        try
        {
            using var response = await apiClient.HttpClient.GetAsync(
                $"api/payment/transactions?page={page}&pageSize={pageSize}", ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new TransactionHistoryViewModel
                {
                    Page = page,
                    PageSize = pageSize,
                    ErrorMessage = "Session expired. Please log in again."
                };
            }

            var body = await ReadBodyAsync(response);
            if (response.IsSuccessStatusCode && body?.Success == true && body.Data is not null)
            {
                var data = body.Data;
                return new TransactionHistoryViewModel
                {
                    Balance = new WalletBalanceViewModel
                    {
                        Balance = data.Balance.Balance,
                        TotalPurchased = data.Balance.TotalPurchased,
                    },
                    Items = data.Transactions.Items.Select(t => new TransactionItemViewModel
                    {
                        OrderCode = t.OrderCode,
                        PaymentMethod = t.PaymentMethod,
                        Credits = t.Credits,
                        Amount = t.Amount,
                        Currency = t.Currency,
                        Status = t.Status,
                        CreatedAt = t.CreatedAt,
                        PaidAt = t.PaidAt,
                    }).ToList(),
                    Page = data.Transactions.PageIndex,
                    PageSize = data.Transactions.PageSize,
                    TotalCount = data.Transactions.TotalCount,
                    TotalPages = data.Transactions.TotalPages,
                };
            }

            return new TransactionHistoryViewModel
            {
                Page = page,
                PageSize = pageSize,
                ErrorMessage = body?.Message ?? $"Failed to load transaction history (HTTP {(int)response.StatusCode})."
            };
        }
        catch (Exception)
        {
            return new TransactionHistoryViewModel
            {
                Page = page,
                PageSize = pageSize,
                ErrorMessage = CannotReach
            };
        }
    }

    private static async Task<BackendResponse<TransactionHistoryData>?> ReadBodyAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<BackendResponse<TransactionHistoryData>>();
        }
        catch
        {
            return null;
        }
    }
}
