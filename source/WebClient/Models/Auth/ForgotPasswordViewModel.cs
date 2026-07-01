using System.ComponentModel.DataAnnotations;

namespace WebClient.Models.Auth;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not a valid email address.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;
}
