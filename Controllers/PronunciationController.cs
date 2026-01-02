// Controllers/PronunciationController.cs

using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DktApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PronunciationController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        private const string HARDCODED_USER = "meryem.kilic";
        private const string HARDCODED_PASS = "Melv18309";
        private const string HARDCODED_KEY  = "20251224164351-fQaj7AdeKhp-87831";

        private static string _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        private const string FLUENT_LOGIN = "https://thefluent.me/api/swagger/login";
        private const string FLUENT_POST  = "https://thefluent.me/api/swagger/post";

        public PronunciationController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("check")]
        public async Task<IActionResult> CheckPronunciation([FromForm] IFormFile audioFile, [FromForm] string text)
        {
            Console.WriteLine($"[PRON] Request arrived | text='{text}'");

            if (audioFile == null || audioFile.Length == 0)
                return BadRequest(new { message = "Ses dosyası yok (audioFile/audio_file)." });

            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(new { message = "Text alanı boş." });

            // fileBytes ileride score için kullanılabilir. Şimdilik post doğrulaması için okuyup tutuyoruz.
            byte[] fileBytes;
            await using (var ms = new MemoryStream())
            {
                await audioFile.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            var client = _httpClientFactory.CreateClient();

            try
            {
                var token = await GetValidToken(client);

                // 1) POST CREATE - FluentMe hassas: header + json body net olmalı
                var postIdResult = await CreatePostWithFallback(client, token, text);

                if (!postIdResult.isSuccess)
                {
                    return StatusCode(
                        (int)(postIdResult.status ?? HttpStatusCode.BadRequest),
                        new { message = "Post Hatası", detail = postIdResult.body }
                    );
                }

                var postId = postIdResult.postId!;
                Console.WriteLine($"[PRON] Post ID OK: {postId}");

                // Şimdilik post aşaması düzgün mü diye postId döndürüyoruz.
                // Post OK olduktan sonra score adımını eklemek daha sağlıklı.
                return Ok(new { message = "Post oluşturuldu", postId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PRON][CRITICAL] {ex}");
                return StatusCode(500, new { message = "Sunucu Hatası", detail = ex.Message });
            }
        }

        // -------------------------
        // FluentMe: Create Post (Fallback)
        // -------------------------
        private async Task<(bool isSuccess, HttpStatusCode? status, string body, string? postId)>
            CreatePostWithFallback(HttpClient client, string token, string text)
        {
            // Deneme-1: post_language_id int
            var attempt1Json = JsonSerializer.Serialize(new
            {
                post_title = "Unity Kaydı",
                post_content = text,
                post_language_id = 76
            });

            var attempt1 = await SendCreatePost(client, token, attempt1Json);
            if (attempt1.isSuccess)
                return attempt1;

            // Deneme-2: post_language_id string
            var attempt2Json = JsonSerializer.Serialize(new
            {
                post_title = "Unity Kaydı",
                post_content = text,
                post_language_id = "76"
            });

            var attempt2 = await SendCreatePost(client, token, attempt2Json);
            return attempt2;
        }

        private async Task<(bool isSuccess, HttpStatusCode? status, string body, string? postId)>
            SendCreatePost(HttpClient client, string token, string jsonBody)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, FLUENT_POST);

            // Header'ları net set et
            req.Headers.Clear();
            req.Headers.Add("x-access-token", token);
            req.Headers.Add("x-api-key", HARDCODED_KEY);
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Body'yi explicit StringContent ile ver (FluentMe'nin bazı durumlarda daha iyi parse ettiği yöntem)
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            Console.WriteLine($"[PRON] POST /post payload: {jsonBody}");

            var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[PRON] POST /post status={(int)resp.StatusCode} body={body}");

            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    _cachedToken = null;

                return (false, resp.StatusCode, body, null);
            }

            // post_id parse
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var obj = JsonSerializer.Deserialize<CreatePostResponse>(body, options);

                if (obj != null && !string.IsNullOrWhiteSpace(obj.post_id))
                    return (true, resp.StatusCode, body, obj.post_id);

                // Bazı API'ler düz string döndürebilir: "12345"
                if (LooksLikeJsonString(body))
                {
                    var s = JsonSerializer.Deserialize<string>(body);
                    if (!string.IsNullOrWhiteSpace(s))
                        return (true, resp.StatusCode, body, s);
                }

                return (false, resp.StatusCode, "Post response parse edilemedi: " + body, null);
            }
            catch (Exception ex)
            {
                return (false, resp.StatusCode, "Post response parse exception: " + ex.Message + " body=" + body, null);
            }
        }

        private static bool LooksLikeJsonString(string s)
        {
            s = s?.Trim() ?? "";
            return s.StartsWith("\"") && s.EndsWith("\"");
        }

        // -------------------------
        // Token
        // -------------------------
        private async Task<string> GetValidToken(HttpClient client)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry)
                return _cachedToken;

            Console.WriteLine("[PRON] Login attempt...");

            // POST login (explicit)
            var loginJson = JsonSerializer.Serialize(new
            {
                username = HARDCODED_USER,
                password = HARDCODED_PASS
            });

            using var loginReq = new HttpRequestMessage(HttpMethod.Post, FLUENT_LOGIN);
            loginReq.Headers.Add("x-api-key", HARDCODED_KEY);
            loginReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            loginReq.Content = new StringContent(loginJson, Encoding.UTF8, "application/json");

            var resp = await client.SendAsync(loginReq);
            var body = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[PRON] Login status={(int)resp.StatusCode} body={body}");

            if (!resp.IsSuccessStatusCode)
                throw new Exception("Login Başarısız: " + body);

            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("token", out var t) &&
                t.ValueKind == JsonValueKind.String)
            {
                _cachedToken = t.GetString();
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                _cachedToken = doc.RootElement.GetString();
            }
            else
            {
                throw new Exception("Token alınamadı. Login response: " + body);
            }

            if (string.IsNullOrWhiteSpace(_cachedToken))
                throw new Exception("Token boş geldi.");

            _tokenExpiry = DateTime.Now.AddMinutes(50);
            return _cachedToken;
        }
    }

    public class CreatePostResponse
    {
        public string post_id { get; set; }
    }
}
