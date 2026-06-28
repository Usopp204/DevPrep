namespace InterviewAPI.Clients;

// JSON contract khớp với endpoint nội bộ của AuthAPI/UserAPI (chỉ cần trùng shape, không share assembly).

/// <summary>Body gửi đi: danh sách AccountId cần resolve.</summary>
public record LookupRequest(List<Guid> AccountIds);

/// <summary>Phần tử trả về từ AuthAPI POST /internal/accounts/lookup.</summary>
public record AccountLookupItem(Guid AccountId, string? Email);

/// <summary>Phần tử trả về từ UserAPI POST /internal/profiles/lookup.</summary>
public record ProfileLookupItem(Guid AccountId, string? FullName);
