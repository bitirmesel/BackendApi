namespace DktApi.Models.Db;

public class Badge
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public string Title { get; set; } = string.Empty; // Örn: "Süper Gayret"
    public string Icon { get; set; } = "star";        // star / trophy / thumb_up / school
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
