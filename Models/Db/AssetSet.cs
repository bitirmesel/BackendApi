using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("asset_sets")]
public class AssetSet
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("letter_id")]
    public long LetterId { get; set; }

    [Column("asset_json")]
    public string AssetJson { get; set; } // JSON veya path

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    public Game Game { get; set; } = null!;
    public Letter Letter { get; set; } = null!;

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
}



