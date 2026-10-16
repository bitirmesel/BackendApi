using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace DktApi.Services;

public enum PasswordCheckResult
{
    Failed,
    Succeeded,

    /// <summary>
    /// Doğru şifre, ama kayıt güncellenmeli: düz metinden geldi ya da hash eski parametrelerle üretilmiş.
    /// Çağıran <see cref="PasswordService.Hash"/> ile yeni hash'i yazmalı.
    /// </summary>
    SucceededNeedsUpgrade
}

/// <summary>
/// Şifre hash'leme (PR-13, docs/design/faz3-kimlik-ve-yetki.md §4.1).
/// ASP.NET Core <see cref="PasswordHasher{TUser}"/>: .NET 8'de PBKDF2-HMAC-SHA512, sürüm bilgisi hash'in içinde.
/// Geçiş döneminde hash'i olmayan kayıtlar eski düz metin sütunuyla doğrulanır ve hash'e yükseltilir.
/// </summary>
public sealed class PasswordService
{
    // PasswordHasher kullanıcı nesnesini kullanmıyor; tek bir hasher iki tablo için de yeterli
    private static readonly object HashUser = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(HashUser, password);

    /// <summary>
    /// <paramref name="input"/> şifresini kaydın hash'i (varsa) ya da eski düz metniyle doğrular.
    /// Hash varsa düz metne hiç bakılmaz: şifre değiştikten sonra eski düz metin geçerli sayılmasın.
    /// </summary>
    public PasswordCheckResult Check(string? passwordHash, string? legacyPlaintext, string? input)
    {
        if (string.IsNullOrEmpty(input))
            return PasswordCheckResult.Failed;

        if (!string.IsNullOrEmpty(passwordHash))
        {
            PasswordVerificationResult result;
            try
            {
                result = _hasher.VerifyHashedPassword(HashUser, passwordHash, input);
            }
            catch (FormatException)
            {
                // Bozuk hash: giriş başarısız, düz metne düşülmez
                return PasswordCheckResult.Failed;
            }

            return result switch
            {
                PasswordVerificationResult.Success => PasswordCheckResult.Succeeded,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheckResult.SucceededNeedsUpgrade,
                _ => PasswordCheckResult.Failed
            };
        }

        if (string.IsNullOrEmpty(legacyPlaintext))
            return PasswordCheckResult.Failed;

        // Sabit süreli karşılaştırma (zamanlama saldırısına karşı)
        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(legacyPlaintext),
            Encoding.UTF8.GetBytes(input));

        return matches ? PasswordCheckResult.SucceededNeedsUpgrade : PasswordCheckResult.Failed;
    }

    public static bool IsSuccess(PasswordCheckResult result) => result != PasswordCheckResult.Failed;
}
