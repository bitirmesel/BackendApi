using System.ComponentModel.DataAnnotations;

namespace DktApi.Models.Auth;

// Therapist kaydı için Flutter'dan gelen verileri tutar
public class RegisterRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    // Flutter tarafında 'institution' olarak adlandırılan alan
    public string? ClinicName { get; set; }
}