namespace ConstructionServiceUsers.Models;

public class UserRegistrationDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
}