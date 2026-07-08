namespace AuthAPI.DTOs;

/// <summary>Yêu cầu tra cứu nhiều tài khoản theo Id (gọi nội bộ từ service khác).</summary>
public record AccountLookupRequest(List<Guid> AccountIds);

/// <summary>Thông tin định danh cơ bản của một tài khoản (resolve cho service khác).</summary>
public record AccountLookupItem(Guid AccountId, string Email);

/// <summary>Yêu cầu tìm AccountId theo email (Email chứa keyword).</summary>
public record AccountSearchRequest(string Keyword);
