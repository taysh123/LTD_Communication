using System.ComponentModel.DataAnnotations;
using LTD_Communication.Secure.Models;

namespace LTD_Communication.Secure.Models.ViewModels;

public class AddCustomerViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [Display(Name = "Full Name")]
    [StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [Display(Name = "Sector")]
    public int? SectorId { get; set; }

    [Display(Name = "Internet Package")]
    public int? PackageId { get; set; }

    public List<Sector> Sectors { get; set; } = new();
    public List<InternetPackage> Packages { get; set; } = new();
}
