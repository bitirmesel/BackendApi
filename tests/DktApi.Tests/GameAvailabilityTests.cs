using DktApi.Services;

namespace DktApi.Tests;

public class GameAvailabilityTests
{
    [Fact]
    public void FillGap_atanamaz()
    {
        Assert.False(GameAvailability.IsPlayable(9));
        Assert.Equal("game_not_playable",
            GameAvailability.ValidateAssignment(9, gameExists: true, letterExists: true, hasAssetSet: true));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    public void Asset_isteyen_oyun_icerik_yoksa_atanamaz(long gameId)
    {
        Assert.Equal("no_content_for_letter",
            GameAvailability.ValidateAssignment(gameId, gameExists: true, letterExists: true, hasAssetSet: false));
        Assert.Null(
            GameAvailability.ValidateAssignment(gameId, gameExists: true, letterExists: true, hasAssetSet: true));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    public void Paketli_icerikli_oyun_asset_set_istemez(long gameId)
    {
        Assert.False(GameAvailability.RequiresAssetSet(gameId));
        Assert.Null(
            GameAvailability.ValidateAssignment(gameId, gameExists: true, letterExists: true, hasAssetSet: false));
    }

    [Fact]
    public void Olmayan_oyun_ve_harf_reddedilir()
    {
        Assert.Equal("game_not_found",
            GameAvailability.ValidateAssignment(99, gameExists: false, letterExists: true, hasAssetSet: false));
        Assert.Equal("letter_not_found",
            GameAvailability.ValidateAssignment(1, gameExists: true, letterExists: false, hasAssetSet: false));
    }

    [Fact]
    public void Her_hata_kodunun_aciklamasi_var()
    {
        foreach (var code in new[] { "game_not_found", "letter_not_found", "game_not_playable", "no_content_for_letter" })
            Assert.NotEqual("Görev atanamadı.", GameAvailability.Describe(code));
    }
}
