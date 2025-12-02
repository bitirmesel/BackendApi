using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("letters")]
public class Letter
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("code")]
    [MaxLength(5)]
    public string Code { get; set; } = string.Empty; // 'A','B',...

    [Column("display_name")]
    [MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    public ICollection<AssetSet> AssetSets { get; set; } = new List<AssetSet>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
}
