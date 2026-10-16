using System.Globalization;
using DktApi.Services;

namespace DktApi.Tests;

public class EmailNormalizerTests
{
    [Theory]
    [InlineData("Ahmet@X.com", "ahmet@x.com")]
    [InlineData("  ayse@okul.edu.tr ", "ayse@okul.edu.tr")]
    [InlineData("zaten@kucuk.com", "zaten@kucuk.com")]
    public void Kirpar_ve_kucuk_harfe_cevirir(string input, string expected)
    {
        Assert.Equal(expected, EmailNormalizer.Normalize(input));
    }

    [Fact]
    public void Null_null_kalir()
    {
        Assert.Null(EmailNormalizer.Normalize(null));
    }

    [Fact]
    public void Turkce_kulturde_I_noktasiz_i_olmaz()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Assert.Equal("ali@ix.com", EmailNormalizer.Normalize("ALI@IX.COM"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
