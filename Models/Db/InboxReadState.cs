using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("inbox_read_states")]
public class InboxReadState
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("player_id")]
    public long PlayerId { get; set; }

    [Required]
    [MaxLength(30)]
    [Column("source_type")]
    public string SourceType { get; set; } = string.Empty; // task | feedback | notification

    [Column("source_id")]
    public long SourceId { get; set; }

    [Column("read_at")]
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    public Player? Player { get; set; }
}