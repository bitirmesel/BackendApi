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
    }
}