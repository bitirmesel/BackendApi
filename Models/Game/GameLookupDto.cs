using DktApi.Models.Db;

namespace DktApi.Models.Game;

// Oyunları listelerken kullanılacak DTO
public class GameLookupDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GameTypeName { get; set; } = string.Empty;
    public string DifficultyLevelName { get; set; } = string.Empty;
    public int DifficultyLevel { get; set; }

    /// <summary>Görev olarak atanabilir mi (Fonem'de oyunu var mı). Bkz. GameAvailability.</summary>
    public bool Assignable { get; set; } = true;
}