using DktApi.Models.Db;
using DktApi.Models.Game;
using Microsoft.EntityFrameworkCore;
using DktApi.Dtos.Game;

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
                Score = 0,
                DurationSec = null
            };

            db.GameSessions.Add(session);
            await db.SaveChangesAsync();

            return Results.Ok(new { sessionId = session.Id });
        });

        app.MapPost("/api/gamesessions/{sessionId:long}/complete", async (
            long sessionId,
            CompleteGameSessionReq req,
            AppDbContext db) =>
        {
            var session = await db.GameSessions
                .Include(gs => gs.Items)
                .FirstOrDefaultAsync(gs => gs.Id == sessionId);

            if (session is null)
                return Results.NotFound("Game session bulunamadı.");

            session.Score = req.Score;
            session.MaxScore = req.MaxScore;
            session.DurationSec = req.DurationSec;
            session.FinishedAt = DateTime.UtcNow;

            if (session.Items.Any())
            {
                db.GameSessionItems.RemoveRange(session.Items);
            }

            var newItems = req.Items.Select(i => new GameSessionItem
            {
                GameSessionId = session.Id,
                OrderNo = i.OrderNo,
                ItemType = i.ItemType,
                PromptText = i.PromptText,
                Score = i.Score,
                IsCorrect = i.IsCorrect,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            db.GameSessionItems.AddRange(newItems);

            if (session.TaskId.HasValue)
            {
                var task = await db.TaskItems.FindAsync(session.TaskId.Value);
                if (task != null)
                {
                    task.Status = "COMPLETED";
                }
            }

            var player = await db.Players.FindAsync(session.PlayerId);
            if (player != null)
            {
                player.TotalScore = (player.TotalScore ?? 0) + req.Score;
                player.LastLogin = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Session tamamlandı.",
                sessionId = session.Id,
                itemCount = newItems.Count
            });
        });

        app.MapGet("/api/players/{playerId:long}/sessions", async (long playerId, AppDbContext db) =>
        {
            var sessions = await db.GameSessions
                .AsNoTracking()
                .Include(gs => gs.Game)
                .Include(gs => gs.Letter)
                .Include(gs => gs.Items)
                .Where(gs => gs.PlayerId == playerId)
                .OrderByDescending(gs => gs.FinishedAt)
                .Select(gs => new
                {
                    sessionId = gs.Id,
                    gameId = gs.GameId,
                    gameName = gs.Game.Name,
                    letterId = gs.LetterId,
                    letterCode = gs.Letter.Code,
                    score = gs.Score,
                    maxScore = gs.MaxScore,
                    durationSec = gs.DurationSec,
                    startedAt = gs.StartedAt,
                    finishedAt = gs.FinishedAt,
                    itemCount = gs.Items.Count
                })
                .ToListAsync();

            return Results.Ok(sessions);
        })
        .WithTags("GameSessions");

        app.MapGet("/api/gamesessions/{sessionId:long}", async (long sessionId, AppDbContext db) =>
        {
            var session = await db.GameSessions
                .AsNoTracking()
                .Include(gs => gs.Game)
                .Include(gs => gs.Letter)
                .Include(gs => gs.Items)
                .FirstOrDefaultAsync(gs => gs.Id == sessionId);

            if (session is null)
                return Results.NotFound("Game session bulunamadı.");

            var response = new
            {
                sessionId = session.Id,
                playerId = session.PlayerId,
                gameId = session.GameId,
                gameName = session.Game.Name,
                letterId = session.LetterId,
                letterCode = session.Letter.Code,
                score = session.Score,
                maxScore = session.MaxScore,
                durationSec = session.DurationSec,
                startedAt = session.StartedAt,
                finishedAt = session.FinishedAt,
                items = session.Items
                    .OrderBy(i => i.OrderNo)
                    .Select(i => new
                    {
                        id = i.Id,
                        orderNo = i.OrderNo,
                        itemType = i.ItemType,
                        promptText = i.PromptText,
                        score = i.Score,
                        isCorrect = i.IsCorrect,
                        createdAt = i.CreatedAt
                    })
                    .ToList()
            };

            return Results.Ok(response);
        })
        .WithTags("GameSessions");
    }
}