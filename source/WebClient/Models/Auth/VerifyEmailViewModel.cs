using System.ComponentModel.DataAnnotations;

namespace WebClient.Models.Auth;

public class VerifyEmailViewModel
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the 6-digit code.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "The verification code must be 6 digits.")]
    public string Otp { get; set; } = string.Empty;
}
