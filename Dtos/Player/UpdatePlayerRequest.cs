using System.ComponentModel.DataAnnotations;

namespace DktApi.Dtos.Player;

/// <summary>
/// PATCH — gönderilen alanlar güncellenir; gönderilmeyenler aynı kalır.
/// </summary>
public class UpdatePlayerRequest
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(50)]
    public string? Nickname { get; set; }

    [EmailAddress]
    [MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(255)]
    public string? Password { get; set; }

    /// <summary>
    /// Sadece değer gönderildiğinde güncellenir; JSON'da alanı hiç göndermemek = değiştirme.
    /// </summary>
    public DateTime? BirthDate { get; set; }

    [MaxLength(50)]
    public string? Gender { get; set; }

    [MaxLength(100)]
    public string? Diagnosis { get; set; }

    [MaxLength(100)]
    public string? ParentName { get; set; }

    [MaxLength(20)]
    public string? ParentPhone { get; set; }

    [MaxLength(150)]
    public string? City { get; set; }

    [MaxLength(150)]
    public string? SchoolName { get; set; }

    public string? Abouts { get; set; }
}
