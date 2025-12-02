using System.ComponentModel.DataAnnotations;

namespace DktApi.Models.Player;

// POST /api/students/{id}/notes için istek modeli
public class AddNoteRequest
{
    [Required]
    public string Text { get; set; } = string.Empty;
}

