using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class ConnectionRequestEndpoints
{
    public static void MapConnectionRequestEndpoints(this WebApplication app)
    {
        // ---------------------------------------------------------------
        // [5] GET /api/connection-requests?therapistId={id}&status=PENDING
        // Terapistin bağlantı isteklerini listeler
        // ---------------------------------------------------------------
        app.MapGet("/api/connection-requests", async (long therapistId, string? status, AppDbContext db) =>
        {
            if (therapistId <= 0)
                return Results.BadRequest(new { error = "Geçersiz therapistId." });

            var filterStatus = string.IsNullOrWhiteSpace(status) ? "PENDING" : status.ToUpper();

            var requests = await db.ConnectionRequests
                .Include(cr => cr.Player)
                .Include(cr => cr.Invitation)
                .Where(cr => cr.TherapistId == therapistId && cr.Status == filterStatus)
                .OrderByDescending(cr => cr.CreatedAt)
                .Select(cr => new
                {
                    id = cr.Id,
                    playerId = cr.PlayerId,
                    playerName = cr.Player.Name,
                    playerDiagnosis = cr.Player.Diagnosis,
                    status = cr.Status,
                    createdAt = cr.CreatedAt,
                    invitationCode = cr.Invitation.Code,
                })
                .ToListAsync();

            return Results.Ok(requests);
        })
        .WithTags("ConnectionRequests")
        .WithName("GetConnectionRequests");

        // ---------------------------------------------------------------
        // [6] PATCH /api/connection-requests/{id}/accept — Terapist kabul eder
        // ---------------------------------------------------------------
        app.MapPatch("/api/connection-requests/{id:long}/accept", async (long id, AppDbContext db) =>
        {
            var request = await db.ConnectionRequests
                .Include(cr => cr.Player)
                .FirstOrDefaultAsync(cr => cr.Id == id);

            if (request == null)
                return Results.NotFound(new { error = "Bağlantı isteği bulunamadı." });

            if (request.Status != "PENDING")
                return Results.Conflict(new { error = "Bu istek zaten işlenmiş." });

            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                request.Status = "ACCEPTED";
                request.RespondedAt = DateTime.UtcNow;

                // therapist_clients'a ekle (çakışma olursa yoksay)
                bool alreadyLinked = await db.TherapistClients
                    .AnyAsync(tc => tc.TherapistId == request.TherapistId && tc.PlayerId == request.PlayerId);

                if (!alreadyLinked)
                {
                    db.TherapistClients.Add(new TherapistClient
                    {
                        TherapistId = request.TherapistId,
                        PlayerId = request.PlayerId,
                    });
                }

                // Danışana kabul bildirimi
                db.Notifications.Add(new Notification
                {
                    PlayerId = request.PlayerId,
                    TherapistId = request.TherapistId,
                    Message = "Terapistiniz bağlantı isteğinizi kabul etti.",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                });

                await db.SaveChangesAsync();
                await tx.CommitAsync();

                return Results.Ok(new
                {
                    message = "Kabul edildi.",
                    playerId = request.PlayerId,
                    playerName = request.Player.Name,
                });
            }
            catch
            {
                await tx.RollbackAsync();
                return Results.Json(new { error = "İşlem sırasında hata oluştu." }, statusCode: 500);
            }
        })
        .WithTags("ConnectionRequests")
        .WithName("AcceptConnectionRequest");

        // ---------------------------------------------------------------
        // [7] PATCH /api/connection-requests/{id}/reject — Terapist reddeder
        // ---------------------------------------------------------------
        app.MapPatch("/api/connection-requests/{id:long}/reject", async (long id, AppDbContext db) =>
        {
            var request = await db.ConnectionRequests
                .Include(cr => cr.Player)
                .FirstOrDefaultAsync(cr => cr.Id == id);

            if (request == null)
                return Results.NotFound(new { error = "Bağlantı isteği bulunamadı." });

            if (request.Status != "PENDING")
                return Results.Conflict(new { error = "Bu istek zaten işlenmiş." });

            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                request.Status = "REJECTED";
                request.RespondedAt = DateTime.UtcNow;

                // Danışana ret bildirimi (opsiyonel)
                db.Notifications.Add(new Notification
                {
                    PlayerId = request.PlayerId,
                    TherapistId = request.TherapistId,
                    Message = "Bağlantı isteğiniz reddedildi.",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                });

                await db.SaveChangesAsync();
                await tx.CommitAsync();

                return Results.Ok(new { message = "Reddedildi." });
            }
            catch
            {
                await tx.RollbackAsync();
                return Results.Json(new { error = "İşlem sırasında hata oluştu." }, statusCode: 500);
            }
        })
        .WithTags("ConnectionRequests")
        .WithName("RejectConnectionRequest");
    }
}
