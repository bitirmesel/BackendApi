using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("tasks")]
public class TaskItem
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("therapist_id")]
    public long TherapistId { get; set; }

    [Column("player_id")]
    public long PlayerId { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("letter_id")]
    public long LetterId { get; set; }

    [Column("asset_set_id")]
    public long? AssetSetId { get; set; } // opsiyonel demiştin

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "ASSIGNED";

    [Column("assigned_at")]
    public DateTime? AssignedAt { get; set; }

    [Column("due_at")]
    public DateTime? DueAt { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    public Therapist Therapist { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public Game Game { get; set; } = null!;
    public Letter Letter { get; set; } = null!;
    public AssetSet? AssetSet { get; set; }

    public ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
}
