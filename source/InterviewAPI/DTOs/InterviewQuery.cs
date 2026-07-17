namespace InterviewAPI.DTOs;

/// <summary>
/// Filter and search parameters for the interview session list. 
/// All fields are optional (leaving them empty means no filter is applied).
/// The Keyword field is used to search within the Title.
/// </summary>
public class InterviewQuery
{
    // Search term for filtering by title
    public string? Keyword { get; set; }

    // Combined status from both local and remote branches, including 'Paused'
    public string? Status { get; set; }          // Configured / Initialized / InProgress / Paused / Completed / Expired / Cancelled
    
    // New property added from the remote dev branch
    public string? RoomStatus { get; set; }      // Active / Closed
    
    // Experience level filtering for the interview
    public string? Level { get; set; }           // Intern / Junior / Middle / Senior / Lead
    
    // Mode of interaction during the interview session
    public string? InteractionMode { get; set; } // Text / Voice / Video
    
    // Type of questions asked in the session
    public string? QuestionType { get; set; }    // Theory / Coding / Behavioral / Mixed
    
    // Unique identifier for the user taking the interview
    public Guid? UserId { get; set; }
    
    // Start date for filtering sessions
    public DateTime? DateFrom { get; set; }
    
    // End date for filtering sessions
    public DateTime? DateTo { get; set; }
    
    // true = include soft-deleted sessions in the list (for admin recovery + dashboard totals)
    public bool IncludeDeleted { get; set; } = false;

    // true = list ONLY soft-deleted sessions (admin "Show deleted" view). Takes precedence over IncludeDeleted.
    public bool OnlyDeleted { get; set; } = false;

    // Current page number for pagination, default is 1
    public int Page { get; set; } = 1;
    
    // Number of records per page, default is 20
    public int PageSize { get; set; } = 20;
}