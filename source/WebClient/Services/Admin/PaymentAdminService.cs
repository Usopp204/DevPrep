using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models.Admin;
using WebClient.Services.Shared;

namespace WebClient.Services.Admin;

public interface IPaymentAdminService
{
    Task<CreditPackagesAdminViewModel> GetPackagesAsync(CancellationToken ct = default);
    Task<(bool Ok, string? Error)> CreatePackageAsync(CreditPackageAdminDto dto, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> UpdatePackageAsync(Guid id, CreditPackageAdminDto dto, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> DeletePackageAsync(Guid id, CancellationToken ct = default);
    Task<AdminPaymentsViewModel> GetPaymentsAsync(
        int page = 1, int pageSize = 20,
        string? keyword = null, string? status = null, string? paymentMethod = null,
        DateTime? dateFrom = null, DateTime? dateTo = null,
        decimal? minAmount = null, decimal? maxAmount = null,
        CancellationToken ct = default);
    Task<RevenueReportViewModel> GetRevenueReportAsync(string period = "today", DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
}

public sealed class PaymentAdminService(IApiClient apiClient) : IPaymentAdminService
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    public async Task<CreditPackagesAdminViewModel> GetPackagesAsync(CancellationToken ct = default)
    {
        try
        {
            var packages = await apiClient.HttpClient
                .GetFromJsonAsync<List<CreditPackageAdminDto>>("api/payment/admin/credit-packages", Opts, ct);
            return new CreditPackagesAdminViewModel { Packages = packages ?? [] };
        }
        catch (Exception ex)
        {
            return new CreditPackagesAdminViewModel { ErrorMessage = ex.Message };
        }
    }

    public async Task<(bool Ok, string? Error)> CreatePackageAsync(CreditPackageAdminDto dto, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                dto.Name, dto.Description, dto.Credits, dto.BonusCredits,
                dto.Price, Currency = "VND", dto.SortOrder
            };
            var res = await apiClient.HttpClient.PostAsJsonAsync("api/payment/admin/credit-packages", payload, ct);
            if (res.IsSuccessStatusCode) return (true, null);
            var body = await res.Content.ReadAsStringAsync(ct);
            return (false, body);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Ok, string? Error)> UpdatePackageAsync(Guid id, CreditPackageAdminDto dto, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                dto.Name, dto.Description, dto.Credits, dto.BonusCredits,
                dto.Price, Currency = "VND", dto.SortOrder
            };
            var res = await apiClient.HttpClient.PutAsJsonAsync($"api/payment/admin/credit-packages/{id}", payload, ct);
            if (res.IsSuccessStatusCode) return (true, null);
            var body = await res.Content.ReadAsStringAsync(ct);
            return (false, body);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Ok, string? Error)> DeletePackageAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var res = await apiClient.HttpClient.DeleteAsync($"api/payment/admin/credit-packages/{id}", ct);
            if (res.IsSuccessStatusCode) return (true, null);
            var body = await res.Content.ReadAsStringAsync(ct);
            return (false, body);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<AdminPaymentsViewModel> GetPaymentsAsync(
        int page = 1, int pageSize = 20,
        string? keyword = null, string? status = null, string? paymentMethod = null,
        DateTime? dateFrom = null, DateTime? dateTo = null,
        decimal? minAmount = null, decimal? maxAmount = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = BuildQuery(new()
            {
                ["page"] = page.ToString(),
                ["pageSize"] = pageSize.ToString(),
                ["keyword"] = keyword,
                ["status"] = status,
                ["paymentMethod"] = paymentMethod,
                ["dateFrom"] = dateFrom?.ToString("yyyy-MM-dd"),
                ["dateTo"] = dateTo?.ToString("yyyy-MM-dd"),
                ["minAmount"] = minAmount?.ToString(),
                ["maxAmount"] = maxAmount?.ToString()
            });
            var result = await apiClient.HttpClient.GetFromJsonAsync<PaymentPageResult>(
                $"api/payment/admin/payments?{query}", Opts, ct);
            return new AdminPaymentsViewModel
            {
                Items       = result?.Items ?? [],
                TotalCount  = result?.TotalCount ?? 0,
                Page        = page,
                PageSize    = pageSize,
                TotalAmount = result?.TotalAmount ?? 0,
                PaidAmount  = result?.PaidAmount ?? 0,
                PaidCount   = result?.PaidCount ?? 0,
                Keyword       = keyword,
                Status        = status,
                PaymentMethod = paymentMethod,
                DateFrom      = dateFrom,
                DateTo        = dateTo,
                MinAmount     = minAmount,
                MaxAmount     = maxAmount
            };
        }
        catch (Exception ex)
        {
            return new AdminPaymentsViewModel { ErrorMessage = ex.Message };
        }
    }

    public async Task<RevenueReportViewModel> GetRevenueReportAsync(string period = "today", DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        try
        {
            var query = BuildQuery(new()
            {
                ["period"] = period,
                ["from"] = from?.ToString("yyyy-MM-dd"),
                ["to"] = to?.ToString("yyyy-MM-dd")
            });
            var result = await apiClient.HttpClient.GetFromJsonAsync<RevenueReportResult>(
                $"api/payment/admin/payments/revenue-report?{query}", Opts, ct);
            return new RevenueReportViewModel
            {
                Period       = result?.Period ?? period,
                From         = result?.From ?? DateTime.Today,
                To           = result?.To ?? DateTime.Today,
                PackagesSold = result?.PackagesSold ?? 0,
                TotalRevenue = result?.TotalRevenue ?? 0,
                TotalCredits = result?.TotalCredits ?? 0
            };
        }
        catch (Exception ex)
        {
            return new RevenueReportViewModel { Period = period, ErrorMessage = ex.Message };
        }
    }

    private static string BuildQuery(Dictionary<string, string?> parts)
    {
        var pairs = parts
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}");
        return string.Join('&', pairs);
    }

    private sealed class PaymentPageResult
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public int PaidCount { get; set; }
        public List<AdminPaymentOrderDto> Items { get; set; } = [];
    }

    private sealed class RevenueReportResult
    {
        public string Period { get; set; } = "today";
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int PackagesSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalCredits { get; set; }
    }
}
