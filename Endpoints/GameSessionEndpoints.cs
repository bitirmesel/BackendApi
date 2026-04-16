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
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == sessionId);

            if (session is null)
                return Results.NotFound("Game session bulunamadı.");

            session.Score = req.Score;
            session.MaxScore = req.MaxScore;
            session.DurationSec = req.DurationSec;
            session.FinishedAt = DateTime.UtcNow;

            if (session.Items.Any())
                db.GameSessionItems.RemoveRange(session.Items);

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

            if (items.Any())
                db.GameSessionItems.AddRange(items);

            if (session.TaskId.HasValue)
            {
                var task = await db.TaskItems.FindAsync(session.TaskId.Value);
                if (task is not null)
                    task.Status = "COMPLETED";
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Game session tamamlandı.",
                sessionId = session.Id
            });
        });

        // Terapistin bir danışanına ait seans geçmişi
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
                .Include(gs => gs.Game)
                .Include(gs => gs.Letter)
                .Include(gs => gs.Items)
                .Include(gs => gs.Feedbacks)
                .OrderByDescending(gs => gs.FinishedAt ?? gs.StartedAt)
                .Select(gs => new GameSessionHistoryListDto
                {
                    Id = gs.Id,
                    PlayerId = gs.PlayerId,
                    GameId = gs.GameId,
                    GameName = gs.Game.Name,
                    LetterId = gs.LetterId,
                    LetterCode = gs.Letter.Code,
                    Score = gs.Score,
                    MaxScore = gs.MaxScore,
                    DurationSec = gs.DurationSec,
                    StartedAt = gs.StartedAt,
                    FinishedAt = gs.FinishedAt,
                    ItemCount = gs.Items.Count,
                    CorrectItemCount = gs.Items.Count(i => i.IsCorrect == true),
                    LatestFeedback = gs.Feedbacks
                        .OrderByDescending(f => f.CreatedAt)
                        .Select(f => f.Comment)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Results.Ok(sessions);
        });

        // Tek seans detayı: item skorları + feedbackler
        app.MapGet("/api/gamesessions/{sessionId:long}", async (long sessionId, AppDbContext db) =>
        {
            var session = await db.GameSessions
                .AsNoTracking()
                .Include(gs => gs.Game)
                .Include(gs => gs.Letter)
                .Include(gs => gs.Items)
                .Include(gs => gs.Feedbacks)
                    .ThenInclude(f => f.Therapist)
                .FirstOrDefaultAsync(gs => gs.Id == sessionId);

            if (session is null)
                return Results.NotFound("Game session bulunamadı.");

            var dto = new GameSessionDetailDto
            {
                Id = session.Id,
                PlayerId = session.PlayerId,
                GameId = session.GameId,
                GameName = session.Game.Name,
                LetterId = session.LetterId,
                LetterCode = session.Letter.Code,
                Score = session.Score,
                MaxScore = session.MaxScore,
                DurationSec = session.DurationSec,
                StartedAt = session.StartedAt,
                FinishedAt = session.FinishedAt,
                Items = session.Items
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
                Feedbacks = session.Feedbacks
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => new GameSessionFeedbackDto
                    {
                        Id = f.Id,
                        TherapistId = f.TherapistId,
                        TherapistName = f.Therapist.Name ?? "Terapist",
                        Comment = f.Comment ?? "",
                        Rating = f.Rating,
                        CreatedAt = f.CreatedAt
                    })
                    .ToList()
            };

            return Results.Ok(dto);
        });

        app.MapPost("/api/gamesessions/{sessionId:long}/feedback", async (
            long sessionId,
            FeedbackReq req,
            AppDbContext db) =>
        {
            var session = await db.GameSessions.FindAsync(sessionId);
            if (session is null)
                return Results.NotFound("Oyun seansı bulunamadı.");

            var therapist = await db.Therapists.FindAsync(req.TherapistId);
            if (therapist is null)
                return Results.BadRequest("Terapist bulunamadı.");

            var feedback = new Feedback
            {
                GameSessionId = sessionId,
                TherapistId = req.TherapistId,
                Comment = req.Feedback,
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

        // İstersen geçici debug için bırak
        app.MapGet("/api/gamesessions/all", async (AppDbContext db) =>
        {
            var sessions = await db.GameSessions
                .AsNoTracking()
                .Include(gs => gs.Game)
                .Include(gs => gs.Letter)
                .Include(gs => gs.Items)
                .Include(gs => gs.Feedbacks)
                .OrderByDescending(gs => gs.FinishedAt ?? gs.StartedAt)
                .ToListAsync();

            return Results.Ok(sessions);
        });
    }
}