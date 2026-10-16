using System.Globalization;
using DktApi.Models.Db;
using DktApi.Services;

namespace DktApi.Tests;

public class PlayerLoginResolverTests
{
    private readonly PasswordService _passwords = new();

    [Theory]
    [InlineData("AliD", "alid")]
    [InlineData("  aliD ", "alid")]
    [InlineData(null, "")]
    public void Kullanici_adi_normalize_edilir(string? input, string expected)
    {
        Assert.Equal(expected, PlayerLoginResolver.NormalizeNickname(input));
    }

    [Fact]
    public void Turkce_kulturde_I_noktasiz_i_olmaz()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Assert.Equal("irmak", PlayerLoginResolver.NormalizeNickname("IRMAK"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Ayni_adli_kayitlardan_sifresi_tutan_secilir()
    {
        // Canlıdaki durum: üç ayrı "test" hesabı
        var candidates = new[]
        {
            new Player { Id = 10, Nickname = "test", PasswordHash = _passwords.Hash("onuncu") },
            new Player { Id = 6, Nickname = "test", Password = "altinci" },
            new Player { Id = 9, Nickname = "test", Password = null },
        };

        var (player, result) = PlayerLoginResolver.Resolve(candidates, "onuncu", _passwords);
        Assert.Equal(10, player?.Id);
        Assert.Equal(PasswordCheckResult.Succeeded, result);

        (player, result) = PlayerLoginResolver.Resolve(candidates, "altinci", _passwords);
        Assert.Equal(6, player?.Id);
        Assert.Equal(PasswordCheckResult.SucceededNeedsUpgrade, result);

        (player, _) = PlayerLoginResolver.Resolve(candidates, "yanlis", _passwords);
        Assert.Null(player);
    }

    [Fact]
    public void Ayni_sifre_birden_fazla_kayitta_tutarsa_en_kucuk_ID_secilir()
    {
        var candidates = new[]
        {
            new Player { Id = 9, Nickname = "test", Password = "ortak" },
            new Player { Id = 6, Nickname = "Test", Password = "ortak" },
        };

        var (player, _) = PlayerLoginResolver.Resolve(candidates, "ortak", _passwords);
        Assert.Equal(6, player?.Id);
    }

    [Fact]
    public void Aday_yoksa_basarisiz()
    {
        var (player, result) = PlayerLoginResolver.Resolve(Array.Empty<Player>(), "x", _passwords);
        Assert.Null(player);
        Assert.Equal(PasswordCheckResult.Failed, result);
    }
}
