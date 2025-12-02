using System.ComponentModel.DataAnnotations;

namespace DktApi.Models.Game;

// POST /api/gamesessions/start için istek modeli
public class CreateGameSessionReq
{
    // Oyun oturumu kimlik doğrulamadan geçiyorsa bu gerekli olmayabilir,
    // ancak şimdilik mevcut yapıyı koruyalım.
    [Required]
    public long PlayerId { get; set; }

    [Required]
    public long GameId { get; set; }
    
    // DB şemasında zorunlu olmasa da, oyunun temel bileşenleri için gerekli olabilir
    public long? LetterId { get; set; } 
    public long? AssetSetId { get; set; } 
    
    // Eğer oturum bir göreve bağlıysa
    public long? TaskId { get; set; } 
}