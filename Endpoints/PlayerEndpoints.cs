using DktApi.Models.Db;
using DktApi.Dtos.Player; // CreateStudentRequest burada
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app)
    {
        // ----------------------------------------------------
        // 1) Öğrenci Listesi
        // GET /api/students?therapistId=1
        // ----------------------------------------------------
        app.MapGet("/api/students", async ([FromQuery] long therapistId, AppDbContext db) =>
        {
            // Bu terapiste bağlı öğrencileri therapist_clients üzerinden alıyoruz
            var playersQuery = db.TherapistClients
                .Where(tc => tc.TherapistId == therapistId)
                .Include(tc => tc.Player)
                    .ThenInclude(p => p.Tasks);

            var list = await playersQuery.ToListAsync();

            var result = list.Select(tc =>
            {
                var p = tc.Player;

                var activeTasksCount = p.Tasks.Count(t => t.Status != "COMPLETED");

                return new
                {
                    id = p.Id,
                    name = p.Name,
                    score = p.TotalScore ?? 0,
                    lastActive = p.LastLogin,          // ISO'ya serialize edilir
                    activeTasks = activeTasksCount,
                    therapistId = therapistId,         // backend kullanımı için
                    advisorId = therapistId            // Flutter'ın beklediği isim
                };
            });

            return Results.Ok(result);
        })
        .WithTags("Students")
        .WithName("GetStudents");

        // ----------------------------------------------------
        // 2) Yeni Öğrenci Oluştur
        // POST /api/students
        // Body: CreateStudentRequest
        // ----------------------------------------------------
        app.MapPost("/api/students", async ([FromBody] CreateStudentRequest req, AppDbContext db) =>
        {
            // Terapist var mı kontrol et
            var therapist = await db.Therapists.FindAsync(req.TherapistId);
            if (therapist is null)
            {
                return Results.BadRequest("Therapist not found.");
            }

            var now = DateTime.UtcNow;

            // Player kaydı
            var player = new Player
            {
                Name = req.Name,
                Nickname = string.IsNullOrWhiteSpace(req.Nickname) ? req.Name : req.Nickname,
                Email = req.Email,
                Password = req.Password,
                BirthDate = req.BirthDate,
                Gender = req.Gender,
                Diagnosis = req.Diagnosis,
                ParentName = req.ParentName,
                ParentPhone = req.ParentPhone,
                City = req.City,
                SchoolName = req.SchoolName,
                Abouts = req.Abouts,
                CreatedAt = now,
                UpdatedAt = now,
                LastLogin = now,
                TotalScore = 0
            };

            db.Players.Add(player);
            await db.SaveChangesAsync();

            // Therapist-Player ilişki kaydı (therapist_clients)
            var link = new TherapistClient
            {
                TherapistId = req.TherapistId,
                PlayerId = player.Id
            };

            db.TherapistClients.Add(link);
            await db.SaveChangesAsync();

            // Frontend için dönen minimal response
            var response = new
            {
                id = player.Id,
                name = player.Name,
                therapistId = req.TherapistId,
                advisorId = req.TherapistId
            };

            return Results.Created($"/api/students/{player.Id}", response);
        })
        .WithTags("Students")
        .WithName("CreateStudent");

        // ----------------------------------------------------
        // 3) Öğrenci Detay
        // GET /api/students/{id}
        // ----------------------------------------------------
        app.MapGet("/api/students/{id:long}", async (long id, AppDbContext db) =>
        {
            var player = await db.Players.FindAsync(id);
            if (player is null)
                return Results.NotFound();

            return Results.Ok(player);
        })
        .WithTags("Students")
        .WithName("GetStudentDetail");

        // ----------------------------------------------------
// 4) TÜM PLAYER'LAR (DEBUG / ADMIN)
// GET /api/players
// ----------------------------------------------------
app.MapGet("/api/players", async (AppDbContext db) =>
{
    var players = await db.Players
        .OrderBy(p => p.Id)
        .Select(p => new
        {
            id = p.Id,
            name = p.Name,
            email = p.Email,
            lastLogin = p.LastLogin,
            totalScore = p.TotalScore
        })
        .ToListAsync();

    return Results.Ok(players);
})
.WithTags("Players")
.WithName("GetAllPlayers");

    }

    
}
