// Services/CloudinaryService.cs
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System.IO;


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


    private readonly Cloudinary _cloudinary;

    // ✅ EKLE: WAV byte[] -> Cloudinary public URL
    public async Task<string> UploadAudioAsync(byte[] wavBytes, string fileName)
    {
        using var ms = new MemoryStream(wavBytes);

        var uploadParams = new RawUploadParams
        {
            File = new FileDescription(fileName, ms),
            Folder = "pronunciation",
            UseFilename = true,
            UniqueFilename = true,
            Overwrite = false
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.StatusCode != HttpStatusCode.OK && result.StatusCode != HttpStatusCode.Created)
            throw new Exception("Cloudinary audio upload failed: " + result.Error?.Message);

        return result.SecureUrl.ToString(); // ✅ FluentMe’ye bunu vereceğiz
    }



public async Task<string> UploadWavAsync(byte[] wavBytes)
{
    using var ms = new MemoryStream(wavBytes);

    // Cloudinary'de ses dosyaları "raw" upload edilir
    var uploadParams = new RawUploadParams
    {
        File = new FileDescription("recording.wav", ms),
        Folder = "pronunciation_wavs"
    };

    var uploadResult = await _cloudinary.UploadAsync(uploadParams);

    return uploadResult.SecureUrl.ToString();
}

}