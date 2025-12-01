namespace DktApi.Models.Db;

public class Game
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;       // Örn: "Eşleştirme Kart"
    public string Code { get; set; } = string.Empty;       // Örn: "match_cards"
    public string Difficulty { get; set; } = "Kolay";      // Kolay / Orta / Zor

    public ICollection<GameSession> Sessions { get; set; } = new List<GameSession>();
}
