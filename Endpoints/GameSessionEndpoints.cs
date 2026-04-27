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
                return Results.BadRequest("Player veya game bulunamadı.");

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
                Score = 0,
                MaxScore = 0,
                DurationSec = null
            };

            db.GameSessions.Add(session);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                sessionId = session.Id
            });
        });

        

        app.MapPost("/api/gamesessions/{sessionId:long}/complete", async (
    long sessionId,
    CompleteGameSessionReq req,
    AppDbContext db) =>
{
    if (req.MaxScore <= 0)
    {
        return Results.BadRequest(new
        {
            message = "MaxScore 0'dan büyük olmalıdır."
        });
    }

    if (req.Score < 0)
    {
        return Results.BadRequest(new
        {
            message = "Score negatif olamaz."
        });
    }

    var session = await db.GameSessions
        .Include(gs => gs.Items)
        .FirstOrDefaultAsync(gs => gs.Id == sessionId);

    if (session is null)
    {
        return Results.NotFound(new
        {
            message = "Game session bulunamadı."
        });
    }

    await using var tx = await db.Database.BeginTransactionAsync();

    session.Score = req.Score;
    session.MaxScore = req.MaxScore;
    session.DurationSec = req.DurationSec;
    session.FinishedAt = DateTime.UtcNow;

    if (session.Items.Any())
    {
        db.GameSessionItems.RemoveRange(session.Items);
    }

    var items = req.Items
        .OrderBy(x => x.OrderNo)
        .Select(x => new GameSessionItem
        {
            GameSessionId = session.Id,
            OrderNo = x.OrderNo,
            ItemType = string.IsNullOrWhiteSpace(x.ItemType) ? "WORD" : x.ItemType,
            PromptText = x.PromptText ?? string.Empty,
            Score = x.Score,
            IsCorrect = x.IsCorrect,
            CreatedAt = DateTime.UtcNow
        })
        .ToList();

    if (items.Count > 0)
    {
        db.GameSessionItems.AddRange(items);
    }

    string? taskStatus = null;

    if (session.TaskId.HasValue)
    {
        var task = await db.TaskItems.FindAsync(session.TaskId.Value);

        if (task is not null)
        {
            task.Status = "COMPLETED";
            taskStatus = task.Status;
        }
    }

    await db.SaveChangesAsync();
    await tx.CommitAsync();

    return Results.Ok(new
    {
        message = "Game session tamamlandı.",
        sessionId = session.Id,
        taskId = session.TaskId,
        taskStatus,
        score = session.Score,
        maxScore = session.MaxScore,
        durationSec = session.DurationSec,
        finishedAt = session.FinishedAt,
        itemCount = items.Count
    });
});


        app.MapGet("/api/therapists/{therapistId:long}/players/{playerId:long}/game-sessions",
            async (long therapistId, long playerId, AppDbContext db) =>
        {
            var relationExists = await db.TherapistClients
                .AnyAsync(tc => tc.TherapistId == therapistId && tc.PlayerId == playerId);

            if (!relationExists)
                return Results.Forbid();

            var sessions = await db.GameSessions
                .AsNoTracking()
                .Where(gs => gs.PlayerId == playerId)
                .Select(gs => new GameSessionHistoryListDto
                {
                    Id = gs.Id,
                    PlayerId = gs.PlayerId,
                    GameId = gs.GameId,
                    GameName = gs.Game != null ? gs.Game.Name : "Oyun",
                    LetterId = gs.LetterId,
                    LetterCode = gs.Letter != null ? gs.Letter.Code : "?",
                    Score = gs.Score,
                    MaxScore = gs.MaxScore,
                    DurationSec = gs.DurationSec,
                    StartedAt = gs.StartedAt,
                    FinishedAt = gs.FinishedAt,
                    ItemCount = gs.Items.Count(),
                    CorrectItemCount = gs.Items.Count(i => i.IsCorrect == true),
                    LatestFeedback = gs.Feedbacks
                        .OrderByDescending(f => f.CreatedAt)
                        .Select(f => f.Comment)
                        .FirstOrDefault()
                })
                .OrderByDescending(gs => gs.FinishedAt ?? gs.StartedAt)
                .ToListAsync();

            return Results.Ok(sessions);
        });

        app.MapGet("/api/gamesessions/{sessionId:long}", async (long sessionId, AppDbContext db) =>
        {
            var session = await db.GameSessions
                .AsNoTracking()
                .Where(gs => gs.Id == sessionId)
                .Select(gs => new GameSessionDetailDto
                {
                    Id = gs.Id,
                    PlayerId = gs.PlayerId,
                    GameId = gs.GameId,
                    GameName = gs.Game != null ? gs.Game.Name : "Oyun",
                    LetterId = gs.LetterId,
                    LetterCode = gs.Letter != null ? gs.Letter.Code : "?",
                    Score = gs.Score,
                    MaxScore = gs.MaxScore,
                    DurationSec = gs.DurationSec,
                    StartedAt = gs.StartedAt,
                    FinishedAt = gs.FinishedAt,
                    Items = gs.Items
                        .OrderBy(i => i.OrderNo)
                        .Select(i => new GameSessionHistoryItemDto
                        {
                            Id = i.Id,
                            OrderNo = i.OrderNo,
                            ItemType = i.ItemType,
                            PromptText = i.PromptText,
                            Score = i.Score,
                            IsCorrect = i.IsCorrect
                        })
                        .ToList(),
                    Feedbacks = gs.Feedbacks
                        .OrderByDescending(f => f.CreatedAt)
                        .Select(f => new GameSessionFeedbackDto
                        {
                            Id = f.Id,
                            TherapistId = f.TherapistId,
                            TherapistName = f.Therapist != null
                                ? (f.Therapist.Name ?? "Terapist")
                                : "Terapist",
                            Comment = f.Comment ?? "",
                            Rating = f.Rating,
                            CreatedAt = f.CreatedAt
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (session is null)
                return Results.NotFound("Game session bulunamadı.");

            return Results.Ok(session);
        });

        app.MapPost("/api/gamesessions/{sessionId:long}/feedback", async (
            long sessionId,
            FeedbackReq req,
            AppDbContext db) =>
        {
            var sessionExists = await db.GameSessions.AnyAsync(gs => gs.Id == sessionId);
            if (!sessionExists)
                return Results.NotFound("Oyun seansı bulunamadı.");

            var therapist = await db.Therapists.FindAsync(req.TherapistId);
            if (therapist is null)
                return Results.BadRequest("Terapist bulunamadı.");

            var feedback = new Feedback
            {
                GameSessionId = sessionId,
                TherapistId = req.TherapistId,
                Comment = req.Feedback ?? string.Empty,
                Rating = 5,
                CreatedAt = DateTime.UtcNow
            };

            db.Feedbacks.Add(feedback);
            await db.SaveChangesAsync();

            return Results.Created($"/api/gamesessions/{sessionId}", new
            {
                id = feedback.Id,
                message = "Geri bildirim kaydedildi."
            });
        });

        app.MapGet("/api/gamesessions/all", async (AppDbContext db) =>
        {
            var sessions = await db.GameSessions
                .AsNoTracking()
                .Select(gs => new
                {
                    gs.Id,
                    gs.PlayerId,
                    gs.GameId,
                    GameName = gs.Game != null ? gs.Game.Name : "Oyun",
                    gs.LetterId,
                    LetterCode = gs.Letter != null ? gs.Letter.Code : "?",
                    gs.Score,
                    gs.MaxScore,
                    gs.DurationSec,
                    gs.StartedAt,
                    gs.FinishedAt,
                    Items = gs.Items
                        .OrderBy(i => i.OrderNo)
                        .Select(i => new
                        {
                            i.Id,
                            i.OrderNo,
                            i.ItemType,
                            i.PromptText,
                            i.Score,
                            i.IsCorrect
                        })
                        .ToList(),
                    Feedbacks = gs.Feedbacks
                        .OrderByDescending(f => f.CreatedAt)
                        .Select(f => new
                        {
                            f.Id,
                            f.TherapistId,
                            f.Comment,
                            f.Rating,
                            f.CreatedAt
                        })
                        .ToList()
                })
                .OrderByDescending(x => x.FinishedAt ?? x.StartedAt)
                .ToListAsync();

            return Results.Ok(sessions);
        });
    }
}