using System.ComponentModel.DataAnnotations;

namespace DktApi.Models.Game;
// POST /api/gamesessions/finish için istek modeli
public class FinishGameSessionReq
{
    [Required]
    public long SessionId { get; set; }

    [Required]
    public int Score { get; set; }
    
    public int? MaxScore { get; set; } 
    
    public int? DurationSec { get; set; }

    // --- ANALİZ İÇİN EKLE ---
    public string? TargetWord { get; set; } // Hangi kelime çalışıldı? (Örn: "kedi")
}