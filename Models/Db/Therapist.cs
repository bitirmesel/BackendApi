using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DktApi.Models.Db;

[Table("therapists")]
public class Therapist
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("password")]
    public string Password { get; set; } = string.Empty; // login için

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

    // İleride ilişki kurarsan açarsın:
    // public ICollection<Player> Players { get; set; } = new List<Player>();
}
