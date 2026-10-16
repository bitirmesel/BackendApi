using DktApi.Models.Db;

namespace DktApi.Services;

/// <summary>
/// Çocuk girişi: kullanıcı adı büyük/küçük harfe duyarsız eşleşir.
/// Aynı kullanıcı adını taşıyan birden fazla eski kayıt olabilir (ör. üç ayrı "test");
/// sırasız <c>FirstOrDefault</c> her seferinde farklı satırı getirip girişi rastgele bozuyordu.
/// Adaylar ID sırasıyla denenir, şifresi tutan ilk kayıt seçilir.
/// </summary>
public static class PlayerLoginResolver
{
    /// <summary>Kırpar, küçük harfe çevirir (invariant: Türkçe kültürde "I" → "ı" olmasın).</summary>
    public static string NormalizeNickname(string? nickname) =>
        nickname?.Trim().ToLowerInvariant() ?? string.Empty;

    public static (Player? player, PasswordCheckResult result) Resolve(
        IEnumerable<Player> candidates, string? password, PasswordService passwords)
    {
        foreach (var candidate in candidates.OrderBy(p => p.Id))
        {
            var result = passwords.Check(candidate.PasswordHash, candidate.Password, password);
            if (PasswordService.IsSuccess(result))
                return (candidate, result);
        }

        return (null, PasswordCheckResult.Failed);
    }
}
