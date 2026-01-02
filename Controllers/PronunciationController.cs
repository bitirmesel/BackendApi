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

        // --- TEST için sabit değerler (prod'da ENV önerilir) ---
        private const string HARDCODED_USER = "meryem.kilic";
        private const string HARDCODED_PASS = "Melv18309";
        private const string HARDCODED_KEY  = "20251224164351-fQaj7AdeKhp-87831";

        private static string? _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        private const string FLUENT_BASE  = "https://thefluent.me/api/swagger";
        private const string FLUENT_LOGIN = FLUENT_BASE + "/login";
        private const string FLUENT_POST  = FLUENT_BASE + "/post";

        public PronunciationController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("check")]
        public async Task<IActionResult> CheckPronunciation([FromForm] string text)
        {
            Console.WriteLine($"[PRON] Request arrived | text='{text}'");

            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(new { message = "Text alanı boş." });

            // Unity'de "audio_file", Postman'de bazen "audioFile" kullanıldığı için ikisini de karşıla
            var file = GetAudioFileFromForm();

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Ses dosyası yok. form-data file key: 'audio_file' (Unity) veya 'audioFile' (Postman) olmalı." });

            Console.WriteLine($"[PRON] audio name='{file.FileName}' len={file.Length} contentType='{file.ContentType}'");

            byte[] fileBytes;
            await using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            var client = _httpClientFactory.CreateClient();

            try
            {
                // 1) TOKEN AL
                var token = await GetValidToken(client);

                // 2) POST OLUŞTUR
                var postRes = await CreatePostWithFallback(client, token, text);
                if (!postRes.isSuccess)
                {
                    return StatusCode((int)(postRes.status ?? HttpStatusCode.BadRequest),
                        new { message = "Post Hatası", detail = postRes.body });
                }

                var postId = postRes.postId!;
                Console.WriteLine($"[PRON] Post ID OK: {postId}");

                // 3) SCORE (SESİ GÖNDER)
                var scoreRes = await SendScore(client, token, postId, fileBytes);
                if (!scoreRes.isSuccess)
                {
                    return StatusCode((int)(scoreRes.status ?? HttpStatusCode.BadRequest),
                        new { message = "Puanlama Hatası", detail = scoreRes.body, postId });
                }

                // FluentMe'nin score JSON'unu Unity'ye aynen döndürelim
                return Ok(scoreRes.body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PRON][CRITICAL] {ex}");
                return StatusCode(500, new { message = "Sunucu Hatası", detail = ex.Message });
            }
        }

        private IFormFile? GetAudioFileFromForm()
        {
            if (!Request.HasFormContentType) return null;

            var files = Request.Form.Files;
            if (files == null || files.Count == 0) return null;

            // Önce isimle ara
            var f = files.GetFile("audio_file");
            if (f != null) return f;

            f = files.GetFile("audioFile");
            if (f != null) return f;

            // Hiçbiri değilse ilk dosyayı al (son çare)
            return files[0];
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

            return await SendCreatePost(client, token, attempt2Json);
        }

        private async Task<(bool isSuccess, HttpStatusCode? status, string body, string? postId)>
            SendCreatePost(HttpClient client, string token, string jsonBody)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, FLUENT_POST);

            req.Headers.Add("x-access-token", token);
            req.Headers.Add("x-api-key", HARDCODED_KEY);
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            Console.WriteLine($"[PRON] POST /post payload: {jsonBody}");

            var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[PRON] POST /post status={(int)resp.StatusCode} body={body}");

            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized) _cachedToken = null;
                return (false, resp.StatusCode, body, null);
            }

            // post_id parse
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var obj = JsonSerializer.Deserialize<CreatePostResponse>(body, options);

                if (obj != null && !string.IsNullOrWhiteSpace(obj.post_id))
                    return (true, resp.StatusCode, body, obj.post_id);

                // Bazı API'ler düz string döndürebilir
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
        // FluentMe: Score
        // -------------------------
        private async Task<(bool isSuccess, HttpStatusCode? status, string body)>
            SendScore(HttpClient client, string token, string postId, byte[] wavBytes)
        {
            var scoreUrl = $"{FLUENT_BASE}/score/{postId}";

            using var multipart = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent(wavBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");

            // FluentMe field adı: audio_file olmalı
            multipart.Add(fileContent, "audio_file", "recording.wav");

            using var req = new HttpRequestMessage(HttpMethod.Post, scoreUrl);
            req.Headers.Add("x-access-token", token);
            req.Headers.Add("x-api-key", HARDCODED_KEY);
            req.Content = multipart;

            Console.WriteLine("[PRON] POST /score sending audio...");
            var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[PRON] POST /score status={(int)resp.StatusCode} body={body}");

            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized) _cachedToken = null;
                return (false, resp.StatusCode, body);
            }

            return (true, resp.StatusCode, body);
        }

        // -------------------------
        // Token (POST + GET fallback)
        // -------------------------
        private async Task<string> GetValidToken(HttpClient client)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry)
                return _cachedToken!;

            Console.WriteLine("[PRON] Login attempt...");

            // 1) POST login dene
            var loginJson = JsonSerializer.Serialize(new { username = HARDCODED_USER, password = HARDCODED_PASS });

            using (var loginReq = new HttpRequestMessage(HttpMethod.Post, FLUENT_LOGIN))
            {
                loginReq.Headers.Add("x-api-key", HARDCODED_KEY);
                loginReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                loginReq.Content = new StringContent(loginJson, Encoding.UTF8, "application/json");

                var resp = await client.SendAsync(loginReq);
                var body = await resp.Content.ReadAsStringAsync();

                Console.WriteLine($"[PRON] Login(POST) status={(int)resp.StatusCode} body={body}");

                // Bazı ortamlarda POST 405 dönebiliyor, o zaman GET fallback
                if (resp.IsSuccessStatusCode)
                {
                    _cachedToken = ExtractTokenFromLoginBody(body);
                    _tokenExpiry = DateTime.Now.AddMinutes(50);
                    return _cachedToken!;
                }

                if (resp.StatusCode != HttpStatusCode.MethodNotAllowed && resp.StatusCode != HttpStatusCode.NotFound)
                {
                    // POST başarısız ama 405 değilse yine de GET deneyelim (daha dayanıklı)
                    Console.WriteLine("[PRON] POST login failed; trying GET fallback anyway...");
                }
            }

            // 2) GET BasicAuth + x-api-key
            var authBytes = Encoding.ASCII.GetBytes($"{HARDCODED_USER}:{HARDCODED_PASS}");
            var authString = Convert.ToBase64String(authBytes);

            using (var req = new HttpRequestMessage(HttpMethod.Get, FLUENT_LOGIN))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
                req.Headers.Add("x-api-key", HARDCODED_KEY);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var resp = await client.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();

                Console.WriteLine($"[PRON] Login(GET) status={(int)resp.StatusCode} body={body}");

                if (!resp.IsSuccessStatusCode)
                    throw new Exception("Login Başarısız: " + body);

                _cachedToken = ExtractTokenFromLoginBody(body);
                _tokenExpiry = DateTime.Now.AddMinutes(50);
                return _cachedToken!;
            }
        }

        private static string ExtractTokenFromLoginBody(string body)
        {
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("token", out var t) &&
                t.ValueKind == JsonValueKind.String)
            {
                var tok = t.GetString();
                if (string.IsNullOrWhiteSpace(tok)) throw new Exception("Token boş geldi.");
                return tok!;
            }

            if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                var tok = doc.RootElement.GetString();
                if (string.IsNullOrWhiteSpace(tok)) throw new Exception("Token boş geldi.");
                return tok!;
            }

            throw new Exception("Token alınamadı. Login response: " + body);
        }

        public class CreatePostResponse
        {
            public string? post_id { get; set; }
        }
    }
}
