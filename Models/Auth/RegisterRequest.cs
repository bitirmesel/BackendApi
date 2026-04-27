using System.ComponentModel.DataAnnotations;

namespace DktApi.Models.Auth;

// Therapist kaydı için Flutter'dan gelen verileri tutar
public class RegisterRequest
{
    [Required(ErrorMessage = "Ad Soyad zorunludur")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kullanıcı adı zorunludur")]
    [MinLength(3, ErrorMessage = "Kullanıcı adı en az 3 karakter olmalıdır")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur")]
    [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre doğrulama zorunludur")]
    public string VerifyPassword { get; set; } = string.Empty;

    // Flutter tarafında 'institution' olarak adlandırılan alan
    public string? ClinicName { get; set; }
}