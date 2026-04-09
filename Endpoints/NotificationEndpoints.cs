using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        // ----------------------------------------------------
        // 1) BİLDİRİM/GERİ BİLDİRİM GÖNDER – POST /api/notifications
        // Flutter: Terapist 'Geri Bildirim Gönder' butonuna basınca burası çalışır.
        // ----------------------------------------------------
        app.MapPost("/api/notifications", async (Notification req, AppDbContext db) =>
        {
            // Gerekli kontroller
            if (req.PlayerId <= 0 || req.TherapistId <= 0 || string.IsNullOrWhiteSpace(req.Message))
                return Results.BadRequest("Eksik bilgi: PlayerId, TherapistId ve Message zorunludur.");

            req.CreatedAt = DateTime.UtcNow;
            req.IsRead = false;

            db.Notifications.Add(req);
            await db.SaveChangesAsync();

            return Results.Created($"/api/notifications/{req.Id}", req);
        })
        .WithTags("Notifications")
        .WithName("CreateNotification");

        // ----------------------------------------------------
        // 2) ÖĞRENCİYE ÖZEL OKUNMAMIŞ BİLDİRİMLER – GET /api/notifications/player/{id}
        // Unity: Ali oyunu açınca okunmamış mesajlarını buradan çeker.
        // ----------------------------------------------------
        app.MapGet("/api/notifications/player/{playerId:long}", async (long playerId, AppDbContext db) =>
        {
            var notifications = await db.Notifications
                .Where(n => n.PlayerId == playerId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    id = n.Id,
                    message = n.Message,
                    therapistId = n.TherapistId,
                    createdAt = n.CreatedAt,
                    isRead = n.IsRead
                })
                .ToListAsync();

            return Results.Ok(notifications);
        })
        .WithTags("Notifications")
        .WithName("GetPlayerUnreadNotifications");

        // ----------------------------------------------------
        // 3) BİLDİRİMİ OKUNDU İŞARETLE – PATCH /api/notifications/{id}/read
        // Unity: Ali 'Tamam' butonuna basınca bildirim silinmesin ama IsRead = true olsun.
        // ----------------------------------------------------
        // PATCH yerine POST yapıyoruz ki Unity zorlanmasın
        app.MapPost("/api/notifications/{id:long}/read", async (long id, AppDbContext db) =>
        {
            var notification = await db.Notifications.FindAsync(id);
            if (notification == null) return Results.NotFound();

            notification.IsRead = true;
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Okundu işaretlendi" });
        });

        // ----------------------------------------------------
        // 4) DEBUG/ADMIN: TÜM BİLDİRİMLERİ LİSTELE – GET /api/notifications
        // Postman'den tüm tabloyu izlemek için kullanılır.
        // ----------------------------------------------------
        app.MapGet("/api/notifications", async (AppDbContext db, long? playerId) =>
        {
            var query = db.Notifications
                .Include(n => n.Player)
                .Include(n => n.Therapist)
                .AsNoTracking()
                .AsQueryable();

            if (playerId.HasValue)
                query = query.Where(n => n.PlayerId == playerId.Value);

            var result = await query
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    id = n.Id,
                    playerName = n.Player != null ? n.Player.Name : "Bilinmiyor",
                    therapistName = n.Therapist != null ? n.Therapist.Name : "Bilinmiyor",
                    message = n.Message,
                    isRead = n.IsRead,
                    createdAt = n.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(result);
        })
        .WithTags("Notifications")
        .WithName("GetAllNotificationsDebug");

        // ----------------------------------------------------
        // 5) BİLDİRİM SİL – DELETE /api/notifications/{id}
        // İhtiyaç halinde tekil bildirim silme.
        // ----------------------------------------------------
        app.MapDelete("/api/notifications/{id:long}", async (long id, AppDbContext db) =>
        {
            var notification = await db.Notifications.FindAsync(id);
            if (notification == null) return Results.NotFound();

            db.Notifications.Remove(notification);
            await db.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithTags("Notifications")
        .WithName("DeleteNotification");
    }
}