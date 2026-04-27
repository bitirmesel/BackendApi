using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class PlayerInboxEndpoints
{
    private const string SourceTask = "task";
    private const string SourceFeedback = "feedback";
    private const string SourceNotification = "notification";

    public static void MapPlayerInboxEndpoints(this WebApplication app)
    {
        // ----------------------------------------------------
        // GET /api/players/{playerId}/inbox
        // Oyuncu bildirim ekranı: görevler + terapist yorumları + genel bildirimler
        // ----------------------------------------------------
        app.MapGet("/api/players/{playerId:long}/inbox", async (long playerId, AppDbContext db) =>
        {
            var playerExists = await db.Players.AnyAsync(p => p.Id == playerId);
            if (!playerExists)
                return Results.NotFound("Player not found.");

            var readStates = await db.InboxReadStates
                .AsNoTracking()
                .Where(r => r.PlayerId == playerId)
                .Select(r => new
                {
                    r.SourceType,
                    r.SourceId
                })
                .ToListAsync();

            bool IsRead(string sourceType, long sourceId)
            {
                return readStates.Any(r =>
                    r.SourceType == sourceType &&
                    r.SourceId == sourceId);
            }

            // -----------------------------
            // 1) Assigned Tasks
            // -----------------------------
            var tasks = await db.TaskItems
                .AsNoTracking()
                .Include(t => t.Game)
                .Include(t => t.Letter)
                .Include(t => t.Therapist)
                .Where(t => t.PlayerId == playerId)
                .OrderByDescending(t => t.AssignedAt)
                .Take(100)
                .Select(t => new
                {
                    id = t.Id,
                    sourceType = SourceTask,
                    title = "Yeni Görev Atandı",
                    message =
                        (t.Note != null && t.Note != "")
                            ? t.Note
                            : ((t.Game != null ? t.Game.Name : "Oyun") + " görevi atandı."),
                    gameId = t.GameId,
                    gameName = t.Game != null ? t.Game.Name : "Oyun",
                    letterId = t.LetterId,
                    letterCode = t.Letter != null ? t.Letter.Code : "",
                    letterDisplayName = t.Letter != null ? t.Letter.DisplayName : "",
                    therapistId = t.TherapistId,
                    therapistName = t.Therapist != null ? t.Therapist.Name : "Terapist",
                    assignedAt = t.AssignedAt,
                    dueAt = t.DueAt,
                    status = t.Status,
                    isRead = false
                })
                .ToListAsync();

            var taskItems = tasks
                .Select(t => new
                {
                    t.id,
                    t.sourceType,
                    t.title,
                    t.message,
                    t.gameId,
                    t.gameName,
                    t.letterId,
                    t.letterCode,
                    t.letterDisplayName,
                    t.therapistId,
                    t.therapistName,
                    t.assignedAt,
                    t.dueAt,
                    t.status,
                    isRead = IsRead(SourceTask, t.id)
                })
                .ToList();

            // -----------------------------
            // 2) Therapist Feedbacks
            // -----------------------------
            var feedbacksRaw = await db.Feedbacks
                .AsNoTracking()
                .Include(f => f.Therapist)
                .Include(f => f.GameSession)
                    .ThenInclude(gs => gs.Game)
                .Include(f => f.GameSession)
                    .ThenInclude(gs => gs.Letter)
                .Where(f => f.GameSession.PlayerId == playerId)
                .OrderByDescending(f => f.CreatedAt)
                .Take(100)
                .Select(f => new
                {
                    id = f.Id,
                    sourceType = SourceFeedback,
                    title = "Terapist Yorumu",
                    message = f.Comment ?? "",
                    sessionId = f.GameSessionId,
                    gameId = f.GameSession.GameId,
                    gameName = f.GameSession.Game != null ? f.GameSession.Game.Name : "Oyun",
                    letterId = f.GameSession.LetterId,
                    letterCode = f.GameSession.Letter != null ? f.GameSession.Letter.Code : "",
                    letterDisplayName = f.GameSession.Letter != null ? f.GameSession.Letter.DisplayName : "",
                    therapistId = f.TherapistId,
                    therapistName = f.Therapist != null ? f.Therapist.Name : "Terapist",
                    rating = f.Rating,
                    createdAt = f.CreatedAt,
                    isRead = false
                })
                .ToListAsync();

            var feedbackItems = feedbacksRaw
                .Select(f => new
                {
                    f.id,
                    f.sourceType,
                    f.title,
                    f.message,
                    f.sessionId,
                    f.gameId,
                    f.gameName,
                    f.letterId,
                    f.letterCode,
                    f.letterDisplayName,
                    f.therapistId,
                    f.therapistName,
                    f.rating,
                    f.createdAt,
                    isRead = IsRead(SourceFeedback, f.id)
                })
                .ToList();

            // -----------------------------
            // 3) General Notifications
            // -----------------------------
            var notificationsRaw = await db.Notifications
                .AsNoTracking()
                .Include(n => n.Therapist)
                .Where(n => n.PlayerId == playerId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(100)
                .Select(n => new
                {
                    id = n.Id,
                    sourceType = SourceNotification,
                    title = "Bildirim",
                    message = n.Message,
                    therapistId = n.TherapistId,
                    therapistName = n.Therapist != null ? n.Therapist.Name : "Terapist",
                    createdAt = n.CreatedAt,
                    dbIsRead = n.IsRead
                })
                .ToListAsync();

            var notificationItems = notificationsRaw
                .Select(n => new
                {
                    n.id,
                    n.sourceType,
                    n.title,
                    n.message,
                    n.therapistId,
                    n.therapistName,
                    n.createdAt,
                    isRead = n.dbIsRead || IsRead(SourceNotification, n.id)
                })
                .ToList();

            var unreadCount =
                taskItems.Count(x => !x.isRead) +
                feedbackItems.Count(x => !x.isRead) +
                notificationItems.Count(x => !x.isRead);

            return Results.Ok(new
            {
                unreadCount,
                tasks = taskItems,
                feedbacks = feedbackItems,
                notifications = notificationItems
            });
        })
        .WithTags("Player Inbox")
        .WithName("GetPlayerInbox");

        // ----------------------------------------------------
        // GET /api/players/{playerId}/inbox/unread-count
        // ----------------------------------------------------
        app.MapGet("/api/players/{playerId:long}/inbox/unread-count", async (long playerId, AppDbContext db) =>
        {
            var playerExists = await db.Players.AnyAsync(p => p.Id == playerId);
            if (!playerExists)
                return Results.NotFound("Player not found.");

            var readStates = await db.InboxReadStates
                .AsNoTracking()
                .Where(r => r.PlayerId == playerId)
                .Select(r => new { r.SourceType, r.SourceId })
                .ToListAsync();

            bool IsRead(string sourceType, long sourceId)
            {
                return readStates.Any(r =>
                    r.SourceType == sourceType &&
                    r.SourceId == sourceId);
            }

            var taskIds = await db.TaskItems
                .AsNoTracking()
                .Where(t => t.PlayerId == playerId)
                .Select(t => t.Id)
                .ToListAsync();

            var feedbackIds = await db.Feedbacks
                .AsNoTracking()
                .Where(f => f.GameSession.PlayerId == playerId)
                .Select(f => f.Id)
                .ToListAsync();

            var notifications = await db.Notifications
                .AsNoTracking()
                .Where(n => n.PlayerId == playerId)
                .Select(n => new { n.Id, n.IsRead })
                .ToListAsync();

            var count =
                taskIds.Count(id => !IsRead(SourceTask, id)) +
                feedbackIds.Count(id => !IsRead(SourceFeedback, id)) +
                notifications.Count(n => !n.IsRead && !IsRead(SourceNotification, n.Id));

            return Results.Ok(new { unreadCount = count });
        })
        .WithTags("Player Inbox")
        .WithName("GetPlayerInboxUnreadCount");

        // ----------------------------------------------------
        // POST /api/players/{playerId}/inbox/{sourceType}/{sourceId}/read
        // Tek item okundu
        // ----------------------------------------------------
        app.MapPost("/api/players/{playerId:long}/inbox/{sourceType}/{sourceId:long}/read",
            async (long playerId, string sourceType, long sourceId, AppDbContext db) =>
            {
                sourceType = NormalizeSourceType(sourceType);

                if (!IsValidSourceType(sourceType))
                    return Results.BadRequest("sourceType sadece task, feedback veya notification olabilir.");

                var playerExists = await db.Players.AnyAsync(p => p.Id == playerId);
                if (!playerExists)
                    return Results.NotFound("Player not found.");

                var sourceExists = await SourceExists(db, playerId, sourceType, sourceId);
                if (!sourceExists)
                    return Results.NotFound("Source item not found for this player.");

                var exists = await db.InboxReadStates.AnyAsync(r =>
                    r.PlayerId == playerId &&
                    r.SourceType == sourceType &&
                    r.SourceId == sourceId);

                if (!exists)
                {
                    db.InboxReadStates.Add(new InboxReadState
                    {
                        PlayerId = playerId,
                        SourceType = sourceType,
                        SourceId = sourceId,
                        ReadAt = DateTime.UtcNow
                    });
                }

                if (sourceType == SourceNotification)
                {
                    var notification = await db.Notifications
                        .FirstOrDefaultAsync(n => n.Id == sourceId && n.PlayerId == playerId);

                    if (notification != null)
                        notification.IsRead = true;
                }

                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    message = "Okundu işaretlendi.",
                    playerId,
                    sourceType,
                    sourceId
                });
            })
        .WithTags("Player Inbox")
        .WithName("MarkInboxItemRead");

        // ----------------------------------------------------
        // POST /api/players/{playerId}/inbox/read-all
        // Hepsini okundu
        // ----------------------------------------------------
        app.MapPost("/api/players/{playerId:long}/inbox/read-all", async (long playerId, AppDbContext db) =>
        {
            var playerExists = await db.Players.AnyAsync(p => p.Id == playerId);
            if (!playerExists)
                return Results.NotFound("Player not found.");

            var now = DateTime.UtcNow;

            var taskIds = await db.TaskItems
                .AsNoTracking()
                .Where(t => t.PlayerId == playerId)
                .Select(t => t.Id)
                .ToListAsync();

            var feedbackIds = await db.Feedbacks
                .AsNoTracking()
                .Where(f => f.GameSession.PlayerId == playerId)
                .Select(f => f.Id)
                .ToListAsync();

            var notificationIds = await db.Notifications
                .Where(n => n.PlayerId == playerId)
                .Select(n => n.Id)
                .ToListAsync();

            var existing = await db.InboxReadStates
                .Where(r => r.PlayerId == playerId)
                .ToListAsync();

            void AddIfMissing(string sourceType, long sourceId)
            {
                var alreadyExists = existing.Any(r =>
                    r.SourceType == sourceType &&
                    r.SourceId == sourceId);

                if (!alreadyExists)
                {
                    db.InboxReadStates.Add(new InboxReadState
                    {
                        PlayerId = playerId,
                        SourceType = sourceType,
                        SourceId = sourceId,
                        ReadAt = now
                    });
                }
            }

            foreach (var id in taskIds)
                AddIfMissing(SourceTask, id);

            foreach (var id in feedbackIds)
                AddIfMissing(SourceFeedback, id);

            foreach (var id in notificationIds)
                AddIfMissing(SourceNotification, id);

            var notifications = await db.Notifications
                .Where(n => n.PlayerId == playerId && !n.IsRead)
                .ToListAsync();

            foreach (var n in notifications)
                n.IsRead = true;

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Tüm inbox okundu işaretlendi.",
                playerId
            });
        })
        .WithTags("Player Inbox")
        .WithName("MarkAllInboxRead");
    }

    private static string NormalizeSourceType(string sourceType)
    {
        return (sourceType ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static bool IsValidSourceType(string sourceType)
    {
        return sourceType is SourceTask or SourceFeedback or SourceNotification;
    }

    private static async Task<bool> SourceExists(
        AppDbContext db,
        long playerId,
        string sourceType,
        long sourceId)
    {
        return sourceType switch
        {
            SourceTask => await db.TaskItems
                .AnyAsync(t => t.Id == sourceId && t.PlayerId == playerId),

            SourceFeedback => await db.Feedbacks
                .AnyAsync(f => f.Id == sourceId && f.GameSession.PlayerId == playerId),

            SourceNotification => await db.Notifications
                .AnyAsync(n => n.Id == sourceId && n.PlayerId == playerId),

            _ => false
        };
    }
}