using DktApi.Models.Db;
using DktApi.Models.Game;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class GameSessionEndpoints
{
    public static void MapGameSessionEndpoints(this WebApplication app)
    {
        /*
        app.MapPost("/api/gamesessions/start", async (CreateGameSessionReq req, AppDbContext db) =>
        {
            var player = await db.Players.FindAsync(req.PlayerId);
            var game = await db.Games.FindAsync(req.GameId);

            if (player is null || game is null)
                return Results.BadRequest("Player veya game bulunamadı");

            var session = new GameSession
            {
                PlayerId = req.PlayerId,
                GameId = req.GameId,
                StartedAt = DateTime.UtcNow
            };

            db.GameSessions.Add(session);
            await db.SaveChangesAsync();

            return Results.Ok(session.Id);
        });
        */

        app.MapPost("/api/gamesessions/start", async (CreateGameSessionReq req, AppDbContext db) =>
        {
            var player = await db.Players.FindAsync(req.PlayerId);
            var game = await db.Games.FindAsync(req.GameId);

            if (player is null || game is null)
                return Results.BadRequest("Player veya game bulunamadı");

            if (req.LetterId is null || req.AssetSetId is null)
                return Results.BadRequest("LetterId ve AssetSetId zorunludur.");

            var letter = await db.Letters.FindAsync(req.LetterId.Value);
            var assetSet = await db.AssetSets.FindAsync(req.AssetSetId.Value);

            if (letter is null || assetSet is null)
                return Results.BadRequest("Letter veya AssetSet bulunamadı.");

            var session = new GameSession
            {
                PlayerId = req.PlayerId,
                GameId = req.GameId,
                LetterId = req.LetterId.Value,
                AssetSetId = req.AssetSetId.Value,
                TaskId = req.TaskId,
                StartedAt = DateTime.UtcNow,
                MaxScore = 0,
                DurationSec = null
            };

            db.GameSessions.Add(session);
            await db.SaveChangesAsync();

            return Results.Ok(new { sessionId = session.Id });
        });

        /*
        app.MapPost("/api/gamesessions/finish", async (FinishGameSessionReq req, AppDbContext db) =>
        {
            var session = await db.GameSessions.FindAsync(req.SessionId);
            if (session is null) return Results.NotFound();

            session.FinishedAt = DateTime.UtcNow;
            session.Score = req.Score;

            // basit örnek: oyuncu skoruna ekleyelim
            var player = await db.Players.FindAsync(session.PlayerId);
            if (player is not null)
            {
                player.TotalScore += req.Score;
                player.LastLogin = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        });
        */

        app.MapPost("/api/gamesessions/finish", async (FinishGameSessionReq req, AppDbContext db) =>
{
    // Mevcut satırı ARATMIYORUZ (FindAsync yapmıyoruz). 
    // Gelen her isteği yeni bir GameSession satırı olarak ekliyoruz.

    var newRecord = new GameSession
    {
        PlayerId = req.PlayerId, // İstek modeline PlayerId eklemelisin veya GameContext'ten almalısın
        GameId = req.GameId,
        LetterId = req.LetterId,
        AssetSetId = req.SessionId, // Eski sessionId'yi 'Grup ID'si' gibi kullanabiliriz
        Score = req.Score,
        TargetWord = req.TargetWord, // "Kedi", "Köpek" bilgisi
        FinishedAt = DateTime.UtcNow,
        StartedAt = DateTime.UtcNow, // Anlık kayıt olduğu için ikisi aynı olabilir
        MaxScore = 100
    };

    db.GameSessions.Add(newRecord); // UPDATE yerine ADD yapıyoruz

    // Oyuncunun toplam puanını güncelleme kısmı
    var player = await db.Players.FindAsync(req.PlayerId);
    if (player != null)
    {
        player.TotalScore ??= 0;
        player.TotalScore += req.Score;
    }

    await db.SaveChangesAsync();
    return Results.Ok();
});

        // Endpoints/GameSessionEndpoints.cs içine eklenebilir
        app.MapGet("/api/gamesessions/all", async (AppDbContext db) =>
        {
            var sessions = await db.GameSessions
                .OrderByDescending(s => s.FinishedAt)
                .ToListAsync();
            return Results.Ok(sessions);
        })
        .WithTags("GameSessions")
        .WithName("GetAllSessionsDebug");
        

        // Tüm kayıtları silmek için (Postman: DELETE /api/gamesessions/clear-all)
        app.MapDelete("/api/gamesessions/clear-all", async (AppDbContext db) =>
        {
            // Veritabanındaki tüm GameSessions kayıtlarını seçer ve temizler
            var allSessions = await db.GameSessions.ToListAsync();
            db.GameSessions.RemoveRange(allSessions);

            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Tüm oyun kayıtları başarıyla silindi." });
        })
        .WithTags("GameSessions");

    }

}
