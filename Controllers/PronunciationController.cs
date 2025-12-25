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
        
        private readonly string _apiUsername;
        private readonly string _apiPassword;
        private readonly string _apiKey;

        private static string _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        public PronunciationController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            
            // --- DETAYLI DEĞİŞKEN OKUMA ---
            // Her birini tek tek okuyup logluyoruz (Şifreleri gizleyerek)
            
            _apiUsername = configuration["FLUENT_USER"];
            Console.WriteLine($"[CONFIG CHECK] FLUENT_USER: '{_apiUsername}' (Boş mu: {string.IsNullOrEmpty(_apiUsername)})");

            _apiPassword = configuration["FLUENT_PASS"];
            string passLog = string.IsNullOrEmpty(_apiPassword) ? "YOK" : "VAR (" + _apiPassword.Length + " karakter)";
            Console.WriteLine($"[CONFIG CHECK] FLUENT_PASS: {passLog}");

            _apiKey = configuration["THE_FLUENT_ME_API_KEY"];
            string keyLog = string.IsNullOrEmpty(_apiKey) ? "YOK" : "VAR (" + _apiKey.Length + " karakter)";
            Console.WriteLine($"[CONFIG CHECK] THE_FLUENT_ME_API_KEY: {keyLog}");

            // Varsa boşlukları temizle
            _apiUsername = _apiUsername?.Trim();
            _apiPassword = _apiPassword?.Trim();
            _apiKey = _apiKey?.Trim();
        }

        [HttpPost("check")]
        public async Task<IActionResult> CheckPronunciation([FromForm] IFormFile audioFile, [FromForm] string text)
        {
            // 1. HANGİSİ EKSİK?
            List<string> missingVars = new List<string>();
            if (string.IsNullOrEmpty(_apiUsername)) missingVars.Add("FLUENT_USER");
            if (string.IsNullOrEmpty(_apiPassword)) missingVars.Add("FLUENT_PASS");
            if (string.IsNullOrEmpty(_apiKey)) missingVars.Add("THE_FLUENT_ME_API_KEY");

            if (missingVars.Count > 0)
            {
                string errorMsg = "Sunucu Config Hatası! Eksik Değişkenler: " + string.Join(", ", missingVars);
                Console.WriteLine($"[CRITICAL ERROR] {errorMsg}");
                Console.WriteLine("Lütfen Render Environment Variables ekranını kontrol edin.");
                return StatusCode(500, errorMsg);
            }

            if (audioFile == null || audioFile.Length == 0) return BadRequest("Ses dosyası yok.");

            var client = _httpClientFactory.CreateClient();

            try
            {
                // LOGIN VE TOKEN
                string token = await GetValidToken(client);

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("x-access-token", token);

                // POST OLUŞTUR
                var postContent = new
                {
                    post_title = "Unity Kaydı",
                    post_content = text,
                    post_language_id = "76"
                };

                var postResponse = await client.PostAsJsonAsync("https://thefluent.me/api/swagger/post", postContent);
                
                if (!postResponse.IsSuccessStatusCode)
                {
                    if(postResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized) _cachedToken = null;
                    return BadRequest("Post Hatası: " + await postResponse.Content.ReadAsStringAsync());
                }

                var postRespStr = await postResponse.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var postObj = JsonSerializer.Deserialize<CreatePostResponse>(postRespStr, options);
                string postId = postObj?.post_id;

                // SES GÖNDER
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
                return StatusCode(500, "İşlem Hatası: " + ex.Message);
            }
        }

        private async Task<string> GetValidToken(HttpClient client)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry) return _cachedToken;

            var authBytes = Encoding.ASCII.GetBytes($"{_apiUsername}:{_apiPassword}");
            var authString = Convert.ToBase64String(authBytes);
            
            var request = new HttpRequestMessage(HttpMethod.Get, "https://thefluent.me/api/swagger/login");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
            request.Headers.Add("x-api-key", _apiKey);

            var response = await client.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Login API Hatası: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");

            var json = await response.Content.ReadAsStringAsync();
            using (JsonDocument doc = JsonDocument.Parse(json))
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