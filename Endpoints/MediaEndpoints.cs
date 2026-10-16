using DktApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http; 

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
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                var errorId = ErrorLog.Log("POST /api/media/upload", ex);
                return Results.Problem($"Görsel yüklenemedi. Hata kodu: {errorId}");
            }
        })
        .WithTags("Media")
        .WithName("UploadImage")
        .DisableAntiforgery();

        // POST /api/media/upload-audio
        // Oyun asset'i olarak model ses dosyası yükler (mp3/wav/ogg).
        // Dönen URL asset JSON'ındaki "audio" alanına yazılır.
        app.MapPost("/api/media/upload-audio", async (IFormFile file, CloudinaryService cloudinaryService) =>
        {
            try
            {
                var url = await cloudinaryService.UploadAudioFileAsync(file, folder: "game_audio");
                return Results.Ok(new { url = url });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                var errorId = ErrorLog.Log("POST /api/media/upload-audio", ex);
                return Results.Problem($"Ses yüklenemedi. Hata kodu: {errorId}");
            }
        })
        .WithTags("Media")
        .WithName("UploadAudio")
        .DisableAntiforgery();
    }
}