using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DktApi.Models.Db;

[Table("therapists")]
public class Therapist
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("username")]
    public string? Username { get; set; }

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    // Şifre hiçbir API yanıtında JSON'a yazılmaz (entity doğrudan dönülse bile)
    // Eski düz metin şifre; yalnızca okunur ve hash'e taşınır (PR-13). Yeni şifre buraya yazılmaz.
    [JsonIgnore]
    [Column("password")]
    public string? Password { get; set; }

    /// <summary>ASP.NET Core PasswordHasher çıktısı (PR-13). Bkz. PasswordService.</summary>
    [JsonIgnore]
    [Column("password_hash")]
    public string? PasswordHash { get; set; }

    [Column("phone_number")]
    public string? PhoneNumber { get; set; }

    [Column("license_number")]
    public string? LicenseNumber { get; set; }

    [Column("profile_image_url")]
    public string? ProfileImageUrl { get; set; }

    [Column("clinic_name")]
    public string? ClinicName { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public ICollection<TherapistClient> TherapistClients { get; set; } = new List<TherapistClient>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}
