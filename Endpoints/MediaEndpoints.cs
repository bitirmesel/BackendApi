using DktApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http; // <--- BU EKSİKTİ

namespace DktApi.Endpoints;

public static class MediaEndpoints
{
    public static void MapMediaEndpoints(this WebApplication app)
    {
        app.MapPost("/api/media/upload", async (IFormFile file, CloudinaryService cloudinaryService) =>
        {
            try
            {
                var url = await cloudinaryService.UploadImageAsync(file);
                return Results.Ok(new { url = url });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        })
        .WithTags("Media")
        .WithName("UploadImage")
        .DisableAntiforgery();
    }
}