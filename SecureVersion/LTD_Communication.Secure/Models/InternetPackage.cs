namespace LTD_Communication.Secure.Models;

public class InternetPackage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
}
