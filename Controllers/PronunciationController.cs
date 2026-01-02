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

        // TEST İÇİN SABİT
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
        public async Task<IActionResult> CheckPronunciation([FromForm] string text)
        {
            Console.WriteLine($"[PRON] Request arrived | text='{text}'");

            try
            {
                // FORM'u oku ve debug log bas
                var form = await Request.ReadFormAsync();

                Console.WriteLine($"[PRON] Form keys: {string.Join(", ", form.Keys)}");
                Console.WriteLine($"[PRON] Files count: {form.Files.Count}");
                foreach (var f in form.Files)
                    Console.WriteLine($"[PRON] File field='{f.Name}' filename='{f.FileName}' len={f.Length} ct='{f.ContentType}'");

                // Unity bazen audio_file bazen audioFile gönderebilir: ikisini de kabul et
                IFormFile audioFile =
                    form.Files.GetFile("audio_file") ??
                    form.Files.GetFile("audioFile") ??
                    form.Files.FirstOrDefault();

                if (audioFile == null || audioFile.Length == 0)
                    return BadRequest(new { message = "Ses dosyası yok. Beklenen alan: audio_file veya audioFile." });

                if (string.IsNullOrWhiteSpace(text))
                    return BadRequest(new { message = "Text alanı boş." });

                var client = _httpClientFactory.CreateClient();

                // 1) TOKEN
                string token = await GetValidToken(client);
                Console.WriteLine("[PRON] Token OK.");

                // 2) POST oluştur
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
                var postRespStr = await postResp.Content.ReadAsStringAsync();
                Console.WriteLine($"[PRON] Post resp status={(int)postResp.StatusCode} body={postRespStr}");

                if (!postResp.IsSuccessStatusCode)
                {
                    if (postResp.StatusCode == System.Net.HttpStatusCode.Unauthorized) _cachedToken = null;
                    return StatusCode((int)postResp.StatusCode, "Post Hatası: " + postRespStr);
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var postObj = JsonSerializer.Deserialize<CreatePostResponse>(postRespStr, options);
                var postId = postObj?.post_id;

                if (string.IsNullOrWhiteSpace(postId))
                    return StatusCode(500, "Post ID alınamadı (post_id boş). Post response: " + postRespStr);

                Console.WriteLine($"[PRON] Post ID OK: {postId}");

                // 3) SCORE isteği (multipart + token)
                using var multipart = new MultipartFormDataContent();

                await using var stream = audioFile.OpenReadStream();
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");

                multipart.Add(fileContent, "audio_file", "recording.wav");

                using var scoreReq = new HttpRequestMessage(HttpMethod.Post, $"https://thefluent.me/api/swagger/score/{postId}");
                scoreReq.Headers.Add("x-access-token", token);
                scoreReq.Content = multipart;

                Console.WriteLine("[PRON] Sending audio for scoring...");
                var scoreResp = await client.SendAsync(scoreReq);
                var scoreStr = await scoreResp.Content.ReadAsStringAsync();
                Console.WriteLine($"[PRON] Score resp status={(int)scoreResp.StatusCode} body={scoreStr}");

                if (scoreResp.IsSuccessStatusCode)
                    return Ok(scoreStr);

                if (scoreResp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    _cachedToken = null;

                return StatusCode((int)scoreResp.StatusCode, "Puanlama Hatası: " + scoreStr);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PRON][CRITICAL] {ex}");
                return StatusCode(500, "Sunucu Hatası: " + ex.Message);
            }
        }

        private async Task<string> GetValidToken(HttpClient client)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry)
                return _cachedToken;

            Console.WriteLine("[PRON] Login attempt...");

            // 1) JSON POST login
            var loginData = new { username = HARDCODED_USER, password = HARDCODED_PASS };

            using var loginReq = new HttpRequestMessage(HttpMethod.Post, "https://thefluent.me/api/swagger/login");
            loginReq.Content = JsonContent.Create(loginData);

            var response = await client.SendAsync(loginReq);

            // 2) POST olmazsa GET BasicAuth + x-api-key
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[PRON] POST login failed status={(int)response.StatusCode}. Trying GET BasicAuth...");

                var authBytes = Encoding.ASCII.GetBytes($"{HARDCODED_USER}:{HARDCODED_PASS}");
                var authString = Convert.ToBase64String(authBytes);

                var request = new HttpRequestMessage(HttpMethod.Get, "https://thefluent.me/api/swagger/login");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
                request.Headers.Add("x-api-key", HARDCODED_KEY);

                response = await client.SendAsync(request);
            }

            var respStr = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[PRON] Login resp status={(int)response.StatusCode} body={respStr}");

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Login Başarısız! Hata: {respStr}");

            using (var doc = JsonDocument.Parse(respStr))
            {
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
                    throw new Exception("Token alınamadı. Beklenmeyen login response formatı: " + respStr);
                }
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
