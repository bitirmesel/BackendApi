using System.Net.Http.Headers;
using System.Text.Json;

namespace DktApi.Services;

/// <summary>
/// OpenAI Whisper API ile ses dosyasını transkript eden servis.
/// Hece egzersizleri (S1, S2, S3) için kullanılır.
/// </summary>
public class WhisperService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey;

    private const string WHISPER_URL = "https://api.openai.com/v1/audio/transcriptions";

    public WhisperService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;

        // Render Environment Variables'dan oku
        _apiKey = config["OPENAI_API_KEY"] 
                  ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
                  ?? throw new Exception("OPENAI_API_KEY bulunamadı! Render Environment Variables kontrol edin.");
    }

    /// <summary>
    /// Ses dosyasını Whisper API'ye gönderip transkript alır.
    /// </summary>
    /// <param name="audioBytes">WAV formatında ses verisi</param>
    /// <param name="fileName">Dosya adı (ör: "recording.wav")</param>
    /// <param name="language">Dil kodu (varsayılan: "tr" — Türkçe)</param>
    /// <returns>Whisper'ın döndüğü transkript metni</returns>
    public async Task<WhisperResult> TranscribeAsync(byte[] audioBytes, string fileName = "recording.wav", string language = "tr")
    {
        var startMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var client = _httpClientFactory.CreateClient();

        using var content = new MultipartFormDataContent();

        // Ses dosyası
        var audioContent = new ByteArrayContent(audioBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "file", fileName);

        // Model
        content.Add(new StringContent("whisper-1"), "model");

        // Dil
        content.Add(new StringContent(language), "language");

        // İstek
        using var request = new HttpRequestMessage(HttpMethod.Post, WHISPER_URL);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = content;

        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        var endMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Console.WriteLine($"[WHISPER] Status: {(int)response.StatusCode}, Duration: {endMs - startMs}ms, Response: {responseBody}");

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Whisper API hatası: {(int)response.StatusCode} - {responseBody}");
        }

        // Whisper response: {"text": "transkript metni"}
        var result = JsonSerializer.Deserialize<WhisperApiResponse>(responseBody, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return new WhisperResult
        {
            Transcript = result?.Text?.Trim() ?? string.Empty,
            DurationMs = endMs - startMs
        };
    }

    private class WhisperApiResponse
    {
        public string? Text { get; set; }
    }
}

/// <summary>
/// Whisper transkript sonucu.
/// </summary>
public class WhisperResult
{
    public string Transcript { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}
