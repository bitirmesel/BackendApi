namespace DktApi.Services;

/// <summary>
/// Hangi oyunun hangi harfle görev olarak atanabileceği.
/// Oyun ID'leri kanonik katalogdur (ARCHITECTURE.md §6.4); Fonem'deki KnownGameIds ile aynı.
/// Fonem'e yeni bir oyun eklendiğinde ya da içerik kaynağı değiştiğinde burası güncellenir.
/// </summary>
public static class GameAvailability
{
    /// <summary>Katalogda var ama Fonem'de henüz oyunu yok (Cümle S2 - Fill Gap).</summary>
    private static readonly HashSet<long> NotPlayableGameIds = new() { 9 };

    /// <summary>
    /// İçeriği uygulamaya paketli oyunlar (Cümle Kur videoları, Hikâye);
    /// harf için asset set gerekmez.
    /// </summary>
    private static readonly HashSet<long> BundledContentGameIds = new() { 8, 10 };

    public static bool IsPlayable(long gameId) => !NotPlayableGameIds.Contains(gameId);

    public static bool RequiresAssetSet(long gameId) => !BundledContentGameIds.Contains(gameId);

    /// <summary>
    /// Görev atama kuralı. Atanabiliyorsa <c>null</c>, değilse istemciye dönen hata kodu.
    /// </summary>
    public static string? ValidateAssignment(long gameId, bool gameExists, bool letterExists, bool hasAssetSet)
    {
        if (!gameExists) return "game_not_found";
        if (!letterExists) return "letter_not_found";
        if (!IsPlayable(gameId)) return "game_not_playable";
        if (RequiresAssetSet(gameId) && !hasAssetSet) return "no_content_for_letter";
        return null;
    }

    /// <summary>Hata kodunun terapiste gösterilecek açıklaması.</summary>
    public static string Describe(string code) => code switch
    {
        "game_not_found" => "Oyun bulunamadı.",
        "letter_not_found" => "Harf bulunamadı.",
        "game_not_playable" => "Bu oyun çocuk uygulamasında henüz yok; görev olarak atanamaz.",
        "no_content_for_letter" => "Bu oyunun seçilen harf için içeriği yok; görev olarak atanamaz.",
        _ => "Görev atanamadı."
    };
}
