namespace ConstructionServiceUsers.Models;

public class User
{
    public Guid UUID { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public DateTime CreatedDate { get; set; }
    public DateTime LastModifiedDate { get; set; }
}