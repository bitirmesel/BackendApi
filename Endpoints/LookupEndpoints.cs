using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class LookupEndpoints
{
    public static void MapLookupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/lookup/letters", async (AppDbContext db) =>
        {
            var letters = await db.Letters
                .OrderBy(l => l.Symbol)
                .ToListAsync();

            return Results.Ok(letters);
        });

        app.MapGet("/api/lookup/games", async (AppDbContext db) =>
        {
            var games = await db.Games.ToListAsync();
            return Results.Ok(games);
        });
    }
}
