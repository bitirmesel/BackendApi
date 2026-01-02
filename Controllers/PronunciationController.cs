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

        // --- ŞİMDİLİK HARDCODE (Render ENV'e taşıyabilirsin) ---
        private const string HARDCODED_USER = "meryem.kilic";
        private const string HARDCODED_PASS = "Melv18309";
        private const string HARDCODED_KEY  = "20251224164351-fQaj7AdeKhp-87831";

        private static string _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        public PronunciationController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // Unity -> backend:
        // form.AddBinaryData("audio_file", bytes, "recording.wav", "audio/wav");
        // form.AddField("text", "kedi");
        [HttpPost("check")]
        [RequestSizeLimit(20_000_000)] // emniyet; istersen artır
        public async Task<IActionResult> CheckPronunciation(
            [FromForm(Name = "audio_file")] IFormFile audioFile,
            [FromForm] string text)
        {
            Console.WriteLine($"[PRON] Request arrived | text='{text}'");

            if (audioFile == null || audioFile.Length == 0)
                return BadRequest("Ses dosyası yok (audio_file).");

            if (string.IsNullOrWhiteSpace(text))
                return BadRequest("Text alanı boş.");

            Console.WriteLine($"[PRON] Incoming file: name='{audioFile.FileName}' len={audioFile.Length} ct='{audioFile.ContentType}'");

            // Dosyayı byte[] al (FluentMe tarafında stream/multipart hassasiyetlerini azaltır)
            byte[] wavBytes;
            await using (var ms = new MemoryStream())
            {
                await audioFile.CopyToAsync(ms);
                wavBytes = ms.ToArray();
            }

            // Basit WAV doğrulama: "RIFF"
            if (wavBytes.Length < 12 ||
                wavBytes[0] != (byte)'R' || wavBytes[1] != (byte)'I' || wavBytes[2] != (byte)'F' || wavBytes[3] != (byte)'F')
            {
                return BadRequest("Gönderilen dosya WAV (RIFF) görünmüyor.");
            }

            var client = _httpClientFactory.CreateClient();

            try
            {
                // 1) TOKEN AL
                var token = await GetValidToken(client);
                Console.WriteLine("[PRON] Token OK.");

                // 2) POST OLUŞTUR
                var postContent = new
                {
                    post_title = "Unity Kaydı",
                    post_content = text,
                    post_language_id = "76"
                };

                using var postReq = new HttpRequestMessage(HttpMethod.Post, "https://thefluent.me/api/swagger/post");
                postReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                postReq.Headers.Add("x-access-token", token);

                // Bazı kurulumlarda post/score da x-api-key istiyor
                postReq.Headers.Add("x-api-key", HARDCODED_KEY);

                postReq.Content = JsonContent.Create(postContent);

                Console.WriteLine("[PRON] Creating post...");
                var postResp = await client.SendAsync(postReq);
                var postBody = await postResp.Content.ReadAsStringAsync();

                Console.WriteLine($"[PRON] Post resp status={(int)postResp.StatusCode} body={postBody}");

                if (!postResp.IsSuccessStatusCode)
                {
                    if (postResp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        _cachedToken = null;

                    return StatusCode((int)postResp.StatusCode, "Post Hatası: " + postBody);
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var postObj = JsonSerializer.Deserialize<CreatePostResponse>(postBody, options);

                var postId = postObj?.post_id;
                if (string.IsNullOrWhiteSpace(postId))
                    return StatusCode(500, "Post ID alınamadı. Post response: " + postBody);

                Console.WriteLine($"[PRON] Post ID OK: {postId}");

                // 3) SCORE: multipart/form-data
                using var multipart = new MultipartFormDataContent();

                // Part 1: audio_file (tırnaklı disposition ile)
                var part1 = new ByteArrayContent(wavBytes);
                part1.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                part1.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "\"audio_file\"",
                    FileName = "\"recording.wav\""
                };
                multipart.Add(part1);

                // Part 2: audioFile (bazı swagger jenerasyonları camelCase bekliyor)
                var part2 = new ByteArrayContent(wavBytes);
                part2.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                part2.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "\"audioFile\"",
                    FileName = "\"recording.wav\""
                };
                multipart.Add(part2);

                using var scoreReq = new HttpRequestMessage(HttpMethod.Post, $"https://thefluent.me/api/swagger/score/{postId}");
                scoreReq.Headers.Add("x-access-token", token);
                scoreReq.Headers.Add("x-api-key", HARDCODED_KEY);
                scoreReq.Content = multipart;

                Console.WriteLine("[PRON] Sending audio for scoring...");
                var scoreResp = await client.SendAsync(scoreReq);
                var scoreBody = await scoreResp.Content.ReadAsStringAsync();

                Console.WriteLine($"[PRON] Score resp status={(int)scoreResp.StatusCode} body={scoreBody}");

                if (scoreResp.IsSuccessStatusCode)
                    return Ok(scoreBody);

                if (scoreResp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    _cachedToken = null;

                return StatusCode((int)scoreResp.StatusCode, "Puanlama Hatası: " + scoreBody);
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
            loginReq.Headers.Add("x-api-key", HARDCODED_KEY); // bazı kurulumlar burada da istiyor
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

            using var doc = JsonDocument.Parse(respStr);

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
                throw new Exception("Token alınamadı. Beklenmeyen login response: " + respStr);
            }

            if (string.IsNullOrWhiteSpace(_cachedToken))
                throw new Exception("Token boş geldi.");

            _tokenExpiry = DateTime.Now.AddMinutes(50);
            return _cachedToken;
        }

        public class CreatePostResponse
        {
            public string post_id { get; set; }
        }
    }
}
