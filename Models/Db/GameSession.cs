using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("game_sessions")]
public class GameSession
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("player_id")]
    public long PlayerId { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("letter_id")]
    public long LetterId { get; set; }

    [Column("asset_set_id")]
    public long AssetSetId { get; set; }

    [Column("task_id")]
    public long? TaskId { get; set; }

    [Column("score")]
    public int Score { get; set; }

    [Column("max_score")]
    public int MaxScore { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("finished_at")]
    public DateTime? FinishedAt { get; set; }

    [Column("duration_sec")]
    public int? DurationSec { get; set; }

    [Column("target_word")]
    public string? TargetWord { get; set; }

    public Player Player { get; set; } = null!;
    public Game Game { get; set; } = null!;
    public Letter Letter { get; set; } = null!;
    public AssetSet AssetSet { get; set; } = null!;
    public TaskItem? Task { get; set; }

    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}
