namespace UserAPI.DTOs;

/// <summary>Yêu cầu tra cứu nhiều hồ sơ theo AccountId (gọi nội bộ từ service khác).</summary>
public record ProfileLookupRequest(List<Guid> AccountIds);

/// <summary>Thông tin tên hiển thị của một hồ sơ (resolve cho service khác).</summary>
public record ProfileLookupItem(Guid AccountId, string? FullName);
