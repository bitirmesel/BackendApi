using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class AdminUpdateEndpoints
{
    public static void MapAdminUpdateEndpoints(this WebApplication app)
    {
        app.MapPost("/api/admin/fix-lookups-and-games", async (AppDbContext db) =>
        {
            // ------------------------------------------------
            // 1) Difficulty level isimlerini düzelt
            // ------------------------------------------------
            var difficultyLevels = await db.DifficultyLevels.ToListAsync();

            foreach (var dl in difficultyLevels)
            {
                dl.Name = dl.Level switch
                {
                    1 => "Kolay",
                    2 => "Orta",
                    3 => "Zor",
                    _ => dl.Name
                };
            }

            // ------------------------------------------------
            // 2) Game type isimlerini düzelt
            // Not: Burada ID'ler varsayımsal olarak:
            // 1 = Hece, 2 = Kelime, 3 = Cümle
            // Eğer sende farklıysa bu mapping'i değiştir.
            // ------------------------------------------------
            var gameTypes = await db.GameTypes.ToListAsync();

            foreach (var gt in gameTypes)
            {
                gt.Name = gt.Id switch
                {
                    1 => "Hece",
                    2 => "Kelime",
                    3 => "Cümle",
                    _ => gt.Name
                };
            }

            await db.SaveChangesAsync();

            // ------------------------------------------------
            // 3) Gerekli GameType ID'lerini bul
            // ID'ler farklıysa isim/code üzerinden bulsun diye güvenli yaptık
            // ------------------------------------------------
            var heceType = await db.GameTypes
                .FirstOrDefaultAsync(x =>
                    x.Name == "Hece" ||
                    x.Code == "SYLLABLE" ||
                    x.Code == "HECE");

            var kelimeType = await db.GameTypes
                .FirstOrDefaultAsync(x =>
                    x.Name == "Kelime" ||
                    x.Code == "WORD" ||
                    x.Code == "KELIME");

            var cumleType = await db.GameTypes
                .FirstOrDefaultAsync(x =>
                    x.Name == "Cümle" ||
                    x.Code == "SENTENCE" ||
                    x.Code == "CUMLE");

            if (heceType is null || kelimeType is null || cumleType is null)
            {
                return Results.BadRequest(new
                {
                    message = "GameType kayıtları eksik. Hece/Kelime/Cümle type kayıtlarını kontrol et."
                });
            }

            // ------------------------------------------------
            // 4) Oyunları düzelt
            // ------------------------------------------------
            var games = await db.Games.ToListAsync();

            foreach (var game in games)
            {
                switch (game.Id)
                {
                    // Hece
                    case 1:
                        game.Name = "Hece S1 - 2 Harfliler";
                        game.GameTypeId = heceType.Id;
                        game.DifficultyLevelId = 1;
                        break;
                    case 2:
                        game.Name = "Hece S2 - 3 Harfliler";
                        game.GameTypeId = heceType.Id;
                        game.DifficultyLevelId = 2;
                        break;
                    case 3:
                        game.Name = "Hece S3 - 4 Harfliler";
                        game.GameTypeId = heceType.Id;
                        game.DifficultyLevelId = 3;
                        break;

                    // Kelime
                    case 4:
                        game.Name = "Kelime S1 - Hafıza Kartı";
                        game.GameTypeId = kelimeType.Id;
                        game.DifficultyLevelId = 1;
                        break;
                    case 5:
                        game.Name = "Kelime S2 - Üçlü Eşleştir";
                        game.GameTypeId = kelimeType.Id;
                        game.DifficultyLevelId = 2;
                        break;
                    case 6:
                        game.Name = "Kelime S3 - Mesleği Bul";
                        game.GameTypeId = kelimeType.Id;
                        game.DifficultyLevelId = 3;
                        break;
                    case 7:
                        game.Name = "Kelime S3 - Gölgesini Bul";
                        game.GameTypeId = kelimeType.Id;
                        game.DifficultyLevelId = 3;
                        break;

                    // Cümle
                    case 8:
                        game.Name = "Cümle S1 - Cümle Kur!";
                        game.GameTypeId = cumleType.Id;
                        game.DifficultyLevelId = 1;
                        break;
                    case 9:
                        game.Name = "Cümle S2 - Fill Gap";
                        game.GameTypeId = cumleType.Id;
                        game.DifficultyLevelId = 2;
                        break;
                    case 10:
                        game.Name = "Cümle S3 - Story";
                        game.GameTypeId = cumleType.Id;
                        game.DifficultyLevelId = 3;
                        break;
                }
            }

            // ------------------------------------------------
            // 5) id=10 yoksa oluştur
            // ------------------------------------------------
            var game10 = await db.Games.FirstOrDefaultAsync(g => g.Id == 10);
            if (game10 is null)
            {
                game10 = new Game
                {
                    Id = 10,
                    Name = "Cümle S3 - Story",
                    GameTypeId = cumleType.Id,
                    DifficultyLevelId = 3,
                    CreatedAt = DateTime.UtcNow
                };

                db.Games.Add(game10);
            }

            await db.SaveChangesAsync();

            // ------------------------------------------------
            // 6) Final çıktı
            // ------------------------------------------------
            var updatedGames = await db.Games
                .Include(g => g.GameType)
                .Include(g => g.DifficultyLevel)
                .OrderBy(g => g.Id)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    GameTypeName = g.GameType.Name,
                    DifficultyLevelName = g.DifficultyLevel.Name,
                    DifficultyLevel = g.DifficultyLevel.Level
                })
                .ToListAsync();

            return Results.Ok(new
            {
                message = "Lookup ve game kayıtları başarıyla düzeltildi.",
                games = updatedGames
            });
        })
        .WithTags("Admin")
        .WithName("FixLookupsAndGames");
    }
}