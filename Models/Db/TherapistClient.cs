using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("therapist_clients")]
public class TherapistClient
{
    [Column("therapist_id")]
    public long TherapistId { get; set; }

    [Column("player_id")]
    public long PlayerId { get; set; }

    public Therapist Therapist { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
