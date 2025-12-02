using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("game_types")]
public class GameType
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("code")]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty; // SYLLABLE, WORD, SENTENCE

    [Column("name")]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    public ICollection<Game> Games { get; set; } = new List<Game>();
}
