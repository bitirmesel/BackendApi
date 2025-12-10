using DktApi.Models.Auth;
using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace DktApi.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // POST /api/auth/login (Giriş)
        
        app.MapPost("/api/auth/login", async (
            [FromBody] LoginRequest req,
            AppDbContext db) =>
        {
            // E-posta ile terapisti bul
            var therapist = await db.Therapists
                .FirstOrDefaultAsync(t => t.Email == req.Email);

            // Kullanıcı yoksa veya şifre yanlışsa (Şimdilik plaintext karşılaştırma)
            if (therapist is null || therapist.Password != req.Password)
            {
                return Results.Unauthorized();
            }
            
            // Eğer isterseniz burada "last_login" tarihini de güncelleyebilirsiniz.

            return Results.Ok(new AuthResponse
            {
                Token = "demo-token", // TODO: JWT'ye geçiş yapılacak
                TherapistId = therapist.Id,
                Name = therapist.Name
            });
        }).WithTags("Auth").WithName("Login");

        
        // POST /api/auth/register (Kayıt)
        
        app.MapPost("/api/auth/register", async (
            [FromBody] RegisterRequest req,
            AppDbContext db) =>
        {
            // 1. E-posta Kontrolü
            var existingTherapist = await db.Therapists
                .AnyAsync(t => t.Email == req.Email);

            if (existingTherapist)
            {
                return Results.BadRequest(new { message = "Bu e-posta adresi zaten kayıtlıdır." });
            }

            // 2. Yeni Terapist Objelerinin Oluşturulması
            var newTherapist = new Therapist
            {
                Name = req.Name,
                Email = req.Email,
                // TODO: Gerçek uygulamada şifre HASH'lenmelidir (örn: Argon2, BCrypt)
                Password = req.Password,
                ClinicName = req.ClinicName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // 3. Veritabanına Ekleme
            db.Therapists.Add(newTherapist);
            await db.SaveChangesAsync();
            
            // 4. Başarılı Yanıt (Flutter'ın beklediği token ve id yapısı)
            return Results.Ok(new AuthResponse
            {
                Token = "demo-token", // TODO: JWT'ye geçiş yapılacak
                TherapistId = newTherapist.Id,
                Name = newTherapist.Name
            });
        }).WithTags("Auth").WithName("Register");

        // PLAYER LOGIN – ÇOCUK UNITY İÇİN
        // POST /api/player/auth/login
        
        app.MapPost("/api/player/auth/login", async (
            [FromBody] PlayerLoginRequest req,
            AppDbContext db) =>
        {
            var player = await db.Players
                .FirstOrDefaultAsync(p => p.Nickname == req.Nickname);

            if (player is null || player.Password != req.Password)
            {
                return Results.Unauthorized();
            }

            // İstersen burada LastLogin güncellemesi de yapabilirsin
            player.LastLogin = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var resp = new PlayerLoginResponse
            {
                PlayerId = player.Id,
                Nickname = player.Nickname,
                TotalScore = player.TotalScore
            };

            return Results.Ok(resp);
        })
        .WithTags("Auth")
        .WithName("PlayerLogin");

    }
}