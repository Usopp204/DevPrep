namespace UserAPI.DTOs;

// DTO nhận từ AuthAPI (Port 7001)
public class InternalAccountDto
{
    public Guid AccountId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public bool IsFaceVerified { get; set; }
    public bool IsVoiceVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? BlockedReason { get; set; }
    public DateTime? BlockedAt { get; set; }
    public Guid? BlockedBy { get; set; }
}

// DTO nhận từ InterviewAPI (Port 7005)
public class InternalSessionSummaryDto
{
    public Guid UserId { get; set; }
    public int TotalSessions { get; set; }
}

// DTO nhận từ InterviewAPI — trạng thái enroll sinh trắc học, nguồn thật thay cho
// Accounts.IsFaceVerified/IsVoiceVerified bên AuthAPI (hai cột đó không bao giờ được ghi).
public class InternalBiometricStatusDto
{
    public bool FaceEnrolled { get; set; }
    public bool VoiceEnrolled { get; set; }
}

// DTO nhận từ PaymentAPI (Port 7008)
public class InternalWalletDto
{
    public Guid UserId { get; set; }
    public int Balance { get; set; }
}