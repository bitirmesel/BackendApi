// Controllers/PronunciationController.cs

using Microsoft.AspNetCore.Mvc;
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

        // Hardcoded kalsın demiştin ama ENV de okuyalım (varsa ENV öncelikli)
        // Render ENV örneği:
        // FLUENT_USER, FLUENT_PASS, THE_FLUENT_ME_API_KEY (veya FLUENT_KEY)
        private string FluentUser => Environment.GetEnvironmentVariable("FLUENT_USER") ?? HARDCODED_USER;
        private string FluentPass => Environment.GetEnvironmentVariable("FLUENT_PASS") ?? HARDCODED_PASS;

        // Key için iki isimden birini destekleyelim
        private string FluentKey =>
            Environment.GetEnvironmentVariable("THE_FLUENT_ME_API_KEY")
            ?? Environment.GetEnvironmentVariable("FLUENT_KEY")
            ?? HARDCODED_KEY;

        // --- TEST İÇİN SABİT DEĞERLER ---
        private const string HARDCODED_USER = "meryem.kilic";
        private const string HARDCODED_PASS = "Melv18309";
        private const string HARDCODED_KEY  = "20251224164351-fQaj7AdeKhp-87831";

        private static string _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        public PronunciationController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // Unity tarafı önerilen:
        // form.AddBinaryData("audioFile", bytes, "recording.wav", "audio/wav");
        // form.AddField("text", "kedi");
        //
        // Ama uyumluluk için audio_file da kabul ediyoruz.
        [HttpPost("check")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CheckPronunciation(
            [FromForm] IFormFile? audioFile,                         // audioFile
            [FromForm(Name = "audio_file")] IFormFile? audio_file,   // audio_file (fallback)
            [FromForm] string text
        )
        {
            Console.WriteLine($"[PRON] Request arrived | text='{text}'");

            var file = audioFile ?? audio_file;

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Ses dosyası yok. Form field: audioFile (önerilen) veya audio_file." });

            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(new { message = "Text alanı boş." });

            Console.WriteLine($"[PRON] Incoming file | name='{file.FileName}' len={file.Length} contentType='{file.ContentType}'");

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(120);

            try
            {
                // 1) TOKEN
                string token = await GetValidToken(client);
                Console.WriteLine("[PRON] Token OK.");

                // 2) POST OLUŞTUR (metin burada)
                var postContent = new
                {
                    post_title = "Unity Kaydı",
                    post_content = text,
                    post_language_id = "76"
                };

                using var postReq = new HttpRequestMessage(HttpMethod.Post, "https://thefluent.me/api/swagger/post");
                postReq.Headers.Add("x-access-token", token);
                postReq.Content = JsonContent.Create(postContent);

                Console.WriteLine("[PRON] Creating post...");
                var postResp = await client.SendAsync(postReq);

                var postBody = await postResp.Content.ReadAsStringAsync();
                Console.WriteLine($"[PRON] Post resp status={(int)postResp.StatusCode} body={postBody}");

                if (!postResp.IsSuccessStatusCode)
                {
                    if (postResp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        _cachedToken = null;

                    return StatusCode((int)postResp.StatusCode, new { message = "Post Hatası", detail = postBody });
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var postObj = JsonSerializer.Deserialize<CreatePostResponse>(postBody, options);

                string? postId = postObj?.post_id;

                if (string.IsNullOrWhiteSpace(postId))
                    return StatusCode(500, new { message = "Post ID alınamadı (post_id boş).", detail = postBody });

                Console.WriteLine($"[PRON] Post ID OK: {postId}");

                // 3) SCORE: WAV’ı gönder
                // ÖNEMLİ: FluentMe bazı yerlerde audio_file, bazı yerlerde audioFile bekliyor olabiliyor.
                // Biz ikisini de gönderiyoruz (en sağlam workaround).
                using var multipart = new MultipartFormDataContent();

                // Not: aynı stream’i iki kere okuyamayız; memory’e alıp iki kez ekleyeceğiz.
                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    using var s = file.OpenReadStream();
                    await s.CopyToAsync(ms);
                    bytes = ms.ToArray();
                }

                // audio_file
                var c1 = new ByteArrayContent(bytes);
                c1.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                multipart.Add(c1, "audio_file", "recording.wav");

                // audioFile
                var c2 = new ByteArrayContent(bytes);
                c2.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                multipart.Add(c2, "audioFile", "recording.wav");

                using var scoreReq = new HttpRequestMessage(HttpMethod.Post, $"https://thefluent.me/api/swagger/score/{postId}");
                scoreReq.Headers.Add("x-access-token", token);
                scoreReq.Content = multipart;

                Console.WriteLine("[PRON] Sending audio for scoring...");
                var scoreResp = await client.SendAsync(scoreReq);

                var scoreBody = await scoreResp.Content.ReadAsStringAsync();
                Console.WriteLine($"[PRON] Score resp status={(int)scoreResp.StatusCode} body={scoreBody}");

                if (scoreResp.IsSuccessStatusCode)
                    return Ok(scoreBody);

                if (scoreResp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    _cachedToken = null;

                return StatusCode((int)scoreResp.StatusCode, new { message = "Puanlama Hatası", detail = scoreBody });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PRON][CRITICAL] {ex}");
                return StatusCode(500, new { message = "Sunucu Hatası", detail = ex.Message });
            }
        }

        private async Task<string> GetValidToken(HttpClient client)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry)
                return _cachedToken;

            Console.WriteLine("[PRON] Login attempt...");

            // 1) JSON POST login
            var loginData = new { username = FluentUser, password = FluentPass };

            using var loginReq = new HttpRequestMessage(HttpMethod.Post, "https://thefluent.me/api/swagger/login");
            loginReq.Content = JsonContent.Create(loginData);

            var resp = await client.SendAsync(loginReq);

            // 2) POST olmazsa GET Basic Auth + x-api-key
            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[PRON] POST login failed status={(int)resp.StatusCode}. Trying GET BasicAuth...");

                var authBytes = Encoding.ASCII.GetBytes($"{FluentUser}:{FluentPass}");
                var authString = Convert.ToBase64String(authBytes);

                var req = new HttpRequestMessage(HttpMethod.Get, "https://thefluent.me/api/swagger/login");
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
                req.Headers.Add("x-api-key", FluentKey);

                resp = await client.SendAsync(req);
            }

            var body = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"[PRON] Login resp status={(int)resp.StatusCode} body={body}");

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Login Başarısız! Hata: {body}");

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
                throw new Exception("Token alınamadı. Beklenmeyen login response formatı: " + body);
            }

            if (string.IsNullOrWhiteSpace(_cachedToken))
                throw new Exception("Token boş geldi.");

            _tokenExpiry = DateTime.Now.AddMinutes(50);
            return _cachedToken!;
        }

        public class CreatePostResponse
        {
            public string? post_id { get; set; }
        }
    }
}
