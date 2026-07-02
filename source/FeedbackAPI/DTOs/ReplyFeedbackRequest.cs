using System.ComponentModel.DataAnnotations;

namespace FeedbackAPI.DTOs;

/// <summary>Nội dung admin trả lời một System feedback.</summary>
public class ReplyFeedbackRequest
{
    [Required(ErrorMessage = "Reply content is required.")]
    [StringLength(4000, MinimumLength = 1)]
    public string ReplyContent { get; set; } = null!;
}
