namespace UserAPI.DTOs;

/// <summary>Yêu cầu tra cứu nhiều hồ sơ theo AccountId (gọi nội bộ từ service khác).</summary>
public record ProfileLookupRequest(List<Guid> AccountIds);

/// <summary>Thông tin tên hiển thị của một hồ sơ (resolve cho service khác).</summary>
public record ProfileLookupItem(Guid AccountId, string? FullName);

/// <summary>Yêu cầu tìm AccountId theo tên (FullName chứa keyword).</summary>
public record ProfileSearchRequest(string Keyword);

/// <summary>Yêu cầu xoá hồ sơ theo danh sách AccountId (AuthAPI gọi khi dọn account Pending hết hạn xác thực).</summary>
public record ProfileDeleteRequest(List<Guid> AccountIds);
