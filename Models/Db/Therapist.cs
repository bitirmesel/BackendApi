namespace DktApi.Models.Db;

public class Therapist
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    public ICollection<Player> Players { get; set; } = new List<Player>();
}
