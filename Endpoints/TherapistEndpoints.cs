using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Endpoints;

public static class TherapistEndpoints
{
    public static void MapTherapistEndpoints(this WebApplication app)
    {
        // Örn. kendi profilini görmek için (isteğe bağlı)
        app.MapGet("/api/therapists/{id:int}", async (int id, AppDbContext db) =>
        {
            var t = await db.Therapists.FindAsync(id);
            return t is null ? Results.NotFound() : Results.Ok(t);
        });
    }
}
