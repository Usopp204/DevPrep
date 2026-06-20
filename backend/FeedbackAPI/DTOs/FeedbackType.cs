namespace FeedbackAPI.DTOs;

/// <summary>
/// Loại feedback dùng để lọc:
///   - System: feedback hệ thống (bảng SystemFeedbacks).
///   - AI: feedback về buổi phỏng vấn AI (bảng InterviewFeedbacks).
/// </summary>
public enum FeedbackType
{
    System,
    AI
}
