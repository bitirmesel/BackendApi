using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("feedback")]
public class Feedback
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("therapist_id")]
    public long TherapistId { get; set; }

    [Column("game_session_id")]
    public long GameSessionId { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    [Column("rating")]
    public int? Rating { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    public Therapist Therapist { get; set; } = null!;
    public GameSession GameSession { get; set; } = null!;
}
