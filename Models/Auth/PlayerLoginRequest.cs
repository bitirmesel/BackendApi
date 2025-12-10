namespace DktApi.Models.Auth;

public class PlayerLoginRequest
{
    public string Nickname { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}