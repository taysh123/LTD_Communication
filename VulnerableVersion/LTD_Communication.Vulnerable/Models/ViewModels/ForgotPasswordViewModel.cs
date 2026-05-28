using System.ComponentModel.DataAnnotations;

namespace LTD_Communication.Vulnerable.Models.ViewModels;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
