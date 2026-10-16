using DktApi.Services;

namespace DktApi.Tests;

/// <summary>
/// PronunciationScoringService kuralları (ARCHITECTURE.md §6.7). Bu testler bugünkü davranışı belgeler;
/// "Karar" ile başlayan testler ekip kararı bekleyen davranışlardır (ROADMAP Backlog B16).
/// </summary>
public class PronunciationScoringServiceTests
{
    private readonly PronunciationScoringService _sut = new();

    [Theory]
    [InlineData("ka", "", "no_audio", 0)]
    [InlineData("ka", "   ", "no_audio", 0)]
    [InlineData("ka", null, "no_audio", 0)]
    public void Bos_transkript_sifir_puan(string target, string? transcript, string key, int score)
    {
        var r = _sut.Evaluate(target, transcript, 95);
        Assert.Equal(key, r.FeedbackKey);
        Assert.Equal(score, r.Score);
    }

    [Theory]
    [InlineData("kedi", "kedi")]
    [InlineData("Kedi", "kedi")]      // büyük/küçük harf
    [InlineData("ke di", "kedi")]     // boşluklar yok sayılır
    public void Tam_eslesme_100(string target, string transcript)
    {
        var r = _sut.Evaluate(target, transcript, 0);
        Assert.Equal("exact_match", r.FeedbackKey);
        Assert.Equal(100, r.Score);
        Assert.Equal(1.0, r.Confidence);
    }

    [Theory]
    [InlineData("ah", "aah")]
    [InlineData("ha", "haa")]
    [InlineData("ah", "ahh")]
    public void Uzatma_varyanti_85(string target, string transcript)
    {
        var r = _sut.Evaluate(target, transcript, 0);
        Assert.Equal("length_variant", r.FeedbackKey);
        Assert.Equal(85, r.Score);
    }

    [Theory]
    [InlineData("kedi", "kedicik")]   // transkript hedefi içeriyor
    [InlineData("kedicik", "kedi")]   // hedef transkripti içeriyor
    public void Iceriyor_70(string target, string transcript)
    {
        var r = _sut.Evaluate(target, transcript, 0);
        Assert.Equal("contains", r.FeedbackKey);
        Assert.Equal(70, r.Score);
    }

    [Fact]
    public void Kisa_hedef_eslesmezse_api_skoruna_guvenilmez()
    {
        var r = _sut.Evaluate("ka", "ta", 95);
        Assert.Equal("mismatch_short", r.FeedbackKey);
        Assert.Equal(0, r.Score);
    }

    [Fact]
    public void Uzun_kelime_eslesmezse_api_skoru_kullanilir()
    {
        var r = _sut.Evaluate("kelebek", "kalem", 87.4);
        Assert.Equal("api_score", r.FeedbackKey);
        Assert.Equal(87, r.Score);
    }

    [Theory]
    [InlineData(150, 100)]
    [InlineData(-5, 0)]
    public void Skor_0_100_araligina_cekilir(double apiScore, int expected)
    {
        Assert.Equal(expected, _sut.Evaluate("kelebek", "kalem", apiScore).Score);
    }

    [Fact]
    public void Gecme_esigi_70_contains_sinirinda()
    {
        // Backend passed = score >= 70; "contains" tam sınırda geçer.
        Assert.True(_sut.Evaluate("kedi", "kedicik", 0).Score >= 70);
        Assert.False(_sut.Evaluate("ka", "ta", 0).Score >= 70);
    }

    [Theory]
    [InlineData("şapka", "sapka")]
    [InlineData("kız", "kiz")]
    [InlineData("çay", "cay")]
    public void Karar_turkce_karakterler_ascii_ye_indirgenir(string target, string transcript)
    {
        // Bugünkü davranış: ş/s, ı/i, ç/c ayrımı yapılmaz (terapötik olarak doğru mu? → Backlog B16)
        Assert.Equal("exact_match", _sut.Evaluate(target, transcript, 0).FeedbackKey);
    }

    [Fact]
    public void Karar_whisper_hattinda_uzun_kelime_eslesmezse_sifir()
    {
        // check-syllable apiScore = 0 gönderir → uzun ve eşleşmeyen kelime 0 alır (→ Backlog B16)
        var r = _sut.Evaluate("kelebek", "kalem", 0);
        Assert.Equal("api_score", r.FeedbackKey);
        Assert.Equal(0, r.Score);
    }
}
