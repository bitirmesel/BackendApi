using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("difficulty_levels")]
public class DifficultyLevel
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("level")]
    public int Level { get; set; } // 1,2,3

    [Column("name")]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public ICollection<Game> Games { get; set; } = new List<Game>();
}
