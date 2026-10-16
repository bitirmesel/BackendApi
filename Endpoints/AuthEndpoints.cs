using DktApi.Models.Auth;
using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using DktApi.Services;

namespace DktApi.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // POST /api/auth/login (Giriş)

        app.MapPost("/api/auth/login", async (
            [FromBody] LoginRequest req,
            AppDbContext db,
            JwtHelper jwtHelper) =>
        {
            // Email (harf duyarsız, B23) veya Username ile terapisti bul
            var email = EmailNormalizer.Normalize(req.Email);
            var therapist = await db.Therapists
                .FirstOrDefaultAsync(t =>
                    (email != null && t.Email.ToLower() == email) ||
                    (req.Username != null && t.Username == req.Username));

            // Kullanıcı yoksa veya şifre yanlışsa
            if (therapist is null || therapist.Password != req.Password)
            {
                return Results.Unauthorized();
            }
            
            // JWT Token Generate Et
            var token = jwtHelper.GenerateToken(therapist);

            return Results.Ok(new AuthResponse
            {
                Token = token,
                TherapistId = therapist.Id,
                Name = therapist.Name
            });
        }).WithTags("Auth").WithName("Login");


        // POST /api/auth/register (Kayıt)

        app.MapPost("/api/auth/register", async (
            [FromBody] RegisterRequest req,
            AppDbContext db,
            JwtHelper jwtHelper) =>
        {
            // 1. Şifre Doğrulama Kontrolü
            if (req.Password != req.VerifyPassword)
            {
                return Results.BadRequest(new { message = "Şifreler eşleşmemektedir." });
            }

            // 2. E-posta Kontrolü (harf duyarsız, B23)
            var email = EmailNormalizer.Normalize(req.Email);
            var existingEmail = await db.Therapists
                .AnyAsync(t => t.Email.ToLower() == email);

            if (existingEmail)
            {
                return Results.BadRequest(new { message = "Bu e-posta adresi zaten kayıtlıdır." });
            }

            // 3. Kullanıcı Adı Kontrolü
            var existingUsername = await db.Therapists
                .AnyAsync(t => t.Username == req.Username);

            if (existingUsername)
            {
                return Results.BadRequest(new { message = "Bu kullanıcı adı zaten alınmıştır." });
            }

            // 4. Yeni Terapist Objelerinin Oluşturulması
            var newTherapist = new Therapist
            {
                Name = req.FullName,
                Username = req.Username,
                Email = email!,
                // TODO: Gerçek uygulamada şifre HASH'lenmelidir (örn: Argon2, BCrypt)
                Password = req.Password,
                ClinicName = req.ClinicName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // 5. Veritabanına Ekleme
            db.Therapists.Add(newTherapist);
            await db.SaveChangesAsync();
            
            // 6. JWT Token Generate Et
            var token = jwtHelper.GenerateToken(newTherapist);
            
            // 7. Başarılı Yanıt
            return Results.Ok(new AuthResponse
            {
                Token = token,
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

        app.MapPost("/api/player/auth/register", async (
    [FromBody] PlayerRegisterRequest req,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Nickname) ||
        string.IsNullOrWhiteSpace(req.Name) ||
        string.IsNullOrWhiteSpace(req.Email) ||
        string.IsNullOrWhiteSpace(req.Password) ||
        string.IsNullOrWhiteSpace(req.PasswordAgain))
    {
        return Results.BadRequest(new { message = "Tüm alanları doldurun." });
    }

    if (req.Password != req.PasswordAgain)
    {
        return Results.BadRequest(new { message = "Şifreler eşleşmiyor." });
    }

    var nicknameExists = await db.Players.AnyAsync(p => p.Nickname == req.Nickname);
    if (nicknameExists)
    {
        return Results.BadRequest(new { message = "Kullanıcı adı alınmış." });
    }

    var email = EmailNormalizer.Normalize(req.Email);
    var emailExists = await db.Players.AnyAsync(p => p.Email != null && p.Email.ToLower() == email);
    if (emailExists)
    {
        return Results.BadRequest(new { message = "E-posta zaten kayıtlı." });
    }

    var now = DateTime.UtcNow;

    var player = new Player
    {
        Nickname = req.Nickname,
        Name = req.Name,
        Email = email,
        Password = req.Password,
        BirthDate = req.BirthDate,
        CreatedAt = now,
        UpdatedAt = now,
        LastLogin = now,
        TotalScore = 0
    };

    db.Players.Add(player);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        playerId = player.Id,
        nickname = player.Nickname,
        totalScore = player.TotalScore
    });
});

    }
}