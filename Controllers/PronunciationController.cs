using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace DktApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PronunciationController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        
        // --- TEST İÇİN SABİT DEĞERLER (Environment Variables Yerine) ---
        private const string HARDCODED_USER = "meryem.kilic";
        private const string HARDCODED_PASS = "Melv18309";
        private const string HARDCODED_KEY  = "20251224164351-fQaj7AdeKhp-87831";

        private static string _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        public PronunciationController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("check")]
        public async Task<IActionResult> CheckPronunciation([FromForm] IFormFile audioFile, [FromForm] string text)
        {
            Console.WriteLine($"[LOG] İstek Geldi. Text: {text}");

            if (audioFile == null || audioFile.Length == 0) 
                return BadRequest("Ses dosyası yok.");

            var client = _httpClientFactory.CreateClient();

            try
            {
                // 1. TOKEN AL
                string token = await GetValidToken(client);
                Console.WriteLine("[LOG] Token Başarıyla Alındı.");

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("x-access-token", token);

                // 2. POST OLUŞTUR
                var postContent = new
                {
                    post_title = "Unity Kaydı",
                    post_content = text,
                    post_language_id = "76"
                };

                Console.WriteLine("[LOG] Post oluşturuluyor...");
                var postResponse = await client.PostAsJsonAsync("https://thefluent.me/api/swagger/post", postContent);
                
                if (!postResponse.IsSuccessStatusCode)
                {
                    if(postResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized) _cachedToken = null;
                    var err = await postResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"[ERROR] Post Hatası: {err}");
                    return BadRequest("Post Hatası: " + err);
                }

                var postRespStr = await postResponse.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var postObj = JsonSerializer.Deserialize<CreatePostResponse>(postRespStr, options);
                string postId = postObj?.post_id;
                
                Console.WriteLine($"[LOG] Post ID alındı: {postId}");

                // 3. SESİ GÖNDER
                using (var content = new MultipartFormDataContent())
                {
                    using (var stream = audioFile.OpenReadStream())
                    {
                        var fileContent = new StreamContent(stream);
                        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                        content.Add(fileContent, "audio_file", "recording.wav");

                        var scoreResponse = await client.PostAsync($"https://thefluent.me/api/swagger/score/{postId}", content);

                        if (scoreResponse.IsSuccessStatusCode)
                        {
                            var result = await scoreResponse.Content.ReadAsStringAsync();
                            Console.WriteLine("[SUCCESS] Puanlama Başarılı!");
                            return Ok(result);
                        }
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

        private async Task<string> GetValidToken(HttpClient client)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry) return _cachedToken;

            Console.WriteLine("[LOG] Login deneniyor (Hardcoded Credentials)...");

            // JSON POST Login
            var loginData = new { username = HARDCODED_USER, password = HARDCODED_PASS };
            var response = await client.PostAsJsonAsync("https://thefluent.me/api/swagger/login", loginData);

            // POST çalışmazsa GET Basic Auth
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("[LOG] POST Login başarısız, GET deneniyor...");
                var authBytes = Encoding.ASCII.GetBytes($"{HARDCODED_USER}:{HARDCODED_PASS}");
                var authString = Convert.ToBase64String(authBytes);
                
                var request = new HttpRequestMessage(HttpMethod.Get, "https://thefluent.me/api/swagger/login");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
                request.Headers.Add("x-api-key", HARDCODED_KEY);

                response = await client.SendAsync(request);
            }

            var respStr = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Login Başarısız! Hata: {respStr}");

            using (JsonDocument doc = JsonDocument.Parse(respStr))
            {
                if (doc.RootElement.TryGetProperty("token", out var t))
                {
                    _cachedToken = t.GetString();
                    _tokenExpiry = DateTime.Now.AddMinutes(50);
                    return _cachedToken;
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.String)
                {
                     _cachedToken = doc.RootElement.GetString();
                     _tokenExpiry = DateTime.Now.AddMinutes(50);
                     return _cachedToken;
                }
            }
            throw new Exception("Token alınamadı.");
        }
    }

    public class CreatePostResponse
    {
        public string post_id { get; set; }
    }
}