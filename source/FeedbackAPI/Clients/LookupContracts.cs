namespace FeedbackAPI.Clients;

// JSON contract khớp shape endpoint nội bộ UserAPI (không share assembly, chỉ cần trùng field).

/// <summary>Body gửi đi: danh sách AccountId cần resolve.</summary>
public record LookupRequest(List<Guid> AccountIds);

/// <summary>Phần tử trả về từ UserAPI POST /internal/profiles/lookup.</summary>
public record ProfileLookupItem(Guid AccountId, string? FullName);

/// <summary>Phần tử trả về từ AuthAPI POST /internal/accounts/lookup.</summary>
public record AccountLookupItem(Guid AccountId, string? Email);

/// <summary>Body gửi đi: tìm AccountId theo keyword (tên hoặc email).</summary>
public record SearchByKeywordRequest(string Keyword);
