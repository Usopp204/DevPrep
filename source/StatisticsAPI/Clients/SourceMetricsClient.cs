using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using StatisticsAPI.Services;

namespace StatisticsAPI.Clients;

/// <summary>Doanh thu theo ngày lấy từ PaymentAPI (đã gộp sẵn tổng + map theo ngày).</summary>
public sealed record RevenueDailyData(
    decimal TotalRevenue,
    int TotalOrders,
    Dictionary<DateOnly, (decimal Revenue, int Orders)> ByDay);

public interface ISourceMetricsClient
{
    Task<long> GetUserCountAsync(DateTime? createdFrom, DateTime? createdTo, string? status, CancellationToken ct);
    Task<long> GetInterviewCountAsync(DateTime? from, DateTime? to, string? status, bool includeDeleted, CancellationToken ct);
    Task<long> GetFeedbackCountAsync(string kind, DateTime? from, DateTime? to, CancellationToken ct);
    Task<RevenueDailyData> GetRevenueDailyAsync(DateOnly from, DateOnly to, CancellationToken ct);
    Task<decimal> GetRevenueReportTotalAsync(string period, CancellationToken ct);
}

/// <summary>
/// Gọi admin endpoint của các service nguồn (User/Interview/Feedback/Payment) QUA ApiGateway
/// để lấy số liệu tổng hợp. KHÔNG join DB chéo (đúng tinh thần microservice). Đính JWT role=Admin
/// tự ký cho các endpoint cần [Authorize] (gateway forward nguyên header Authorization);
/// PaymentAPI /admin/payments/* không cần auth nhưng đính token cũng vô hại.
/// Named HttpClient "gateway" set BaseAddress (:7000) + UseProxy=false ở Program.cs.
/// </summary>
public sealed class SourceMetricsClient(
    IHttpClientFactory httpClientFactory,
    IServiceTokenFactory tokenFactory) : ISourceMetricsClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string? _token;
    private string Token => _token ??= tokenFactory.CreateAdminToken();

    public async Task<long> GetUserCountAsync(DateTime? createdFrom, DateTime? createdTo, string? status, CancellationToken ct)
    {
        var qs = new List<string> { "Page=1", "PageSize=1" };
        if (createdFrom.HasValue) qs.Add("CreatedFrom=" + Iso(createdFrom.Value));
        if (createdTo.HasValue) qs.Add("CreatedTo=" + Iso(createdTo.Value));
        if (!string.IsNullOrWhiteSpace(status)) qs.Add("Status=" + Uri.EscapeDataString(status));

        using var doc = await GetJsonAsync("api/admin/users?" + string.Join("&", qs), ct);
        return doc.RootElement.GetProperty("data").GetProperty("totalCount").GetInt64();
    }

    public async Task<long> GetInterviewCountAsync(DateTime? from, DateTime? to, string? status, bool includeDeleted, CancellationToken ct)
    {
        var qs = new List<string> { "page=1", "pageSize=1" };
        if (!string.IsNullOrWhiteSpace(status)) qs.Add("status=" + Uri.EscapeDataString(status));
        if (from.HasValue) qs.Add("dateFrom=" + Iso(from.Value));
        if (to.HasValue) qs.Add("dateTo=" + Iso(to.Value));
        if (includeDeleted) qs.Add("includeDeleted=true");

        using var doc = await GetJsonAsync("api/admin/interviews?" + string.Join("&", qs), ct);
        return doc.RootElement.GetProperty("data").GetProperty("totalItems").GetInt64();
    }

    public async Task<long> GetFeedbackCountAsync(string kind, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var qs = new List<string> { "page=1", "pageSize=1" };
        if (from.HasValue) qs.Add("dateFrom=" + Iso(from.Value));
        if (to.HasValue) qs.Add("dateTo=" + Iso(to.Value));

        using var doc = await GetJsonAsync($"api/admin/feedbacks/{kind}?" + string.Join("&", qs), ct);
        return doc.RootElement.GetProperty("data").GetProperty("totalItems").GetInt64();
    }

    public async Task<RevenueDailyData> GetRevenueDailyAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        // Qua gateway: route /api/payment/* strip prefix -> PaymentAPI nhận /admin/payments/...
        var url = $"api/payment/admin/payments/revenue-daily?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        using var doc = await GetJsonAsync(url, ct);
        var root = doc.RootElement;

        var byDay = new Dictionary<DateOnly, (decimal, int)>();
        if (root.TryGetProperty("days", out var days) && days.ValueKind == JsonValueKind.Array)
        {
            foreach (var d in days.EnumerateArray())
            {
                var date = DateOnly.FromDateTime(d.GetProperty("date").GetDateTime());
                var rev = d.GetProperty("revenue").GetDecimal();
                var ord = d.GetProperty("orders").GetInt32();
                byDay[date] = (rev, ord);
            }
        }

        return new RevenueDailyData(
            root.GetProperty("totalRevenue").GetDecimal(),
            root.GetProperty("totalOrders").GetInt32(),
            byDay);
    }

    public async Task<decimal> GetRevenueReportTotalAsync(string period, CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"api/payment/admin/payments/revenue-report?period={Uri.EscapeDataString(period)}", ct);
        return doc.RootElement.GetProperty("totalRevenue").GetDecimal();
    }

    // --- helpers ---------------------------------------------------------------

    private async Task<JsonDocument> GetJsonAsync(string relativeUrl, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("gateway");
        using var req = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var resp = await client.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private static string Iso(DateTime dt) => Uri.EscapeDataString(dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
}
