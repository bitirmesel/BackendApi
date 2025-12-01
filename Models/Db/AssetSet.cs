namespace DktApi.Models.Db;

public class AssetSet
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Letter { get; set; } = string.Empty; // A, B, C...
    public string GameCode { get; set; } = string.Empty;

    // JSON içeriği (kelimeler, cümleler vs.)
    public string JsonContent { get; set; } = "{}";
}
