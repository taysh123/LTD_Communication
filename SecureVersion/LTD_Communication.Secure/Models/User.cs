namespace LTD_Communication.Secure.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public int FailedLoginAttempts { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
}
