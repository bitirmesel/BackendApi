using System.ComponentModel.DataAnnotations;

namespace DktApi.Models.Game;
// POST /api/gamesessions/finish için istek modeli
public class FinishGameSessionReq
{
    public long SessionId { get; set; } // Unity'den gelen grup ID'si
    public long PlayerId { get; set; }  // Kırmızı hatayı çözer
    public long GameId { get; set; }    // Kırmızı hatayı çözer
    public long LetterId { get; set; }  // Kırmızı hatayı çözer
    public int Score { get; set; }
    public string? TargetWord { get; set; } // "Kedi", "Köpek" bilgisi
    public int? MaxScore { get; set; }
}