using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Services;

/// <summary>
/// Hash'i olmayan kayıtların eski düz metin şifresini hash'ler (PR-13).
/// İdempotent: yalnızca <c>password_hash IS NULL AND password &lt;&gt; ''</c> satırlarına dokunur.
///
/// Düz metin sütunu bu aşamada BİLEREK silinmez: eski <c>8nfn</c> servisi aynı veritabanını
/// düz metinle okuyor. Hash'le girişler canlıda doğrulandıktan sonra ayrı bir PR temizler.
/// </summary>
public static class PasswordBackfill
{
    public static async Task<(int players, int therapists)> RunAsync(
        AppDbContext db, PasswordService passwords, CancellationToken ct = default)
    {
        var players = await db.Players.AsNoTracking()
            .Where(p => p.PasswordHash == null && p.Password != null && p.Password != "")
            .Select(p => new { p.Id, p.Password })
            .ToListAsync(ct);
        var playerCount = 0;
        foreach (var p in players)
        {
            var hash = HashVerified(passwords, p.Password!);
            // Koşullu yazma: hash'leme sürerken şifre değiştiyse (ya da giriş hash'i yazdıysa) dokunma
            playerCount += await db.Players
                .Where(x => x.Id == p.Id && x.PasswordHash == null && x.Password == p.Password)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PasswordHash, hash), ct);
        }

        var therapists = await db.Therapists.AsNoTracking()
            .Where(t => t.PasswordHash == null && t.Password != null && t.Password != "")
            .Select(t => new { t.Id, t.Password })
            .ToListAsync(ct);
        var therapistCount = 0;
        foreach (var t in therapists)
        {
            var hash = HashVerified(passwords, t.Password!);
            therapistCount += await db.Therapists
                .Where(x => x.Id == t.Id && x.PasswordHash == null && x.Password == t.Password)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PasswordHash, hash), ct);
        }

        return (playerCount, therapistCount);
    }

    /// <summary>Yazmadan önce hash'in gerçekten doğrulandığından emin olur.</summary>
    private static string HashVerified(PasswordService passwords, string plaintext)
    {
        var hash = passwords.Hash(plaintext);
        if (passwords.Check(hash, null, plaintext) == PasswordCheckResult.Failed)
            throw new InvalidOperationException("Üretilen şifre hash'i doğrulanamadı.");
        return hash;
    }
}

/// <summary>
/// Backfill'i uygulama açıldıktan sonra arka planda çalıştırır. PBKDF2 yavaş olduğu için
/// açılışı (Render port bekleme süresi) geciktirmemesi gerekiyor.
/// </summary>
public sealed class PasswordBackfillService(IServiceScopeFactory scopes) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var passwords = scope.ServiceProvider.GetRequiredService<PasswordService>();
            var (players, therapists) = await PasswordBackfill.RunAsync(db, passwords, stoppingToken);
            Console.WriteLine($"[PASSWORD] Backfill tamam: {players} oyuncu, {therapists} terapist hash'lendi.");
        }
        catch (Exception ex)
        {
            ErrorLog.Log("PasswordBackfill", ex);
        }
    }
}
