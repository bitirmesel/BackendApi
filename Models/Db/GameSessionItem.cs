using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("game_session_items")]
public class GameSessionItem
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_session_id")]
    public long GameSessionId { get; set; }

    [Column("order_no")]
    public int OrderNo { get; set; }

    [Column("item_type")]
    [MaxLength(30)]
    public string ItemType { get; set; } = "WORD";
    // WORD | SYLLABLE | SENTENCE | PHONEME | MATCH

    [Column("prompt_text")]
    public string PromptText { get; set; } = string.Empty;

    [Column("score")]
    public int? Score { get; set; }

    [Column("is_correct")]
    public bool? IsCorrect { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    public GameSession GameSession { get; set; } = null!;
}