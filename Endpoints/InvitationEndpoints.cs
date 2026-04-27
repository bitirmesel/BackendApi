using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class InvitationEndpoints
{
    // Davet kodu oluşturulurken kullanılacak karakter seti (karışıklık yaratacak karakterler çıkarıldı)
    private const string CodeCharset = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static void MapInvitationEndpoints(this WebApplication app)
    {
        // ---------------------------------------------------------------
        // [1] POST /api/invitations — Terapist yeni davet kodu oluşturur
        // ---------------------------------------------------------------
        app.MapPost("/api/invitations", async (CreateInvitationRequest req, AppDbContext db) =>
        {
            if (req.TherapistId <= 0)
                return Results.BadRequest(new { error = "Geçersiz therapistId." });

            // Terapist var mı?
            var therapist = await db.Therapists.FindAsync(req.TherapistId);
            if (therapist == null)
                return Results.NotFound(new { error = "Terapist bulunamadı." });

            // PENDING kod sayısı >= 10 ise kısıtla
            var pendingCount = await db.InvitationCodes
                .CountAsync(ic => ic.TherapistId == req.TherapistId && ic.Status == "PENDING");

            if (pendingCount >= 10)
                return Results.Json(
                    new { error = "Maksimum 10 aktif davet kodunuz olabilir. Önce mevcut kodları iptal edin." },
                    statusCode: 429);

            // Benzersiz 6 karakterlik kod üret (max 5 deneme)
            string code = string.Empty;
            var random = new Random();
            for (int attempt = 0; attempt < 5; attempt++)
            {
                var candidate = new string(Enumerable.Range(0, 6)
                    .Select(_ => CodeCharset[random.Next(CodeCharset.Length)])
                    .ToArray());

                bool exists = await db.InvitationCodes.AnyAsync(ic => ic.Code == candidate);
                if (!exists)
                {
                    code = candidate;
                    break;
                }
            }

            if (string.IsNullOrEmpty(code))
                return Results.Json(new { error = "Kod üretimi başarısız. Tekrar deneyin." }, statusCode: 500);

            var now = DateTime.UtcNow;
            var invitation = new InvitationCode
            {
                Code = code,
                TherapistId = req.TherapistId,
                Status = "PENDING",
                CreatedAt = now,
                ExpiresAt = now.AddHours(48),
            };

            db.InvitationCodes.Add(invitation);
            await db.SaveChangesAsync();

            return Results.Created($"/api/invitations/{invitation.Id}", new
            {
                id = invitation.Id,
                code = invitation.Code,
                therapistId = invitation.TherapistId,
                status = invitation.Status,
                createdAt = invitation.CreatedAt,
                expiresAt = invitation.ExpiresAt,
            });
        })
        .WithTags("Invitations")
        .WithName("CreateInvitation");

        // ---------------------------------------------------------------
        // [2] GET /api/invitations?therapistId={id}
        // Terapistin son 7 günkü kodları (EXPIRED hariç)
        // ---------------------------------------------------------------
        app.MapGet("/api/invitations", async (long therapistId, AppDbContext db) =>
        {
            if (therapistId <= 0)
                return Results.BadRequest(new { error = "Geçersiz therapistId." });

            var since = DateTime.UtcNow.AddDays(-7);
            var now = DateTime.UtcNow;

            var codes = await db.InvitationCodes
                .Where(ic => ic.TherapistId == therapistId
                             && ic.CreatedAt >= since
                             && ic.Status != "EXPIRED")
                .OrderByDescending(ic => ic.CreatedAt)
                .Select(ic => new
                {
                    id = ic.Id,
                    code = ic.Code,
                    status = ic.Status,
                    expiresAt = ic.ExpiresAt,
                    remainingMinutes = ic.Status == "PENDING"
                        ? (long)Math.Max(0, (ic.ExpiresAt - now).TotalMinutes)
                        : 0L,
                    usedByPlayerName = ic.UsedByPlayer != null ? ic.UsedByPlayer.Name : (string?)null,
                    usedAt = ic.UsedAt,
                })
                .ToListAsync();

            return Results.Ok(codes);
        })
        .WithTags("Invitations")
        .WithName("GetInvitations");

        // ---------------------------------------------------------------
        // [3] DELETE /api/invitations/{id} — PENDING kodu iptal et
        // ---------------------------------------------------------------
        app.MapDelete("/api/invitations/{id:long}", async (long id, AppDbContext db) =>
        {
            var invitation = await db.InvitationCodes.FindAsync(id);
            if (invitation == null)
                return Results.NotFound(new { error = "Davet kodu bulunamadı." });

            if (invitation.Status != "PENDING")
                return Results.Conflict(new { error = "Sadece PENDING durumdaki kodlar iptal edilebilir." });

            invitation.Status = "CANCELLED";
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Davet kodu iptal edildi." });
        })
        .WithTags("Invitations")
        .WithName("CancelInvitation");

        // ---------------------------------------------------------------
        // [4] POST /api/invitations/redeem — Danışan kodu kullanır
        // IP başına dakikada max 10 istek (basit in-memory rate limiting)
        // ---------------------------------------------------------------
        app.MapPost("/api/invitations/redeem", async (
            RedeemInvitationRequest req,
            AppDbContext db,
            HttpContext httpCtx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Code) || req.PlayerId <= 0)
                return Results.BadRequest(new { error = "Geçersiz istek. Code ve playerId zorunludur." });

            // [1] Kodu bul
            var invitation = await db.InvitationCodes
                .Include(ic => ic.Therapist)
                .FirstOrDefaultAsync(ic => ic.Code == req.Code.Trim().ToUpper());

            if (invitation == null)
                return Results.NotFound(new { error = "Geçersiz kod." });

            // [2] Status kontrolü
            if (invitation.Status != "PENDING")
                return Results.Conflict(new { error = "Kod kullanılmış veya iptal edilmiş." });

            // [3] Süre kontrolü
            if (invitation.ExpiresAt < DateTime.UtcNow)
            {
                invitation.Status = "EXPIRED";
                await db.SaveChangesAsync();
                return Results.Json(new { error = "Kodun süresi dolmuş." }, statusCode: 410);
            }

            // [4] Player var mı?
            var player = await db.Players.FindAsync(req.PlayerId);
            if (player == null)
                return Results.NotFound(new { error = "Danışan bulunamadı." });

            // [5] Bu player-therapist çifti için aktif connection_request var mı?
            bool activeRequestExists = await db.ConnectionRequests
                .AnyAsync(cr => cr.PlayerId == req.PlayerId
                                && cr.TherapistId == invitation.TherapistId
                                && cr.Status == "PENDING");

            if (activeRequestExists)
                return Results.Conflict(new { error = "Bu terapiste zaten bekleyen bir bağlantı isteğiniz var." });

            // [6] Zaten bağlı mı?
            bool alreadyLinked = await db.TherapistClients
                .AnyAsync(tc => tc.TherapistId == invitation.TherapistId && tc.PlayerId == req.PlayerId);

            if (alreadyLinked)
                return Results.Conflict(new { error = "Zaten bağlısınız." });

            // [7] Transaction: connection_request ekle, kodu USED yap, terapiste bildirim gönder
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var connectionRequest = new ConnectionRequest
                {
                    PlayerId = req.PlayerId,
                    TherapistId = invitation.TherapistId,
                    InvitationId = invitation.Id,
                    Status = "PENDING",
                    CreatedAt = DateTime.UtcNow,
                };
                db.ConnectionRequests.Add(connectionRequest);

                invitation.Status = "USED";
                invitation.UsedAt = DateTime.UtcNow;
                invitation.UsedByPlayerId = req.PlayerId;

                var notification = new Notification
                {
                    PlayerId = req.PlayerId,
                    TherapistId = invitation.TherapistId,
                    Message = $"Yeni bağlantı isteği: {player.Name}",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                };
                db.Notifications.Add(notification);

                await db.SaveChangesAsync();
                await tx.CommitAsync();

                return Results.Created($"/api/connection-requests/{connectionRequest.Id}", new
                {
                    connectionRequestId = connectionRequest.Id,
                    therapistId = invitation.TherapistId,
                    therapistName = invitation.Therapist.Name,
                    therapistClinicName = invitation.Therapist.ClinicName,
                    status = connectionRequest.Status,
                    message = "Bağlantı isteğiniz gönderildi. Onay bekleniyor.",
                });
            }
            catch
            {
                await tx.RollbackAsync();
                return Results.Json(new { error = "İşlem sırasında hata oluştu. Tekrar deneyin." }, statusCode: 500);
            }
        })
        .WithTags("Invitations")
        .WithName("RedeemInvitation")
        .AddEndpointFilter<RedeemRateLimitFilter>();
    }
}

