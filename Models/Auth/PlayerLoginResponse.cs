namespace DktApi.Models.Auth;

public class PlayerLoginResponse
{
    public long PlayerId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public int? TotalScore { get; set; }
}