// Controllers/PronunciationController.cs

using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;

namespace DktApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PronunciationController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        // --- ŞİMDİLİK HARDCODE (sonra ENV'e taşırsın) ---
        private const string HARDCODED_USER = "meryem.kilic";
        private const string HARDCODED_PASS = "Melv18309";
        private const string HARDCODED_KEY  = "20251224164351-fQaj7AdeKhp-87831";

        private static string _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        public PronunciationController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // Unity:
        // form.AddBinaryData("audio_file", bytes, "recording.wav", "audio/wav");
        // form.AddField("text", "kedi kedi kedi");
        //
        // Postman:
        // form-data: audioFile (File) + text (Text)
        [HttpPost("check")]
        public async Task<IActionResult> CheckPronunciation(
            // Postman key "audioFile" ile gönderiyor, Unity key "audio_file".
            // Bu yüzden ikisini de yakalamak için audioFile param'ını default bırakıp,
            // model binder her iki ismi de yakalayabilsin diye [FromForm] + Name kullanmıyoruz.
            [FromForm] IFormFile audioFile,
            [FromForm] string text
        )
        {
            Console.WriteLine($"[PRON] Request arrived | text='{text}'");

            if (audioFile == null || audioFile.Length == 0)
                return BadRequest(new { message = "Ses dosyası yok (audioFile/audio_file)." });

            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(new { message = "Text alanı boş." });

            Console.WriteLine($"[PRON] Incoming file: name='{audioFile.FileName}' len={audioFile.Length} ct='{audioFile.ContentType}'");

            // Dosyayı byte[] olarak al (stream'i birden fazla fallback istekte kullanacağız)
            byte[] fileBytes;
            await using (var ms = new MemoryStream())
            {
                await audioFile.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            // Basit WAV sanity check (RIFF/WAVE)
            if (!LooksLikeWav(fileBytes))
            {
                Console.WriteLine("[PRON][WARN] Incoming file does NOT look like WAV (RIFF/WAVE).");
                // Yine de deneyelim ama en azından log kalsın
            }

            var client = _httpClientFactory.CreateClient();

            try
            {
                // 1) TOKEN
                var token = await GetValidToken(client);
                Console.WriteLine("[PRON] Token OK.");

                // 2) POST OLUŞTUR
                var postContent = new
                {
                    post_title = "Unity Kaydı",
                    post_content = text,
                    post_language_id = 76 // string değil int denemek daha güvenli
                };

                using var postReq = new HttpRequestMessage(HttpMethod.Post, "https://thefluent.me/api/swagger/post");
                postReq.Headers.Add("x-access-token", token);
                postReq.Headers.Add("x-api-key", HARDCODED_KEY);
                postReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                postReq.Content = JsonContent.Create(postContent);

                Console.WriteLine("[PRON] Creating post...");
                var postResp = await client.SendAsync(postReq);
                var postRespStr = await postResp.Content.ReadAsStringAsync();

                Console.WriteLine($"[PRON] Post resp status={(int)postResp.StatusCode} body={postRespStr}");

                if (!postResp.IsSuccessStatusCode)
                {
                    if (postResp.StatusCode == HttpStatusCode.Unauthorized) _cachedToken = null;
                    return StatusCode((int)postResp.StatusCode, new { message = "Post Hatası", detail = postRespStr });
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var postObj = JsonSerializer.Deserialize<CreatePostResponse>(postRespStr, options);
                var postId = postObj?.post_id;

                if (string.IsNullOrWhiteSpace(postId))
                    return StatusCode(500, new { message = "Post ID alınamadı (post_id boş).", detail = postRespStr });

                Console.WriteLine($"[PRON] Post ID OK: {postId}");

                // 3) SCORE - 1. deneme: multipart (birden fazla alan adıyla)
                var scoreUrl = $"https://thefluent.me/api/swagger/score/{postId}";
                var scoreAttempt1 = await TryScoreMultipart(client, scoreUrl, token, fileBytes);

                if (scoreAttempt1.isSuccess)
                    return Ok(scoreAttempt1.body);

                Console.WriteLine($"[PRON] Multipart score failed. status={scoreAttempt1.status} body={scoreAttempt1.body}");

                // 4) SCORE - 2. deneme: raw body (bazı servisler multipart sevmez)
                var scoreAttempt2 = await TryScoreRaw(client, scoreUrl, token, fileBytes);

                if (scoreAttempt2.isSuccess)
                    return Ok(scoreAttempt2.body);

                Console.WriteLine($"[PRON] Raw score failed. status={scoreAttempt2.status} body={scoreAttempt2.body}");

                // İkisi de başarısızsa FluentMe'nin verdiği hatayı olduğu gibi dön
                return StatusCode(
                    (int)(scoreAttempt2.status ?? scoreAttempt1.status ?? HttpStatusCode.BadRequest),
                    new
                    {
                        message = "Puanlama Hatası",
                        detail = scoreAttempt2.body ?? scoreAttempt1.body
                    }
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PRON][CRITICAL] {ex}");
                return StatusCode(500, new { message = "Sunucu Hatası", detail = ex.Message });
            }
        }

        // -------------------------
        // FluentMe SCORE helpers
        // -------------------------
        private async Task<(bool isSuccess, HttpStatusCode? status, string body)> TryScoreMultipart(
            HttpClient client, string url, string token, byte[] wavBytes)
        {
            using var multipart = new MultipartFormDataContent();

            // Aynı dosyayı farklı field isimleriyle ekleyelim (API naming tutarsızlığı için)
            // Not: Her StreamContent ayrı instance olmalı.
            multipart.Add(MakeFileContent(wavBytes), "audio_file", "recording.wav");
            multipart.Add(MakeFileContent(wavBytes), "audioFile",  "recording.wav");
            multipart.Add(MakeFileContent(wavBytes), "file",       "recording.wav");

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("x-access-token", token);
            req.Headers.Add("x-api-key", HARDCODED_KEY);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = multipart;

            var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            return (resp.IsSuccessStatusCode, resp.StatusCode, body);
        }

        private async Task<(bool isSuccess, HttpStatusCode? status, string body)> TryScoreRaw(
            HttpClient client, string url, string token, byte[] wavBytes)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("x-access-token", token);
            req.Headers.Add("x-api-key", HARDCODED_KEY);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // raw binary
            var content = new ByteArrayContent(wavBytes);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
            req.Content = content;

            var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            return (resp.IsSuccessStatusCode, resp.StatusCode, body);
        }

        private static StreamContent MakeFileContent(byte[] bytes)
        {
            var sc = new StreamContent(new MemoryStream(bytes));
            sc.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
            return sc;
        }

        private static bool LooksLikeWav(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 12) return false;

            // "RIFF" .... "WAVE"
            return bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
                && bytes[8] == (byte)'W' && bytes[9] == (byte)'A' && bytes[10] == (byte)'V' && bytes[11] == (byte)'E';
        }

        // -------------------------
        // Token
        // -------------------------
        private async Task<string> GetValidToken(HttpClient client)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry)
                return _cachedToken;

            Console.WriteLine("[PRON] Login attempt (hardcoded credentials)...");

            // 1) POST JSON login
            var loginData = new { username = HARDCODED_USER, password = HARDCODED_PASS };
            using var loginReq = new HttpRequestMessage(HttpMethod.Post, "https://thefluent.me/api/swagger/login");
            loginReq.Headers.Add("x-api-key", HARDCODED_KEY);
            loginReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            loginReq.Content = JsonContent.Create(loginData);

            var response = await client.SendAsync(loginReq);

            // 2) POST olmazsa GET Basic Auth + x-api-key
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[PRON] POST login failed status={(int)response.StatusCode}. Trying GET BasicAuth...");

                var authBytes = Encoding.ASCII.GetBytes($"{HARDCODED_USER}:{HARDCODED_PASS}");
                var authString = Convert.ToBase64String(authBytes);

                var request = new HttpRequestMessage(HttpMethod.Get, "https://thefluent.me/api/swagger/login");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
                request.Headers.Add("x-api-key", HARDCODED_KEY);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                response = await client.SendAsync(request);
            }

            var respStr = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[PRON] Login resp status={(int)response.StatusCode} body={respStr}");

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Login Başarısız! Hata: {respStr}");

            // Token parse
            using (JsonDocument doc = JsonDocument.Parse(respStr))
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
                    throw new Exception("Token alınamadı. Beklenmeyen login response: " + respStr);
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
