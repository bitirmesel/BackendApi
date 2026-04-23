using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class AdminUpdateEndpoints
{
    public static void MapAdminUpdateEndpoints(this WebApplication app)
    {
        app.MapPost("/api/admin/rename-lookups", async (AppDbContext db) =>
        {
            // 1) Difficulty levels -> Türkçeleştir
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

            // 2) Game type adlarını da istersen Türkçeleştir
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

            // 3) Games güncelle
            var games = await db.Games.ToListAsync();

            foreach (var game in games)
            {
                switch (game.Id)
                {
                    // Hece
                    case 1:
                        game.Name = "Hece S1 - 2 Harfliler";
                        game.DifficultyLevelId = 1;
                        break;
                    case 2:
                        game.Name = "Hece S2 - 3 Harfliler";
                        game.DifficultyLevelId = 2;
                        break;
                    case 3:
                        game.Name = "Hece S3 - 4 Harfliler";
                        game.DifficultyLevelId = 3;
                        break;

                    // Kelime
                    case 4:
                        game.Name = "Kelime S1 - Hafıza Kartı";
                        game.DifficultyLevelId = 1;
                        break;
                    case 5:
                        game.Name = "Kelime S2 - Üçlü Eşleştir";
                        game.DifficultyLevelId = 2;
                        break;
                    case 6:
                        game.Name = "Kelime S3 - Mesleği Bul";
                        game.DifficultyLevelId = 3;
                        break;
                    case 7:
                        game.Name = "Kelime S3 - Gölgesini Bul";
                        game.DifficultyLevelId = 3;
                        break;

                    // Cümle
                    case 8:
                        game.Name = "Cümle S1 - Cümle Kur!";
                        game.DifficultyLevelId = 1;
                        break;
                    case 9:
                        game.Name = "Cümle S2 -  Cümle Oyunu 2";
                        game.DifficultyLevelId = 2;
                        break;
                    case 10:
                        game.Name = "Cümle S3 - Cümle Oyunu 3";
                        game.DifficultyLevelId = 3;
                        break;
                }
            }

            await db.SaveChangesAsync();

            var updatedDifficultyLevels = await db.DifficultyLevels
                .OrderBy(x => x.Level)
                .Select(x => new
                {
                    x.Id,
                    x.Level,
                    x.Name
                })
                .ToListAsync();

            var updatedGameTypes = await db.GameTypes
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Name
                })
                .ToListAsync();

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
                message = "Difficulty level, game type ve game isimleri başarıyla güncellendi.",
                difficultyLevels = updatedDifficultyLevels,
                gameTypes = updatedGameTypes,
                games = updatedGames
            });
        })
        .WithTags("Admin")
        .WithName("RenameLookups");
    }
}