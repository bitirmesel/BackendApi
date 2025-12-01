using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        // 1) Öğrenci istatistikleri – GET /api/students/{id}/stats
        app.MapGet("/api/students/{id:int}/stats", async (int id, AppDbContext db) =>
        {
            var player = await db.Players
                .Include(p => p.Tasks)
                .Include(p => p.Badges)
                .Include(p => p.GameSessions)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (player is null) return Results.NotFound();

            var completedTasks = player.Tasks.Count(t => t.Status == "Tamamlandı");
            var totalTasks = player.Tasks.Count;
            var progressPercentage = totalTasks == 0
                ? 0
                : (int)Math.Round((double)completedTasks / totalTasks * 100);

            // Demo: son 4 hafta için fake data (gerçek senaryoda GameSession tarihine göre hesaplanır)
            var weeklyProgress = new List<int> { 20, 40, 60, progressPercentage };

            // Skills demo (gerçek projede başka tablolardan)
            var skills = new Dictionary<string, double>
            {
                { "Harf Farkındalığı", 0.7 },
                { "Heceleme", 0.5 },
                { "Kelime Üretimi", 0.6 },
                { "Cümle Akıcılığı", 0.4 }
            };

            var dto = new
            {
                progressPercentage,
                completedTasks,
                badgeCount = player.Badges.Count,
                weeklyProgress,
                skills
            };

            return Results.Ok(dto);
        });

        // 2) Bildirimler – GET /api/dashboard/notifications?therapistId=1
        app.MapGet("/api/dashboard/notifications", async (int therapistId, AppDbContext db) =>
        {
            var notifs = await db.Notifications
                .Where(n => n.TherapistId == therapistId)
                .OrderByDescending(n => n.Id)
                .Select(n => new
                {
                    title = n.Title,
                    message = n.Message,
                    time = n.Time,
                    type = n.Type,
                    unread = n.Unread
                })
                .ToListAsync();

            return Results.Ok(notifs);
        });
    }
}
