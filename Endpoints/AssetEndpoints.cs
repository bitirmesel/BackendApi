using DktApi.Models.Db;
using DktApi.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class AssetEndpoints
{
    public static void MapAssetEndpoints(this WebApplication app)
    {
        // POST /api/assets/create
        // Bu adrese istek atarak veritabanına asset ekleyeceğiz
        app.MapPost("/api/assets/create", async (CreateAssetSetRequest req, AppDbContext db) =>
        {
            // 1. Önce bu oyun ve harf için daha önce kayıt var mı bakalım?
            var existing = await db.AssetSets
                .FirstOrDefaultAsync(a => a.GameId == req.GameId && a.LetterId == req.LetterId);

            if (existing != null)
            {
                // Varsa güncelleyelim
                existing.AssetJson = req.JsonData;
                existing.CreatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Results.Ok(new { message = "Asset seti GÜNCELLENDİ.", id = existing.Id });
            }

            // 2. Yoksa yenisini oluşturalım
            var newAssetSet = new AssetSet
            {
                GameId = req.GameId,
                LetterId = req.LetterId,
                AssetJson = req.JsonData,
                CreatedAt = DateTime.UtcNow
            };

            db.AssetSets.Add(newAssetSet);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Yeni asset seti OLUŞTURULDU.", id = newAssetSet.Id });
        })
        .WithTags("Assets"); // Swagger'da "Assets" başlığı altında görünsün

        // GET /api/tasks/{taskId}/asset-set
        app.MapGet("/api/tasks/{taskId:long}/asset-set", async (long taskId, AppDbContext db) =>
        {
            var task = await db.TaskItems
                .Include(t => t.AssetSet)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task is null)
                return Results.NotFound("Task not found.");

            AssetSet? assetSet = task.AssetSet;

            // Eğer AssetSetId dolu değilse, game+letter üzerinden asset set bulmayı deneriz
            if (assetSet is null)
            {
                assetSet = await db.AssetSets
                    .FirstOrDefaultAsync(a => a.GameId == task.GameId && a.LetterId == task.LetterId);
            }

            if (assetSet is null)
                return Results.NotFound("Asset set not found for this task.");

            var response = new
            {
                assetSetId = assetSet.Id,
                gameId = assetSet.GameId,
                letterId = assetSet.LetterId,
                json = assetSet.AssetJson
            };

            return Results.Ok(response);
        })
        .WithTags("Assets")
        .WithName("GetAssetSetByTask");

        // ----------------------------------------------------
// DEBUG/ADMIN: TÜM ASSET SET'LER (row row)
// GET /api/asset-sets
// Opsiyonel filtreler: ?gameId=1&letterId=2
// Opsiyonel: ?includeJson=true  (AssetJson büyükse default false kalsın)
// ----------------------------------------------------
app.MapGet("/api/asset-sets", async (
    AppDbContext db,
    long? gameId,
    long? letterId,
    bool includeJson
) =>
{
    var q = db.AssetSets
        .AsNoTracking()
        .Include(a => a.Game)
        .Include(a => a.Letter)
        .AsQueryable();

    if (gameId.HasValue) q = q.Where(a => a.GameId == gameId.Value);
    if (letterId.HasValue) q = q.Where(a => a.LetterId == letterId.Value);

    var items = await q
        .OrderByDescending(a => a.CreatedAt)
        .Select(a => new
        {
            assetSetId = a.Id,
            gameId = a.GameId,
            gameName = a.Game != null ? a.Game.Name : null,

            letterId = a.LetterId,
            letterCode = a.Letter != null ? a.Letter.Code : null,
            letterDisplayName = a.Letter != null ? a.Letter.DisplayName : null,

            createdAt = a.CreatedAt,

            // JSON çok büyük olabileceği için default false
            assetJson = includeJson ? a.AssetJson : null,

            // Referans sayıları (debug için güzel olur)
            tasksCount = a.Tasks.Count,
            sessionsCount = a.GameSessions.Count
        })
        .ToListAsync();

    return Results.Ok(items);
})
.WithTags("Assets")
.WithName("GetAllAssetSetsDebug");

    }

    
}