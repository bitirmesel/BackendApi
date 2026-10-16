namespace DktApi;

/// <summary>
/// İstemciye iç hata detayı (exception mesajı, stack trace, dış servis yanıtı) dönmemek için:
/// tam detay loglanır, istemciye yalnızca kısa bir hata kodu verilir.
/// Hata kodu Render loglarında "[ERROR {kod}]" olarak aranabilir.
/// </summary>
public static class ErrorLog
{
    public static string Log(string context, Exception ex)
    {
        var errorId = Guid.NewGuid().ToString("N")[..8];
        Console.WriteLine($"[ERROR {errorId}] {context}: {ex}");
        return errorId;
    }
}
