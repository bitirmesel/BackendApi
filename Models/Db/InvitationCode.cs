using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("invitation_codes")]
public class InvitationCode
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("code")]
    [MaxLength(8)]
    public string Code { get; set; } = string.Empty;

    [Column("therapist_id")]
    public long TherapistId { get; set; }

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "PENDING";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    [Column("used_by_player_id")]
    public long? UsedByPlayerId { get; set; }

    // Navigation Properties
    public Therapist Therapist { get; set; } = null!;
    public Player? UsedByPlayer { get; set; }
    public ICollection<ConnectionRequest> ConnectionRequests { get; set; } = new List<ConnectionRequest>();
}
