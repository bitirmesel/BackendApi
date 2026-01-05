using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System.IO;
using System.Net; // HttpStatusCode için gerekli
using Microsoft.Extensions.Configuration; // IConfiguration için gerekli
using Microsoft.AspNetCore.Http; // IFormFile için gerekli

namespace DktApi.Services;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration config)
    {
        // Render Environment Variables'dan okuyacak
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

    // Resim Yükleme (Flutter/Therapist paneli için)
    public async Task<string> UploadImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("Dosya boş olamaz");

        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "game_assets"
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);
        return uploadResult.SecureUrl.ToString();
    }

    // Ses Yükleme (Unity Pronunciation için - WAV byte[])
    public async Task<string> UploadAudioAsync(byte[] wavBytes, string fileName)
    {
        if (wavBytes == null || wavBytes.Length == 0)
            throw new ArgumentException("Ses verisi boş olamaz");

        using var ms = new MemoryStream(wavBytes);

        // Ses dosyaları Cloudinary'de "raw" olarak yüklenir
        var uploadParams = new RawUploadParams
        {
            File = new FileDescription(fileName, ms),
            Folder = "pronunciation",
            // Linkin sonuna .wav ekleyerek FluentMe'nin dosyayı tanımasını sağlıyoruz
            PublicId = $"audio_{DateTime.Now.Ticks}.wav",
            Overwrite = false
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.StatusCode != HttpStatusCode.OK && result.StatusCode != HttpStatusCode.Created)
            throw new Exception("Cloudinary audio upload failed: " + result.Error?.Message);

        return result.SecureUrl.ToString(); // FluentMe'ye gidecek olan URL
    }
}