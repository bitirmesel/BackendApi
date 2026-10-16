using DktApi.Models.Db;
using DktApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Tests;

public class PasswordServiceTests
{
    private readonly PasswordService _passwords = new();

    [Fact]
    public void Hash_duz_metin_icermez_ve_dogrulanir()
    {
        var hash = _passwords.Hash("gizli123");

        Assert.DoesNotContain("gizli123", hash);
        Assert.Equal(PasswordCheckResult.Succeeded, _passwords.Check(hash, null, "gizli123"));
        Assert.Equal(PasswordCheckResult.Failed, _passwords.Check(hash, null, "yanlis"));
    }

    [Fact]
    public void Ayni_sifre_her_seferinde_farkli_hash_uretir()
    {
        Assert.NotEqual(_passwords.Hash("gizli123"), _passwords.Hash("gizli123"));
    }

    [Fact]
    public void Hash_yoksa_eski_duz_metin_dogrulanir_ve_yukseltme_ister()
    {
        Assert.Equal(PasswordCheckResult.SucceededNeedsUpgrade, _passwords.Check(null, "eski123", "eski123"));
        Assert.Equal(PasswordCheckResult.Failed, _passwords.Check(null, "eski123", "baska"));
    }

    [Fact]
    public void Hash_varsa_eski_duz_metin_artik_gecmez()
    {
        // Şifre değiştirildi: hash yeni şifrenin, düz metin sütununda eski şifre kalmış olabilir
        var hash = _passwords.Hash("yeni123");

        Assert.Equal(PasswordCheckResult.Failed, _passwords.Check(hash, "eski123", "eski123"));
        Assert.Equal(PasswordCheckResult.Succeeded, _passwords.Check(hash, "eski123", "yeni123"));
    }

    [Theory]
    [InlineData(null, null, "x")]   // şifresiz hesap (B24)
    [InlineData(null, "", "")]      // boş düz metin + boş girdi
    [InlineData(null, null, null)]
    [InlineData(null, "abc", "")]
    public void Bos_ya_da_sifresiz_hesap_giris_yapamaz(string? hash, string? legacy, string? input)
    {
        Assert.Equal(PasswordCheckResult.Failed, _passwords.Check(hash, legacy, input));
    }

    [Fact]
    public void Bozuk_hash_hata_firlatmaz_basarisiz_doner()
    {
        Assert.Equal(PasswordCheckResult.Failed, _passwords.Check("bu-bir-hash-degil", "x", "x"));
    }
}

public class PasswordBackfillTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PasswordService _passwords = new();

    public PasswordBackfillTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = CreateDb();
        db.Database.EnsureCreated();
    }

    private AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Duz_metinleri_hashler_digerlerine_dokunmaz_ve_idempotent()
    {
        var existingHash = _passwords.Hash("zatenhash");
        using (var db = CreateDb())
        {
            db.Players.AddRange(
                new Player { Name = "A", Nickname = "a", Password = "sifreA" },
                new Player { Name = "B", Nickname = "b", Password = null },          // şifresiz
                new Player { Name = "C", Nickname = "c", Password = "" },            // boş
                new Player { Name = "D", Nickname = "d", PasswordHash = existingHash });
            db.Therapists.Add(new Therapist { Name = "T", Email = "t@x.com", Password = "sifreT" });
            await db.SaveChangesAsync();
        }

        using (var db = CreateDb())
        {
            var (players, therapists) = await PasswordBackfill.RunAsync(db, _passwords);
            Assert.Equal(1, players);
            Assert.Equal(1, therapists);
        }

        using (var db = CreateDb())
        {
            var a = await db.Players.SingleAsync(p => p.Nickname == "a");
            Assert.Equal(PasswordCheckResult.Succeeded, _passwords.Check(a.PasswordHash, null, "sifreA"));
            Assert.Equal("sifreA", a.Password); // düz metin bu aşamada bilerek korunur (8nfn)

            Assert.Null((await db.Players.SingleAsync(p => p.Nickname == "b")).PasswordHash);
            Assert.Null((await db.Players.SingleAsync(p => p.Nickname == "c")).PasswordHash);
            Assert.Equal(existingHash, (await db.Players.SingleAsync(p => p.Nickname == "d")).PasswordHash);

            var t = await db.Therapists.SingleAsync();
            Assert.Equal(PasswordCheckResult.Succeeded, _passwords.Check(t.PasswordHash, null, "sifreT"));

            // İkinci çalıştırma hiçbir şeye dokunmaz
            var again = await PasswordBackfill.RunAsync(db, _passwords);
            Assert.Equal((0, 0), again);
        }
    }

    [Fact]
    public async Task Sifre_degistiyse_backfill_yeni_hashi_ezmez()
    {
        long id;
        using (var db = CreateDb())
        {
            var p = new Player { Name = "E", Nickname = "e", Password = "eski" };
            db.Players.Add(p);
            await db.SaveChangesAsync();
            id = p.Id;
        }

        // Backfill okuduktan sonra kullanıcı şifresini değiştirmiş gibi: koşullu yazma eşleşmemeli
        var newHash = _passwords.Hash("yeni");
        using (var db = CreateDb())
        {
            await db.Players.Where(x => x.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PasswordHash, newHash).SetProperty(x => x.Password, (string?)null));
            var (players, _) = await PasswordBackfill.RunAsync(db, _passwords);
            Assert.Equal(0, players);
            var e = await db.Players.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(newHash, e.PasswordHash);
        }
    }
}
