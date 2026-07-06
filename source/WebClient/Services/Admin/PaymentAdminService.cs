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
    Task<AdminPaymentsViewModel> GetPaymentsAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);
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

    public async Task<AdminPaymentsViewModel> GetPaymentsAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var result = await apiClient.HttpClient.GetFromJsonAsync<PaymentPageResult>(
                $"api/payment/admin/payments?page={page}&pageSize={pageSize}", Opts, ct);
            return new AdminPaymentsViewModel
            {
                Items      = result?.Items ?? [],
                TotalCount = result?.TotalCount ?? 0,
                Page       = page,
                PageSize   = pageSize
            };
        }
        catch (Exception ex)
        {
            return new AdminPaymentsViewModel { ErrorMessage = ex.Message };
        }
    }

    private sealed class PaymentPageResult
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<AdminPaymentOrderDto> Items { get; set; } = [];
    }
}
