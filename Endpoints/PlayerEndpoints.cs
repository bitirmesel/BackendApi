using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app)
    {
        // Liste – StudentsScreen: GET http://.../api/students
        app.MapGet("/api/students", async (AppDbContext db) =>
        {
            var players = await db.Players
                .Include(p => p.Tasks)
                .ToListAsync();

            var result = players.Select(p => new
            {
                id = p.Id,
                advisorId = p.AdvisorId,
                name = p.Name,
                age = p.Age,
                level = p.Level,
                score = p.Score,
                lastActive = p.LastActive.ToString("O"), // ISO 8601
                activeTasks = p.Tasks.Count(t => t.Status != "Tamamlandı")
            });

            return Results.Ok(result);
        });

        // Öğrenci oluştur – StudentsScreen add dialog: POST /api/students
        app.MapPost("/api/students", async (AppDbContext db, Player player) =>
        {
            player.LastActive = DateTime.UtcNow;
            db.Players.Add(player);
            await db.SaveChangesAsync();
            return Results.Created($"/api/students/{player.Id}", player);
        });

        // Öğrenci detay – gerekirse
        app.MapGet("/api/students/{id:int}", async (int id, AppDbContext db) =>
        {
            var p = await db.Players.FindAsync(id);
            if (p is null) return Results.NotFound();
            return Results.Ok(p);
        });

        // NOTLAR
        app.MapGet("/api/students/{id:int}/notes", async (int id, AppDbContext db) =>
        {
            var notes = await db.Notes
                .Where(n => n.PlayerId == id)
                .OrderByDescending(n => n.Id)
                .Select(n => new
                {
                    date = n.Date,
                    text = n.Text
                })
                .ToListAsync();

            return Results.Ok(notes);
        });

        app.MapPost("/api/students/{id:int}/notes", async (int id, AddNoteRequest req, AppDbContext db) =>
        {
            var player = await db.Players.FindAsync(id);
            if (player is null) return Results.NotFound();

            var note = new Note
            {
                PlayerId = id,
                Text = req.Text,
                Date = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
            };

            db.Notes.Add(note);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // ROZETLER
        app.MapGet("/api/students/{id:int}/badges", async (int id, AppDbContext db) =>
        {
            var badges = await db.Badges
                .Where(b => b.PlayerId == id)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new
                {
                    title = b.Title,
                    icon = b.Icon
                })
                .ToListAsync();

            return Results.Ok(badges);
        });

        app.MapPost("/api/students/{id:int}/badges", async (int id, AddBadgeRequest req, AppDbContext db) =>
        {
            var player = await db.Players.FindAsync(id);
            if (player is null) return Results.NotFound();

            var badge = new Badge
            {
                PlayerId = id,
                Title = req.Title,
                Icon = req.Icon
            };
            db.Badges.Add(badge);
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }
}
