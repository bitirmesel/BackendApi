namespace DktApi.Models.Auth;

public class LoginRequest
{
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string Password { get; set; } = string.Empty;
}
