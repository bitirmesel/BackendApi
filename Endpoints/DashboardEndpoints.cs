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
        app.MapGet("/api/dashboard/summary", async (long? therapistId, string? selectedDate, AppDbContext db) =>
{
    if (therapistId is null)
        return Results.BadRequest("TherapistId gereklidir.");

    var id = therapistId.Value;

    var therapist = await db.Therapists.FirstOrDefaultAsync(t => t.Id == id);
    if (therapist is null)
        return Results.NotFound("Therapist not found");

    var studentIds = await db.TherapistClients
        .Where(tc => tc.TherapistId == id)
        .Select(tc => tc.PlayerId)
        .Distinct()
        .ToListAsync();

    var totalStudents = studentIds.Count;

    // ── KRİTİK TARİH SEÇİMİ GÜNCELLEMESİ ──
    // Eğer Flutter'dan bir tarih gelirse onu baz al, gelmezse bugünün tarihini (UtcNow) kullan.
    DateTime targetDate = DateTime.UtcNow;
    if (!string.IsNullOrEmpty(selectedDate) && DateTime.TryParse(selectedDate, out var parsedDate))
    {
        targetDate = parsedDate.ToUniversalTime();
    }

    // Seçilen tarihin 7 gün öncesini ve haftalık başlangıcını hesapla
    var weekAgo = targetDate.AddDays(-7);
    var weekStart = targetDate.Date.AddDays(-6);

    var sessionsLastWeek = await db.GameSessions
        .Where(gs => studentIds.Contains(gs.PlayerId) &&
                     gs.FinishedAt != null &&
                     gs.FinishedAt >= weekAgo &&
                     gs.FinishedAt <= targetDate) // Seçilen hedef tarihe kadar olan aralık
        .ToListAsync();

    var completedThisWeek = sessionsLastWeek.Count;

    // Geri bildirimi olmayan seans sayısı (Tüm zamanlar veya seçili aralık)
    var pendingFeedback = await db.GameSessions
        .Include(gs => gs.Feedbacks)
        .Where(gs => studentIds.Contains(gs.PlayerId) &&
                     gs.FinishedAt != null &&
                     gs.Feedbacks.Count == 0)
        .CountAsync();

    int successRate = 0;
    var scoredSessions = sessionsLastWeek.Where(s => s.MaxScore > 0).ToList();
    if (scoredSessions.Any())
    {
        var avg = scoredSessions.Average(s => (double)s.Score / s.MaxScore);
        successRate = (int)Math.Round(avg * 100);
    }

    var allWeekSessions = await db.GameSessions
        .Where(gs => studentIds.Contains(gs.PlayerId) &&
                     gs.FinishedAt != null &&
                     gs.FinishedAt >= weekStart &&
                     gs.FinishedAt <= targetDate)
        .ToListAsync();

    var weeklyActivity = Enumerable.Range(0, 7)
        .Select(offset =>
        {
            var dayDate = weekStart.AddDays(offset);
            var count = allWeekSessions.Count(s => s.FinishedAt!.Value.Date == dayDate.Date);

            return new
            {
                day = dayDate.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture),
                count
            };
        })
        .ToList();

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