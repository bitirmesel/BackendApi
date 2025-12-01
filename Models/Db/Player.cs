namespace DktApi.Models.Db;

public class Player
{
    public int Id { get; set; }
    public int AdvisorId { get; set; }
    public Therapist? Advisor { get; set; }

    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }

    // "Başlangıç" / "Orta" / "İleri"
    public string Level { get; set; } = "Başlangıç";

    public int Score { get; set; } // 0 – 120 arası
    public DateTime LastActive { get; set; } = DateTime.UtcNow;

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();
    public ICollection<Badge> Badges { get; set; } = new List<Badge>();
    public ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
}
