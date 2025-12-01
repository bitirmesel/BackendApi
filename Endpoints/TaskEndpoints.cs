using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        // Bir öğrencinin görevlerini listele – GET /api/tasks/{studentId}
        app.MapGet("/api/tasks/{studentId:int}", async (int studentId, AppDbContext db) =>
        {
            var tasks = await db.TaskItems
                .Where(t => t.PlayerId == studentId)
                .OrderByDescending(t => t.Id)
                .Select(t => new
                {
                    id = t.Id,
                    title = t.Title,
                    date = t.Date,
                    status = t.Status
                })
                .ToListAsync();

            return Results.Ok(tasks);
        });

        // Yeni görev oluştur – StudentsDetailScreen POST /api/tasks
        app.MapPost("/api/tasks", async (CreateTaskRequest req, AppDbContext db) =>
        {
            var player = await db.Players.FindAsync(req.StudentId);
            if (player is null) return Results.BadRequest("Öğrenci bulunamadı");

            var task = new TaskItem
            {
                PlayerId = req.StudentId,
                Title = req.Title,
                Date = req.Date,
                Status = req.Status
            };

            db.TaskItems.Add(task);
            await db.SaveChangesAsync();

            return Results.Created($"/api/tasks/{task.Id}", task);
        });

        // Görev güncelle (durum değiştirme vb.) – isteğe bağlı
        app.MapPut("/api/tasks/{id:int}", async (int id, CreateTaskRequest req, AppDbContext db) =>
        {
            var task = await db.TaskItems.FindAsync(id);
            if (task is null) return Results.NotFound();

            task.Title = req.Title;
            task.Status = req.Status;
            task.Date = req.Date;

            await db.SaveChangesAsync();
            return Results.Ok(task);
        });
    }
}
