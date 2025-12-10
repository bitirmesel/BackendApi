using DktApi.Models.Db;
using DktApi.Dtos.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        // ----------------------------------------------------
        // 1) GÖREV ATA – POST /api/tasks
        // ----------------------------------------------------
        app.MapPost("/api/tasks", async (AssignTaskRequest req, AppDbContext db) =>
        {
            // İsteğe bağlı: Therapist & Player var mı kontrolü
            var therapistExists = await db.Therapists.AnyAsync(t => t.Id == req.TherapistId);
            if (!therapistExists)
                return Results.BadRequest("Therapist not found");

            var playerExists = await db.Players.AnyAsync(p => p.Id == req.PlayerId);
            if (!playerExists)
                return Results.BadRequest("Player not found");

            var task = new TaskItem
            {
                TherapistId = req.TherapistId,
                PlayerId = req.PlayerId,
                GameId = req.GameId,
                LetterId = req.LetterId,
                AssetSetId = req.AssetSetId,
                Status = "ASSIGNED",
                AssignedAt = DateTime.UtcNow,
                DueAt = req.DueAt,
                Note = req.Note
            };

            db.TaskItems.Add(task);
            await db.SaveChangesAsync();

            return Results.Created($"/api/tasks/{task.Id}", new { task.Id });
        });

        // ----------------------------------------------------
        // 2) TERAPİSTİN GÖREV LİSTESİ – GET /api/therapists/{id}/tasks
        // (İster ayrı liste ekranında, ister öğrenci detayında kullanılabilir)
        // ----------------------------------------------------
        app.MapGet("/api/therapists/{therapistId:long}/tasks",
            async (long therapistId, AppDbContext db) =>
        {
            var tasks = await db.TaskItems
                .Include(t => t.Player)
                .Include(t => t.Game)
                .Include(t => t.Letter)
                .Where(t => t.TherapistId == therapistId)
                .OrderByDescending(t => t.AssignedAt)
                .ToListAsync();

            var result = tasks.Select(t => new
            {
                id = t.Id,
                studentId = t.PlayerId,
                studentName = t.Player?.Name,
                gameId = t.GameId,
                gameName = t.Game?.Name,
                letterId = t.LetterId,
                letterCode = t.Letter?.Code,
                status = t.Status,
                assignedAt = t.AssignedAt,
                dueAt = t.DueAt,
                note = t.Note
            });

            return Results.Ok(result);
        });

        // ----------------------------------------------------
        // 3) GÖREVLERDEN BİLDİRİM ÜRET – GET /api/therapists/{id}/notifications
        // TasksScreen burayı kullanıyor
        // ----------------------------------------------------
        app.MapGet("/api/therapists/{therapistId:long}/notifications",
            async (long therapistId, AppDbContext db) =>
            {
                var tasks = await db.TaskItems
                    .Include(t => t.Player)
                    .Where(t => t.TherapistId == therapistId)
                    .OrderByDescending(t => t.AssignedAt)
                    .Take(50)
                    .ToListAsync();

                var now = DateTime.UtcNow;

                var notifications = tasks.Select(t =>
                {
                    string type = t.Status switch
                    {
                        "COMPLETED" => "success",
                        "ASSIGNED"  => "info",
                        "OVERDUE"   => "warning",
                        _           => "info"
                    };

                    string title = t.Status switch
                    {
                        "COMPLETED" => "Görev Tamamlandı",
                        "ASSIGNED"  => "Yeni Görev Atandı",
                        "OVERDUE"   => "Geciken Görev",
                        _           => "Görev Güncellendi"
                    };

                    var studentName = t.Player?.Name ?? "Öğrenci";

                    string message = t.Status switch
                    {
                        "COMPLETED" => $"{studentName} atanmış görevi tamamladı.",
                        "ASSIGNED"  => $"{studentName} için yeni bir görev atandı.",
                        "OVERDUE"   => $"{studentName} için görev süresi dolmak üzere.",
                        _           => $"{studentName} görevi güncellendi."
                    };

                    var time = (t.AssignedAt ?? now).ToLocalTime().ToString("g");

                    return new NotificationDto
                    {
                        Title = title,
                        Message = message,
                        Time = time,
                        Type = type,
                        Unread = true
                    };
                }).ToList();

                return Results.Ok(notifications);
            });

            // 4) PLAYER İÇİN AKTİF GÖREVLER – UNITY
            // GET /api/players/{playerId}/tasks/active
            app.MapGet("/api/players/{playerId:long}/tasks/active",
            async (long playerId, AppDbContext db) =>
            {
                var tasks = await db.TaskItems
                    .Include(t => t.Game)
                    .Include(t => t.Letter)
                    .Where(t => t.PlayerId == playerId && t.Status == "ASSIGNED")
                    .OrderBy(t => t.DueAt ?? t.AssignedAt)
                    .ToListAsync();

                var result = tasks.Select(t => new
                {
                    taskId = t.Id,
                    gameId = t.GameId,
                    gameName = t.Game.Name,
                    letterId = t.LetterId,
                    letterCode = t.Letter.Code,
                    letterDisplayName = t.Letter.DisplayName,
                    assetSetId = t.AssetSetId,
                    status = t.Status,
                    assignedAt = t.AssignedAt,
                    dueAt = t.DueAt
                });

                return Results.Ok(result);
            })
            .WithTags("Tasks")
            .WithName("GetPlayerActiveTasks");

            // 4) PLAYER İÇİN TÜM ATANMIŞ GÖREVLER — GET /api/players/{playerId}/tasks
app.MapGet("/api/players/{playerId:long}/tasks",
    async (long playerId, AppDbContext db) =>
{
    var tasks = await db.TaskItems
        .Where(t => t.PlayerId == playerId)
        .Include(t => t.Game)
        .Include(t => t.Letter)
        .OrderByDescending(t => t.AssignedAt)
        .Select(t => new 
        {
            taskId = t.Id,
            gameId = t.GameId,
            gameName = t.Game!.Name,
            letterId = t.LetterId,
            letterCode = t.Letter!.Code,
            note = t.Note,
            status = t.Status,
            assignedAt = t.AssignedAt,
            dueAt = t.DueAt
        })
        .ToListAsync();

    return Results.Ok(tasks);
})
.WithTags("Tasks")
.WithName("GetAllPlayerTasks");


    }
}
