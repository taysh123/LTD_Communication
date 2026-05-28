using System.ComponentModel.DataAnnotations;
using LTD_Communication.Vulnerable.Models;

namespace LTD_Communication.Vulnerable.Models.ViewModels;

public class AddCustomerViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }
    public string? Address { get; set; }

    [Display(Name = "Sector")]
    public int? SectorId { get; set; }

    [Display(Name = "Internet Package")]
    public int? PackageId { get; set; }

    public List<Sector> Sectors { get; set; } = new();
    public List<InternetPackage> Packages { get; set; } = new();
}
