// Dtos.cs

// Login sırasında DB'den çekeceğimiz basit user modeli
public record DbUser(
    long   Id,
    string Name,
    string Email,
    string Password
);

// Oyun oturumu oluşturma isteği
public record CreateGameSessionReq(
    long  PlayerId,      // players.id
    long  GameId,        // games.id
    long  LetterId,      // letters.id
    long? AssetSetId,    // asset_sets.id (opsiyonel)
    long? TaskId,        // tasks.id (opsiyonel)
    int?  MaxScore       // maksimum skor (ör. 100)
);

// Login isteği
public record LoginRequest(
    string Role,      // "player" | "therapist"
    string Email,
    string Password
);

// Login cevabı
public record LoginResponse(
    string Token,
    string Role,
    long   UserId,
    string Name,
    string Email
);
