using System;
using System.Collections.Generic;

namespace WebClient.Models.Manager;

public class UserListItemViewModel
{
    public Guid UserId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
    public DateTime RegistrationTimestamp { get; set; }
    public int TotalInterviewCount { get; set; }
}

public class UserDetailsViewModel
{
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsFaceVerified { get; set; }
    public bool IsVoiceVerified { get; set; }
    public DateTime RegistrationTimestamp { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? BlockedReason { get; set; }
    public DateTime? BlockedAt { get; set; }
    public int CreditBalance { get; set; }
    public List<CreditTransactionViewModel> CreditTransactions { get; set; } = new();
    public List<CompletedSessionViewModel> CompletedSessions { get; set; } = new();
}

public class CreditTransactionViewModel
{
    public Guid LedgerId { get; set; }
    public string? EntryType { get; set; }
    public int CreditChange { get; set; }
    public int BalanceAfter { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CompletedSessionViewModel
{
    public Guid SessionId { get; set; }
    public string? Title { get; set; }
    public string? Level { get; set; }
    public string? InteractionMode { get; set; }
    public string? QuestionType { get; set; }
    public string? Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public int? DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    public int CreditCost { get; set; }
}
