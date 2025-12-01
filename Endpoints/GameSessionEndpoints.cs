using DktApi.Models.Db;
using DktApi.Models.Game;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class GameSessionEndpoints
{
    public static void MapGameSessionEndpoints(this WebApplication app)
    {
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
                player.Score += req.Score;
                player.LastActive = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }
}
