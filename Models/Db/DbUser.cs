namespace DktApi.Models.Db;

public class DbUser
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // DEMO icin plain text de tutulabilir
    public string Role { get; set; } = "therapist";

    public int? TherapistId { get; set; }
    public Therapist? Therapist { get; set; }
}
