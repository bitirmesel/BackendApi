using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using DktApi.Models.FluentMe; 

// DTO'ları buradan çekiyoruz

namespace DktApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PronunciationController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        
        // Şifreleri Render ayarlarından çekeceğiz
        private readonly string _apiUsername;
        private readonly string _apiPassword;

        // Türkçe (Kadın Sesi) ID'si: 76
        private const string TURKISH_LANGUAGE_ID = "76"; 

        // Token Hafızası
        private static string _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        public PronunciationController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _apiUsername = configuration["FLUENT_USER"]; // Render Env Var
            _apiPassword = configuration["FLUENT_PASS"]; // Render Env Var
        }

        [HttpPost("check")]
        public async Task<IActionResult> CheckPronunciation([FromForm] IFormFile audioFile, [FromForm] string text)
        {
            if (audioFile == null || audioFile.Length == 0) 
                return BadRequest("Ses dosyası yok.");

            var client = _httpClientFactory.CreateClient();

            try
            {
                // 1. ADIM: Token Al (Login Ol)
                string token = await GetValidToken(client);

                // Token'ı başlığa ekle
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("x-access-token", token);

                // 2. ADIM: Post Oluştur
                var postContent = new
                {
                    post_title = "Unity Kaydı",
                    post_content = text,
                    post_language_id = TURKISH_LANGUAGE_ID
                };

                var postResponse = await client.PostAsJsonAsync("https://thefluent.me/api/swagger/post", postContent);
                
                // Eğer Token süresi dolduysa (401)
                if (postResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _cachedToken = null; // Token'ı unut
                    return StatusCode(401, "Token süresi doldu, lütfen tekrar deneyin.");
                }

                if (!postResponse.IsSuccessStatusCode)
                     return BadRequest("Post Hatası: " + await postResponse.Content.ReadAsStringAsync());

                // Post ID'yi çek
                var postJson = await postResponse.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var postObj = JsonSerializer.Deserialize<CreatePostResponse>(postJson, options);
                
                if(postObj == null || string.IsNullOrEmpty(postObj.post_id))
                    return BadRequest("Post ID alınamadı.");

                string postId = postObj.post_id;

                // 3. ADIM: Sesi Puanla
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
                return StatusCode(500, "Sunucu Hatası: " + ex.Message);
            }
        }

        // --- Login Helper ---
        private async Task<string> GetValidToken(HttpClient client)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry)
                return _cachedToken;

            // Basic Auth ile Login
            var authBytes = Encoding.ASCII.GetBytes($"{_apiUsername}:{_apiPassword}");
            var authString = Convert.ToBase64String(authBytes);
            
            var request = new HttpRequestMessage(HttpMethod.Get, "https://thefluent.me/api/swagger/login");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new Exception("Login Başarısız! Kullanıcı adı/şifre hatalı olabilir.");

            var json = await response.Content.ReadAsStringAsync();
            
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.String)
                    _cachedToken = doc.RootElement.GetString();
                else if (doc.RootElement.TryGetProperty("token", out var t))
                    _cachedToken = t.GetString();
                else
                    throw new Exception("Token formatı anlaşılamadı.");
            }

            _tokenExpiry = DateTime.Now.AddMinutes(50); 
            return _cachedToken;
        }
    }
}