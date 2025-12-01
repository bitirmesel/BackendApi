using DktApi.Models.Auth;
using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest req,
            AppDbContext db,
            IConfiguration config) =>
        {
            // therapists tablosundan email ile kullanıcı çek
            var therapist = await db.Therapists
                .FirstOrDefaultAsync(t => t.Email == req.Email);

            if (therapist is null || therapist.Password != req.Password)
            {
                // Şimdilik plaintext karşılaştırma – DEMO
                return Results.Unauthorized();
            }

            // Eğer token üretmek istiyorsan, DbUser yerine Therapist’i baz alan bir helper yapmamız lazım.
            // Şimdilik basit bir response dönelim, token kısmını istersen sonra güzelleştiririz.

            return Results.Ok(new LoginResponse
            {
                Token = "demo-token",              // TODO: JWT'e geçeceğiz
                TherapistId = (int)therapist.Id,
                Name = therapist.Name
            });
        });
    }
}
