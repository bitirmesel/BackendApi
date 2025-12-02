using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("players")]
public class Player
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("nickname")]
    [MaxLength(50)]
    public string Nickname { get; set; } = string.Empty;

    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("email")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Column("password")]
    [MaxLength(255)]
    public string Password { get; set; } = string.Empty;

    [Column("birth_date")]
    public DateTime? BirthDate { get; set; }

    [Column("gender")]
    [MaxLength(50)]
    public string? Gender { get; set; }

    [Column("diagnosis")]
    [MaxLength(100)]
    public string? Diagnosis { get; set; }

    [Column("parent_name")]
    [MaxLength(100)]
    public string? ParentName { get; set; }

    [Column("parent_phone")]
    [MaxLength(20)]
    public string? ParentPhone { get; set; }

    [Column("city")]
    [MaxLength(150)]
    public string? City { get; set; }

    [Column("school_name")]
    [MaxLength(150)]
    public string? SchoolName { get; set; }

    [Column("abouts")]
    public string? Abouts { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("total_score")]
    public int? TotalScore { get; set; }

    [Column("last_login")]
    public DateTime? LastLogin { get; set; }

    // İlişkiler
    public ICollection<TherapistClient> TherapistClients { get; set; } = new List<TherapistClient>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
}
