using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;
using System.Linq; 
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace DktApi.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app) 
    {
        // --------------------------------------------------
        // 1) ANA DASHBOARD ÖZETİ
        // URL: GET /api/dashboard/summary?therapistId=...
        // Terapiste bağlı öğrencilerin serbest ve ödev tüm seanslarını kapsar.
        // --------------------------------------------------
        app.MapGet("/api/dashboard/summary", async (long? therapistId, AppDbContext db) =>
        {
            if (therapistId is null)
                return Results.BadRequest("TherapistId gereklidir.");
            
            var id = therapistId.Value;

            var therapist = await db.Therapists
                .FirstOrDefaultAsync(t => t.Id == id);

            if (therapist is null)
                return Results.NotFound("Therapist not found");

            // Terapistin danışanı olan benzersiz öğrenci ID listesi
            var studentIds = await db.TherapistClients
                .Where(tc => tc.TherapistId == id)
                .Select(tc => tc.PlayerId)
                .Distinct()
                .ToListAsync();

            var totalStudents = studentIds.Count;

            var now = DateTime.UtcNow;
            var weekAgo = now.AddDays(-7);
            var weekStart = now.Date.AddDays(-6); // Bugün dahil son 7 günün başlangıcı

            // ── KRİTİK GÜNCELLEME ──
            // Sadece ödevleri (gs.Task != null) değil, bu terapiste bağlı öğrencilerin son 7 gündeki TÜM seanslarını çeker.
            var sessionsLastWeek = await db.GameSessions
                .Where(gs => studentIds.Contains(gs.PlayerId) && 
                             gs.FinishedAt != null && 
                             gs.FinishedAt >= weekAgo)
                .ToListAsync();

            var completedThisWeek = sessionsLastWeek.Count;

            // Geri bildirimi (feedback) olmayan tamamlanmış oturum sayısı
            var pendingFeedback = await db.GameSessions
                .Include(gs => gs.Feedbacks)
                .Where(gs => studentIds.Contains(gs.PlayerId) && 
                             gs.FinishedAt != null && 
                             gs.Feedbacks.Count == 0)
                .CountAsync();

            // Başarı oranı: score / max_score ortalaması
            int successRate = 0;
            var scoredSessions = sessionsLastWeek.Where(s => s.MaxScore > 0).ToList();
            if (scoredSessions.Any())
            {
                var avg = scoredSessions.Average(s => (double)s.Score / s.MaxScore);
                successRate = (int)Math.Round(avg * 100);
            }

            // Haftalık aktivite: son 7 günün her bir günü için { day, count }
            var allWeekSessions = await db.GameSessions
                .Where(gs => studentIds.Contains(gs.PlayerId) && 
                             gs.FinishedAt != null && 
                             gs.FinishedAt >= weekStart)
                .ToListAsync();

            // Gün isimlerini "tr-TR" formatında (Pzt, Sal...) üreterek Flutter fl_chart ile kusursuz eşleştirir.
            var weeklyActivity = Enumerable.Range(0, 7)
                .Select(offset =>
                {
                    var dayDate = weekStart.AddDays(offset);
                    var count = allWeekSessions.Count(s => s.FinishedAt!.Value.Date == dayDate.Date);

                    return new
                    {
                        day = dayDate.ToString("ddd", new System.Globalization.CultureInfo("tr-TR")),
                        count
                    };
                })
                .ToList();

            // DTO olarak döndürülecek anonim nesne
            var dto = new
            {
                advisorName = therapist.Name,
                totalStudents,
                completedThisWeek,
                pendingFeedback,
                successRate,
                weeklyActivity
            };

            return Results.Ok(dto);
        }).WithTags("Dashboard").WithName("GetDashboardSummary");

        // --------------------------------------------------
        // 2) ÖĞRENCİ DETAY İSTATİSTİKLERİ (Önceki Tanım Korundu)
        // GET /api/students/{id}/stats?therapistId=1
        // --------------------------------------------------
        app.MapGet("/api/students/{id:long}/stats", async (long id, long therapistId, AppDbContext db) =>
        {
            var player = await db.Players
                .Include(p => p.Tasks)
                .Include(p => p.GameSessions)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (player is null)
                return Results.NotFound();

            // Bu terapiste ait görevler
            var tasks = player.Tasks
                .Where(t => t.TherapistId == therapistId)
                .ToList();

            var completedTasks = tasks.Count(t => t.Status == "COMPLETED");
            var totalTasks = tasks.Count;

            var progressPercentage = totalTasks == 0
                ? 0
                : (int)Math.Round((double)completedTasks / totalTasks * 100);

            // Son 4 hafta için simple progress (şimdilik dummy)
            var weeklyProgress = new List<int> { 20, 40, 60, progressPercentage };

            var dto = new
            {
                progressPercentage,
                completedTasks,
                totalTasks,
                weeklyProgress
            };

            return Results.Ok(dto);
        }).WithTags("Students").WithName("GetStudentStats");

    }
}