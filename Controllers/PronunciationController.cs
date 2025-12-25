using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
// using DktApi.Models; // Gerekirse açarsın ama aşağıya ekledim garanti olsun diye.

namespace DktApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PronunciationController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        
        // Render Environment Variables'dan okuyacaklarımız
        private readonly string _apiUsername;
        private readonly string _apiPassword;
        private readonly string _apiKey; 

        // Token'ı hafızada tutalım
        private static string _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        public PronunciationController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            // .Trim() ile boşlukları temizliyoruz
            _apiUsername = configuration["FLUENT_USER"]?.Trim();
            _apiPassword = configuration["FLUENT_PASS"]?.Trim();
            _apiKey = configuration["THE_FLUENT_ME_API_KEY"]?.Trim();
        }

        [HttpPost("check")]
        public async Task<IActionResult> CheckPronunciation([FromForm] IFormFile audioFile, [FromForm] string text)
        {
            if (audioFile == null || audioFile.Length == 0) 
                return BadRequest("Ses dosyası yok.");

            // Config Kontrolü
            if (string.IsNullOrEmpty(_apiUsername) || string.IsNullOrEmpty(_apiPassword) || string.IsNullOrEmpty(_apiKey))
            {
                Console.WriteLine("[ERROR] API Key veya User/Pass Render'da eksik!");
                return StatusCode(500, "Sunucu Config Hatası: Environment Variables eksik.");
            }

            var client = _httpClientFactory.CreateClient();

            try
            {
                // 1. ADIM: OTOMATİK LOGIN OL VE TOKEN AL
                string token = await GetValidToken(client);

                // Token'ı başlığa ekle
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("x-access-token", token);

                // 2. ADIM: POST OLUŞTUR (ID: 76 Türkçe)
                var postContent = new
                {
                    post_title = "Unity Kaydı",
                    post_content = text,
                    post_language_id = "76"
                };

                var postResponse = await client.PostAsJsonAsync("https://thefluent.me/api/swagger/post", postContent);
                
                if (!postResponse.IsSuccessStatusCode)
                {
                    if(postResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        _cachedToken = null; // Token bayatlamış olabilir
                    
                    return BadRequest("Post Oluşturma Hatası: " + await postResponse.Content.ReadAsStringAsync());
                }

                // Post ID'yi al
                var postRespStr = await postResponse.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var postObj = JsonSerializer.Deserialize<CreatePostResponse>(postRespStr, options);
                
                string postId = postObj?.post_id;
                if(string.IsNullOrEmpty(postId)) return BadRequest("API'den Post ID dönmedi.");

                // 3. ADIM: SESİ GÖNDER
                using (var content = new MultipartFormDataContent())
                {
                    using (var stream = audioFile.OpenReadStream())
                    {
                        var fileContent = new StreamContent(stream);
                        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                        content.Add(fileContent, "audio_file", "recording.wav");

                        var scoreResponse = await client.PostAsync($"https://thefluent.me/api/swagger/score/{postId}", content);

                        if (scoreResponse.IsSuccessStatusCode)
                            return Ok(await scoreResponse.Content.ReadAsStringAsync());
                        else
                            return StatusCode((int)scoreResponse.StatusCode, "Puanlama Hatası: " + await scoreResponse.Content.ReadAsStringAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRITICAL] {ex.Message}");
                return StatusCode(500, "Sunucu Hatası: " + ex.Message);
            }
        }

        // --- LOGIN YARDIMCISI ---
        private async Task<string> GetValidToken(HttpClient client)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry)
                return _cachedToken;

            // Basic Auth Header Oluşturma
            var authBytes = Encoding.ASCII.GetBytes($"{_apiUsername}:{_apiPassword}");
            var authString = Convert.ToBase64String(authBytes);
            
            var request = new HttpRequestMessage(HttpMethod.Get, "https://thefluent.me/api/swagger/login");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
            request.Headers.Add("x-api-key", _apiKey); // API Key'i de ekliyoruz

            var response = await client.SendAsync(request);
            var respStr = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Login Başarısız! ({response.StatusCode}) Hata: {respStr}");

            // JSON Parse
            using (JsonDocument doc = JsonDocument.Parse(respStr))
            {
                if (doc.RootElement.TryGetProperty("token", out var tokenProp))
                {
                    _cachedToken = tokenProp.GetString();
                    _tokenExpiry = DateTime.Now.AddMinutes(50);
                    return _cachedToken;
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.String) // Bazen direkt string döner
                {
                     _cachedToken = doc.RootElement.GetString();
                     _tokenExpiry = DateTime.Now.AddMinutes(50);
                     return _cachedToken;
                }
                else
                {
                    throw new Exception("Token bulunamadı: " + respStr);
                }
            }
        }
    }

    // --- YARDIMCI SINIFLAR (Dosyanın sonuna ekledik ki hata vermesin) ---
    public class CreatePostResponse
    {
        public string post_id { get; set; }
        public string message { get; set; }
    }
}