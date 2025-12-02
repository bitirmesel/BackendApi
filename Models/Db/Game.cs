using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("games")]
public class Game
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_type_id")]
    public long GameTypeId { get; set; }

    [Column("difficulty_level_id")]
    public long DifficultyLevelId { get; set; }

    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    public GameType GameType { get; set; } = null!;
    public DifficultyLevel DifficultyLevel { get; set; } = null!;

    public ICollection<AssetSet> AssetSets { get; set; } = new List<AssetSet>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
}
