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
            var user = await db.DbUsers
                .Include(u => u.Therapist)
                .FirstOrDefaultAsync(u => u.Email == req.Email);

            if (user is null || user.PasswordHash != req.Password) // DEMO
            {
                return Results.Unauthorized();
            }

            var jwtHelper = new JwtHelper(config);
            var token = jwtHelper.GenerateToken(user);

            return Results.Ok(new LoginResponse
            {
                Token = token,
                TherapistId = user.TherapistId ?? 0,
                Name = user.Therapist?.Name ?? "Terapist"
            });
        });
    }
}
