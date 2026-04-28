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
            var result = await db.TherapistClients
                .Where(tc => tc.TherapistId == therapistId && tc.Player != null)
                .Select(tc => new
                {
                    id = tc.Player.Id,
                    name = tc.Player.Name,
                    score = tc.Player.TotalScore ?? 0,
                    lastActive = tc.Player.LastLogin,
                    activeTasks = tc.Player.Tasks.Count(t => t.Status != "COMPLETED"),
                    therapistId = therapistId,
                    advisorId = therapistId
                })
                .ToListAsync();

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
        // Danışan güncelle (kısmi) — PATCH /api/students/{id} veya PATCH /api/players/{id}
        // ----------------------------------------------------
        app.MapPatch("/api/students/{id:long}", PatchPlayerAsync)
            .WithTags("Students")
            .WithName("PatchStudent");

        app.MapPatch("/api/players/{id:long}", PatchPlayerAsync)
            .WithTags("Players")
            .WithName("PatchPlayer");

        // ----------------------------------------------------
        // Danışanın bağlı terapist(ler)i — therapist_clients
        // GET /api/players/{playerId}/therapists
        // ----------------------------------------------------
        app.MapGet("/api/players/{playerId:long}/therapists", async (long playerId, AppDbContext db) =>
        {
            var playerExists = await db.Players.AsNoTracking().AnyAsync(p => p.Id == playerId);
            if (!playerExists)
                return Results.NotFound(new { error = "Danışan bulunamadı." });

            var therapists = await db.TherapistClients
                .AsNoTracking()
                .Where(tc => tc.PlayerId == playerId)
                .OrderBy(tc => tc.TherapistId)
                .Select(tc => new
                {
                    id = tc.Therapist.Id,
                    advisorId = tc.Therapist.Id,
                    name = tc.Therapist.Name,
                    username = tc.Therapist.Username,
                    email = tc.Therapist.Email,
                    phoneNumber = tc.Therapist.PhoneNumber,
                    licenseNumber = tc.Therapist.LicenseNumber,
                    profileImageUrl = tc.Therapist.ProfileImageUrl,
                    clinicName = tc.Therapist.ClinicName,
                    city = tc.Therapist.City,
                    createdAt = tc.Therapist.CreatedAt,
                    updatedAt = tc.Therapist.UpdatedAt
                })
                .ToListAsync();

            return Results.Ok(new
            {
                playerId,
                therapists
            });
        })
        .WithTags("Players")
        .WithName("GetPlayerTherapists");

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

    private static async Task<IResult> PatchPlayerAsync(
        long id,
        [FromBody] UpdatePlayerRequest req,
        AppDbContext db)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == id);
        if (player is null)
            return Results.NotFound(new { error = "Danışan bulunamadı." });

        var changed = false;

        if (req.Name is not null)
        {
            var v = req.Name.Trim();
            if (v.Length == 0)
                return Results.BadRequest(new { error = "İsim boş olamaz." });
            if (player.Name != v)
            {
                player.Name = v;
                changed = true;
            }
        }

        if (req.Nickname is not null)
        {
            var v = req.Nickname.Trim();
            if (v.Length == 0)
                return Results.BadRequest(new { error = "Kullanıcı adı boş olamaz." });
            var nickTaken = await db.Players.AnyAsync(p => p.Id != id && p.Nickname == v);
            if (nickTaken)
                return Results.Conflict(new { error = "Bu kullanıcı adı kullanılıyor." });
            if (player.Nickname != v)
            {
                player.Nickname = v;
                changed = true;
            }
        }

        if (req.Email is not null)
        {
            var v = req.Email.Trim();
            if (v.Length == 0)
                return Results.BadRequest(new { error = "E-posta boş olamaz." });
            var emailTaken = await db.Players.AnyAsync(p => p.Id != id && p.Email == v);
            if (emailTaken)
                return Results.Conflict(new { error = "Bu e-posta başka bir hesapta kayıtlı." });
            if (player.Email != v)
            {
                player.Email = v;
                changed = true;
            }
        }

        if (req.Password is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { error = "Şifre boş olamaz." });
            if (player.Password != req.Password)
            {
                player.Password = req.Password;
                changed = true;
            }
        }

        if (req.BirthDate.HasValue)
        {
            var bd = req.BirthDate.Value;
            if (player.BirthDate != bd)
            {
                player.BirthDate = bd;
                changed = true;
            }
        }

        if (req.Gender is not null)
        {
            var next = string.IsNullOrWhiteSpace(req.Gender) ? null : req.Gender.Trim();
            if (player.Gender != next)
            {
                player.Gender = next;
                changed = true;
            }
        }

        if (req.Diagnosis is not null)
        {
            var next = string.IsNullOrWhiteSpace(req.Diagnosis) ? null : req.Diagnosis.Trim();
            if (player.Diagnosis != next)
            {
                player.Diagnosis = next;
                changed = true;
            }
        }

        if (req.ParentName is not null)
        {
            var next = string.IsNullOrWhiteSpace(req.ParentName) ? null : req.ParentName.Trim();
            if (player.ParentName != next)
            {
                player.ParentName = next;
                changed = true;
            }
        }

        if (req.ParentPhone is not null)
        {
            var next = string.IsNullOrWhiteSpace(req.ParentPhone) ? null : req.ParentPhone.Trim();
            if (player.ParentPhone != next)
            {
                player.ParentPhone = next;
                changed = true;
            }
        }

        if (req.City is not null)
        {
            var next = string.IsNullOrWhiteSpace(req.City) ? null : req.City.Trim();
            if (player.City != next)
            {
                player.City = next;
                changed = true;
            }
        }

        if (req.SchoolName is not null)
        {
            var next = string.IsNullOrWhiteSpace(req.SchoolName) ? null : req.SchoolName.Trim();
            if (player.SchoolName != next)
            {
                player.SchoolName = next;
                changed = true;
            }
        }

        if (req.Abouts is not null)
        {
            var next = string.IsNullOrWhiteSpace(req.Abouts) ? null : req.Abouts.Trim();
            if (player.Abouts != next)
            {
                player.Abouts = next;
                changed = true;
            }
        }

        if (changed)
        {
            player.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Results.Ok(new
        {
            id = player.Id,
            nickname = player.Nickname,
            name = player.Name,
            email = player.Email,
            birthDate = player.BirthDate,
            gender = player.Gender,
            diagnosis = player.Diagnosis,
            parentName = player.ParentName,
            parentPhone = player.ParentPhone,
            city = player.City,
            schoolName = player.SchoolName,
            abouts = player.Abouts,
            createdAt = player.CreatedAt,
            updatedAt = player.UpdatedAt,
            lastLogin = player.LastLogin,
            totalScore = player.TotalScore
        });
    }
}
