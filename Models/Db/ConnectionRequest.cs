using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("connection_requests")]
public class ConnectionRequest
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("player_id")]
    public long PlayerId { get; set; }

    [Column("therapist_id")]
    public long TherapistId { get; set; }

    [Column("invitation_id")]
    public long InvitationId { get; set; }

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "PENDING";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("responded_at")]
    public DateTime? RespondedAt { get; set; }

    // Navigation Properties
    public Player Player { get; set; } = null!;
    public Therapist Therapist { get; set; } = null!;
    public InvitationCode Invitation { get; set; } = null!;
}
