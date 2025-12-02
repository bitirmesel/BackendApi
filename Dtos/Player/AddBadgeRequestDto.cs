using System.ComponentModel.DataAnnotations;

namespace DktApi.Models.Player;

// POST /api/students/{id}/badges için istek modeli
public class AddBadgeRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;
    
    // Rozet ikonu için (örneğin: "star", "trophy")
    [Required] 
    public string Icon { get; set; } = string.Empty;
}
