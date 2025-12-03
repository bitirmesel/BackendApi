// Services/CloudinaryService.cs
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace DktApi.Services;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration config)
    {
        // Render Environment Variables'dan okuyacak
        // Eğer appsettings.json'da yoksa bile Render'ın "Environment" sekmesinden çeker.
        var cloudName = config["CLOUDINARY_CLOUD_NAME"];
        var apiKey = config["CLOUDINARY_API_KEY"];
        var apiSecret = config["CLOUDINARY_API_SECRET"];

        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("Cloudinary ayarları bulunamadı! Lütfen Render Environment Variables kontrol edin.");
        }

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("Dosya boş olamaz");

        using var stream = file.OpenReadStream();
        
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            // İstersen klasör belirtebilirsin:
            // Folder = "game_assets" 
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);

        // Yüklenen resmin güvenli (https) linkini döndür
        return uploadResult.SecureUrl.ToString();
    }
}