// ---------------------------------------------------------------
// Request DTOs
// ---------------------------------------------------------------
public record CreateInvitationRequest(long TherapistId);
public record RedeemInvitationRequest(string Code, long PlayerId);

// ---------------------------------------------------------------
// Rate Limit Filter: IP başına dakikada max 10 /redeem isteği
// ---------------------------------------------------------------
public class RedeemRateLimitFilter : IEndpointFilter
{
    // Statik: uygulama genelinde paylaşılır
    private static readonly Dictionary<string, (int Count, DateTime WindowStart)> _ipWindows = new();
    private static readonly object _lock = new();
    private const int MaxRequests = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var httpCtx = ctx.HttpContext;
        var ip = httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;

        lock (_lock)
        {
            if (_ipWindows.TryGetValue(ip, out var entry))
            {
                if (now - entry.WindowStart > Window)
                {
                    // Yeni pencere
                    _ipWindows[ip] = (1, now);
                }
                else if (entry.Count >= MaxRequests)
                {
                    httpCtx.Response.StatusCode = 429;
                    httpCtx.Response.WriteAsync("{\"error\":\"Çok fazla istek. Lütfen bir dakika bekleyin.\"}").GetAwaiter().GetResult();
                    return ValueTask.FromResult<object?>(null);
                }
                else
                {
                    _ipWindows[ip] = (entry.Count + 1, entry.WindowStart);
                }
            }
            else
            {
                _ipWindows[ip] = (1, now);
            }
        }

        return await next(ctx);
    }
}
