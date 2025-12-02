using System.ComponentModel.DataAnnotations;

namespace DktApi.Models.Game;
// POST /api/gamesessions/finish için istek modeli
public class FinishGameSessionReq
{
    [Required]
    public long SessionId { get; set; }

    [Required]
    public int Score { get; set; }
    
    // DB'deki max_score alanı için (Eğer frontend gönderiyorsa)
    public int? MaxScore { get; set; } 
    
    // Oturumun süresi (DB'de duration_sec int)
    public int? DurationSec { get; set; }
}