namespace DktApi.Models.Db;

public class GameSession
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public int GameId { get; set; }
    public Game? Game { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    public int Score { get; set; }
}
