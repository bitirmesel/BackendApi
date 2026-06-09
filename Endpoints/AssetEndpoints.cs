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
            try
            {
                if (req.GameId <= 0 || req.LetterId <= 0)
                    return Results.BadRequest(new { message = "GameId ve LetterId sıfırdan büyük olmalı." });

                if (string.IsNullOrWhiteSpace(req.JsonData))
                    return Results.BadRequest(new { message = "JsonData boş olamaz." });

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

                // 2. Game ve Letter var mı kontrol et
                var gameExists = await db.Games.AnyAsync(g => g.Id == req.GameId);
                if (!gameExists)
                    return Results.NotFound(new { message = $"GameId={req.GameId} bulunamadı." });

                var letterExists = await db.Letters.AnyAsync(l => l.Id == req.LetterId);
                if (!letterExists)
                    return Results.NotFound(new { message = $"LetterId={req.LetterId} bulunamadı." });

                // 3. Yoksa yenisini oluşturalım
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
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message + " | Inner: " + (ex.InnerException?.Message ?? "-"),
                    statusCode: 500
                );
            }
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

        app.MapGet("/api/asset-sets", async (
    AppDbContext db,
    long? gameId,
    long? letterId,
    bool? includeJson
) =>
{
    bool inc = includeJson ?? false;

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

            assetJson = inc ? a.AssetJson : null,

            // Count'lar bazı EF sürümlerinde sıkıntı çıkarabilir; garanti olsun diye subquery yaptım
            tasksCount = db.TaskItems.Count(t => t.AssetSetId == a.Id),
            sessionsCount = db.GameSessions.Count(s => s.AssetSetId == a.Id)
        })
        .ToListAsync();

    return Results.Ok(items);
})
.WithTags("Assets")
.WithName("GetAllAssetSetsDebug");

        // POST /api/assets/bulk-create
        // Birden fazla asset setini tek seferde eklemek/güncellemek için
        app.MapPost("/api/assets/bulk-create", async (List<CreateAssetSetRequest> reqList, AppDbContext db) =>
        {
            var results = new List<object>();

            foreach (var req in reqList)
            {
                var existing = await db.AssetSets
                    .FirstOrDefaultAsync(a => a.GameId == req.GameId && a.LetterId == req.LetterId);

                if (existing != null)
                {
                    existing.AssetJson = req.JsonData;
                    existing.CreatedAt = DateTime.UtcNow;
                    results.Add(new { gameId = req.GameId, letterId = req.LetterId, status = "GÜNCELLENDI", id = existing.Id });
                }
                else
                {
                    var newAssetSet = new AssetSet
                    {
                        GameId = req.GameId,
                        LetterId = req.LetterId,
                        AssetJson = req.JsonData,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.AssetSets.Add(newAssetSet);
                    results.Add(new { gameId = req.GameId, letterId = req.LetterId, status = "OLUŞTURULDU", id = 0 });
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { message = $"{reqList.Count} asset seti işlendi.", details = results });
        })
        .WithTags("Assets")
        .WithName("BulkCreateAssets");

        // PUT /api/assets/{gameId}/{letterId}/json
        // Mevcut asset set'in JSON'ını entity yüklemeden direkt günceller
        app.MapPut("/api/assets/{gameId:long}/{letterId:long}/json", async (long gameId, long letterId, UpdateAssetJsonRequest req, AppDbContext db) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.JsonData))
                    return Results.BadRequest(new { message = "JsonData boş olamaz." });

                var rowsUpdated = await db.AssetSets
                    .Where(a => a.GameId == gameId && a.LetterId == letterId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(a => a.AssetJson, req.JsonData)
                        .SetProperty(a => a.CreatedAt, DateTime.UtcNow));

                if (rowsUpdated == 0)
                    return Results.NotFound(new { message = $"GameId={gameId}, LetterId={letterId} için kayıt bulunamadı." });

                return Results.Ok(new { message = "Asset JSON güncellendi.", gameId, letterId });
            }
            catch (Exception ex)
            {
                return Results.Json(
                    new { error = ex.Message, inner = ex.InnerException?.Message ?? "-" },
                    statusCode: 500
                );
            }
        })
        .WithTags("Assets")
        .WithName("UpdateAssetJson");

    }


}