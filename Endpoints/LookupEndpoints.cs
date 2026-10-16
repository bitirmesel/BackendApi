using DktApi.Models.Db;
using DktApi.Models.Game;
using DktApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace DktApi.Endpoints;

public static class LookupEndpoints
{
    public static void MapLookupEndpoints(this WebApplication app)
    {
        // ----------------------------------------------------
        // GET /api/lookup/letters (Harfleri Listele)
        // ----------------------------------------------------
        app.MapGet("/api/lookup/letters", async (AppDbContext db) =>
        {
            var letters = await db.Letters
                // HATA DÜZELTMESİ: 'Symbol' yerine 'Code' kullanıldı
                .OrderBy(l => l.Code) 
                .Select(l => new 
                {
                    l.Id,
                    l.Code,
                    l.DisplayName
                })
                .ToListAsync();

            return Results.Ok(letters);
        }).WithTags("Lookup").WithName("GetLetters");

        // ----------------------------------------------------
        // GET /api/lookup/games (Oyunları Listele)
        // ----------------------------------------------------
        app.MapGet("/api/lookup/games", async (AppDbContext db) =>
        {
            // İlişkili tabloları (GameType ve DifficultyLevel) dahil ederek oyunları çekeriz.
            var games = await db.Games
                .Include(g => g.GameType)
                .Include(g => g.DifficultyLevel)
                .Select(g => new GameLookupDto // Sadeleştirilmiş DTO'ya map ederiz
                {
                    Id = g.Id,
                    Name = g.Name,
                    GameTypeName = g.GameType.Name,
                    DifficultyLevelName = g.DifficultyLevel.Name,
                    DifficultyLevel = g.DifficultyLevel.Level
                })
                .OrderBy(g => g.Name)
                .ToListAsync();

            // Atanabilirlik kod tarafında (GameAvailability); SQL'e çevrilmez
            foreach (var g in games)
                g.Assignable = GameAvailability.IsPlayable(g.Id);

            return Results.Ok(games);
        }).WithTags("Lookup").WithName("GetGames");

        // ----------------------------------------------------
        // GET /api/lookup/games/{gameId}/letters
        // Bu oyunun görev olarak atanabileceği harfler (içeriği olanlar).
        // Oyun Fonem'de yoksa boş liste döner.
        // ----------------------------------------------------
        app.MapGet("/api/lookup/games/{gameId:long}/letters", async (long gameId, AppDbContext db) =>
        {
            if (!await db.Games.AnyAsync(g => g.Id == gameId))
                return Results.NotFound(new { message = GameAvailability.Describe("game_not_found") });

            if (!GameAvailability.IsPlayable(gameId))
                return Results.Ok(Array.Empty<object>());

            var letters = db.Letters.AsQueryable();
            if (GameAvailability.RequiresAssetSet(gameId))
                letters = letters.Where(l => db.AssetSets.Any(a => a.GameId == gameId && a.LetterId == l.Id));

            var result = await letters
                .OrderBy(l => l.Code)
                .Select(l => new { l.Id, l.Code, l.DisplayName })
                .ToListAsync();

            return Results.Ok(result);
        }).WithTags("Lookup").WithName("GetAssignableLettersForGame");
        
        // ----------------------------------------------------
        // Opsiyonel: GET /api/lookup/gametypes (Oyun Tiplerini Listele)
        // ----------------------------------------------------
         app.MapGet("/api/lookup/gametypes", async (AppDbContext db) =>
        {
            var gameTypes = await db.GameTypes
                .Select(gt => new { gt.Id, gt.Code, gt.Name })
                .OrderBy(gt => gt.Name)
                .ToListAsync();
            
            return Results.Ok(gameTypes);
        }).WithTags("Lookup").WithName("GetGameTypes");
        
        // ----------------------------------------------------
        // Opsiyonel: GET /api/lookup/difficultylevels (Zorluk Seviyelerini Listele)
        // ----------------------------------------------------
        app.MapGet("/api/lookup/difficultylevels", async (AppDbContext db) =>
        {
            var difficultyLevels = await db.DifficultyLevels
                .Select(dl => new { dl.Id, dl.Level, dl.Name })
                .OrderBy(dl => dl.Level)
                .ToListAsync();
            
            return Results.Ok(difficultyLevels);
        }).WithTags("Lookup").WithName("GetDifficultyLevels");
    }
}