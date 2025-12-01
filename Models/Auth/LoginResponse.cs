namespace DktApi.Models.Auth;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public int TherapistId { get; set; }
    public string Name { get; set; } = string.Empty;
}
