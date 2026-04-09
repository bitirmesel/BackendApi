using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("notifications")]
public class Notification
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("player_id")]
    public long PlayerId { get; set; }

    [Column("therapist_id")]
    public long TherapistId { get; set; }

    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("is_read")]
    public bool IsRead { get; set; } = false;

    // İlişkiler (Navigation Properties)
    public Player Player { get; set; } = null!;
    public Therapist Therapist { get; set; } = null!;
}