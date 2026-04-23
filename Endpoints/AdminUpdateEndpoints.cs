using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class AdminUpdateEndpoints
{
    public static void MapAdminUpdateEndpoints(this WebApplication app)
    {
        app.MapPost("/api/admin/rename-lookups", async (AppDbContext db) =>
        {
            // 1) Difficulty levels
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

            // 2) Games
            var games = await db.Games.ToListAsync();

            foreach (var game in games)
            {
                game.Name = game.Id switch
                {
                    8  => "Cümle S1 - Cümle Kur!",
                    9  => "Cümle S2 - Fill Gap",
                    10 => "Cümle S3 - Story",

                    1  => "Hece S1 - 2 Harfliler",
                    2  => "Hece S2 - 3 Harfliler",
                    3  => "Hece S3 - 4 Harfliler",

                    4  => "Kelime S1 - Hafıza Kartı",
                    5  => "Kelime S2 - Üçlü Eşleştir",
                    6  => "Kelime S3 - Mesleği Bul",
                    7  => "Kelime S3 - Gölgesini Bul",

                    _ => game.Name
                };
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
                message = "Difficulty level ve game isimleri başarıyla güncellendi.",
                difficultyLevels = updatedDifficultyLevels,
                games = updatedGames
            });
        })
        .WithTags("Admin")
        .WithName("RenameLookups");
    }
}