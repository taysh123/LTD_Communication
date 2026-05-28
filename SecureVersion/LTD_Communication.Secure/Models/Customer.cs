namespace LTD_Communication.Secure.Models;

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int? SectorId { get; set; }
    public int? PackageId { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? SectorName { get; set; }
    public string? PackageName { get; set; }
}
