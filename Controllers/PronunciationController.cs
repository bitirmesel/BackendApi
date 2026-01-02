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

        // --- TEST İÇİN SABİT DEĞERLER (ENV yerine) ---
        private const string HARDCODED_USER = "meryem.kilic";
        private const string HARDCODED_PASS = "Melv18309";
        private const string HARDCODED_KEY  = "20251224164351-fQaj7AdeKhp-87831";

        private static string? _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        private const string FLUENT_LOGIN = "https://thefluent.me/api/swagger/login";
        private const string FLUENT_POST  = "https://thefluent.me/api/swagger/post";
        private static string FluentScore(string postId) => $"https://thefluent.me/api/swagger/score/{postId}";

        public PronunciationController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("check")]
        public async Task<IActionResult> CheckPronunciation([FromForm] string text)
        {
            // 1) Dosyayı hem "audioFile" hem "audio_file" olarak kabul et (Unity vs Postman).
            var file = ResolveIncomingAudioFile();
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Ses dosyası yok. (audioFile veya audio_file bekleniyor)" });

            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(new { message = "Text alanı boş." });

            // 2) Byte[] al (score'a bunu basacağız)
            byte[] fileBytes;
            await using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            // 3) WAV hızlı kontrol (PCM16 mi?)
            var wavInfo = TryParseWavHeader(fileBytes);
            if (wavInfo == null)
            {
                return BadRequest(new
                {
                    message = "Geçersiz WAV",
                    detail = "Dosya RIFF/WAVE değil veya header okunamadı.",
                    receivedContentType = file.ContentType,
                    fileName = file.FileName,
                    fileLen = file.Length
                });
            }

            // En güvenlisi: 16-bit PCM (audioFormat=1)
            if (wavInfo.AudioFormat != 1 || wavInfo.BitsPerSample != 16)
            {
                return BadRequest(new
                {
                    message = "Desteklenmeyen WAV encoding",
                    detail = "LINEAR16 (PCM 16-bit) olmalı. (audioFormat=1, bits=16)",
                    wavInfo
                });
            }

            // Mono önerilir
            if (wavInfo.NumChannels != 1)
            {
                return BadRequest(new
                {
                    message = "Desteklenmeyen kanal sayısı",
                    detail = "Mono (1 kanal) önerilir/çoğu STT bekler.",
                    wavInfo
                });
            }

            // Sample rate: 8000/16000/44100 vs kabul edilebilir ama 16000 en sağlıklısı.
            // Burada bloklamıyorum; sadece log/geri bildirim için bırakıyorum.

            var client = _httpClientFactory.CreateClient();

            try
            {
                var token = await GetValidToken(client);

                // 4) Post oluştur (post_language_id tip fallback)
                var postCreate = await CreatePostWithFallback(client, token, text);
                if (!postCreate.isSuccess)
                {
                    return StatusCode((int)(postCreate.status ?? HttpStatusCode.BadRequest),
                        new { message = "Post Hatası", detail = postCreate.body });
                }

                var postId = postCreate.postId!;
                Console.WriteLine($"[PRON] Post created OK: {postId}");

                // 5) Score çağır
                var score = await SendScoreRequest(client, token, postId, fileBytes, file.FileName);

                if (!score.isSuccess)
                {
                    return StatusCode((int)(score.status ?? HttpStatusCode.BadRequest),
                        new { message = "Puanlama Hatası", detail = score.body, postId, wavInfo });
                }

                // Başarılı: FluentMe score JSON’u ne döndürüyorsa aynen ilet
                return Ok(score.body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PRON][CRITICAL] {ex}");
                return StatusCode(500, new { message = "Sunucu Hatası", detail = ex.Message });
            }
        }

        // -------------------------
        // Incoming file resolver (audioFile OR audio_file)
        // -------------------------
        private IFormFile? ResolveIncomingAudioFile()
        {
            // Model binding bazen sadece Request.Form.Files ile yakalanıyor
            var files = Request.Form?.Files;
            if (files == null || files.Count == 0) return null;

            // Önce isme göre ara
            var f = files.GetFile("audioFile");
            if (f != null) return f;

            f = files.GetFile("audio_file");
            if (f != null) return f;

            // İkisi de yoksa ilk dosyayı al
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

                // Bazen düz string dönebilir
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

        private async Task<(bool isSuccess, HttpStatusCode? status, string body)>
            SendScoreRequest(HttpClient client, string token, string postId, byte[] wavBytes, string originalFileName)
        {
            using var multipart = new MultipartFormDataContent();

            // Aynı byte[]’ı iki farklı field adıyla (audio_file ve audioFile) ekleyelim.
            // Bazı backend’ler isim konusunda çok katı olabiliyor.
            var safeFileName = string.IsNullOrWhiteSpace(originalFileName) ? "recording.wav" : originalFileName;
            if (!safeFileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                safeFileName += ".wav";

            // audio_file
            var c1 = new ByteArrayContent(wavBytes);
            c1.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
            multipart.Add(c1, "audio_file", safeFileName);

            // audioFile
            var c2 = new ByteArrayContent(wavBytes);
            c2.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
            multipart.Add(c2, "audioFile", safeFileName);

            using var req = new HttpRequestMessage(HttpMethod.Post, FluentScore(postId));
            req.Headers.Add("x-access-token", token);
            req.Headers.Add("x-api-key", HARDCODED_KEY);
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = multipart;

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

        private static bool LooksLikeJsonString(string s)
        {
            s = (s ?? "").Trim();
            return s.StartsWith("\"") && s.EndsWith("\"");
        }

        // -------------------------
        // Token
        // -------------------------
        private async Task<string> GetValidToken(HttpClient client)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry)
                return _cachedToken!;

            Console.WriteLine("[PRON] Login attempt...");

            // Bazı ortamlarda POST login 405 dönebiliyor.
            // Bu yüzden önce POST dene, olmazsa GET BasicAuth fallback yap.
            var loginJson = JsonSerializer.Serialize(new { username = HARDCODED_USER, password = HARDCODED_PASS });

            using var postReq = new HttpRequestMessage(HttpMethod.Post, FLUENT_LOGIN);
            postReq.Headers.Add("x-api-key", HARDCODED_KEY);
            postReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            postReq.Content = new StringContent(loginJson, Encoding.UTF8, "application/json");

            var resp = await client.SendAsync(postReq);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[PRON] POST login failed {(int)resp.StatusCode}. Trying GET BasicAuth...");

                var authBytes = Encoding.ASCII.GetBytes($"{HARDCODED_USER}:{HARDCODED_PASS}");
                var authString = Convert.ToBase64String(authBytes);

                using var getReq = new HttpRequestMessage(HttpMethod.Get, FLUENT_LOGIN);
                getReq.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
                getReq.Headers.Add("x-api-key", HARDCODED_KEY);
                getReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                resp = await client.SendAsync(getReq);
                body = await resp.Content.ReadAsStringAsync();
            }

            Console.WriteLine($"[PRON] Login status={(int)resp.StatusCode} body={body}");

            if (!resp.IsSuccessStatusCode)
                throw new Exception("Login Başarısız: " + body);

            // Token parse
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
            return _cachedToken!;
        }

        // -------------------------
        // WAV Header quick parse (RIFF/WAVE + fmt )
        // -------------------------
        private static WavInfo? TryParseWavHeader(byte[] data)
        {
            try
            {
                if (data.Length < 44) return null;

                // "RIFF"
                if (data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F') return null;
                // "WAVE"
                if (data[8] != 'W' || data[9] != 'A' || data[10] != 'V' || data[11] != 'E') return null;

                // fmt chunk genelde 12'den başlar ama bazen ekstra chunk girer.
                // Minimal/robust: "fmt " arayalım.
                int idx = 12;
                int fmtIndex = -1;

                while (idx + 8 <= data.Length)
                {
                    var chunkId = Encoding.ASCII.GetString(data, idx, 4);
                    int chunkSize = BitConverter.ToInt32(data, idx + 4);

                    if (chunkId == "fmt ")
                    {
                        fmtIndex = idx;
                        break;
                    }

                    idx += 8 + chunkSize;
                    if (idx < 0 || idx >= data.Length) break;
                }

                if (fmtIndex < 0) return null;

                int fmtSize = BitConverter.ToInt32(data, fmtIndex + 4);
                if (fmtIndex + 8 + fmtSize > data.Length) return null;

                short audioFormat = BitConverter.ToInt16(data, fmtIndex + 8);
                short numChannels = BitConverter.ToInt16(data, fmtIndex + 10);
                int sampleRate = BitConverter.ToInt32(data, fmtIndex + 12);
                // int byteRate = BitConverter.ToInt32(data, fmtIndex + 16);
                // short blockAlign = BitConverter.ToInt16(data, fmtIndex + 20);
                short bitsPerSample = BitConverter.ToInt16(data, fmtIndex + 22);

                return new WavInfo
                {
                    AudioFormat = audioFormat,
                    NumChannels = numChannels,
                    SampleRate = sampleRate,
                    BitsPerSample = bitsPerSample,
                    TotalBytes = data.Length
                };
            }
            catch
            {
                return null;
            }
        }
    }

    public class CreatePostResponse
    {
        public string? post_id { get; set; }
    }

    public class WavInfo
    {
        public short AudioFormat { get; set; }     // PCM = 1
        public short NumChannels { get; set; }     // Mono = 1
        public int SampleRate { get; set; }        // 16000 önerilir
        public short BitsPerSample { get; set; }   // 16 önerilir
        public int TotalBytes { get; set; }
    }
}
