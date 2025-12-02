namespace DktApi.Models.Auth;

// Giriş ve Kayıt sonrası dönülecek yanıt yapısı
public class AuthResponse
{
    public string Token { get; set; } = "demo-token"; // JWT için yer tutucu
    public long TherapistId { get; set; } // DB'deki bigserial/bigint ile uyumlu
    public string Name { get; set; } = string.Empty;
}