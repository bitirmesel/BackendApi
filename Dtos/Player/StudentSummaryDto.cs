using System.ComponentModel.DataAnnotations;

namespace DktApi.Models.Player;

// GET /api/students listelemesi için kullanılacak DTO yapısı
public class StudentSummaryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? TotalScore { get; set; } // Player modelindeki ad
    public DateTime? LastLogin { get; set; } // Player modelindeki ad
    public int ActiveTasksCount { get; set; }
    
    // Eğer DB'de yoksa, zorunlu olmayan (null) alan olarak bırakıldı
    public long? AdvisorId { get; set; } 
    public int? Level { get; set; } 
}