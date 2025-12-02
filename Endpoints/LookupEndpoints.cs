using DktApi.Models.Db;
using DktApi.Models.Game;
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
            
            return Results.Ok(games);
        }).WithTags("Lookup").WithName("GetGames");
        
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