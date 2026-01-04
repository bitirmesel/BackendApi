// Controllers/PronunciationController.cs

using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DktApi.Services; // CloudinaryService için gerekli

// NAudio
using NAudio.Wave;
using NAudio.Wave.SampleProviders;


namespace DktApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PronunciationController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly CloudinaryService _cloudinary;

        private const string HARDCODED_USER = "meryem.kilic";
        private const string HARDCODED_PASS = "Melv18309";
        private const string HARDCODED_KEY = "20251224164351-fQaj7AdeKhp-87831";

        private static string? _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        private const string FLUENT_LOGIN = "https://thefluent.me/api/swagger/login";
        private const string FLUENT_POST = "https://thefluent.me/api/swagger/post";
        private const string FLUENT_SCORE = "https://thefluent.me/api/swagger/score/"; // + {postId}

        // TEK CONSTRUCTOR
        public PronunciationController(IHttpClientFactory httpClientFactory, CloudinaryService cloudinary)
        {
            _httpClientFactory = httpClientFactory;
            _cloudinary = cloudinary;
        }

        [HttpPost("check")]
        public async Task<IActionResult> CheckPronunciation([FromForm] string text)
        {
            Console.WriteLine($"[PRON] Request arrived | text='{text}'");

            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(new { message = "Text alanı boş." });

            // audio_file (Unity) veya audioFile (Postman)
            IFormFile? audioFile = null;

            if (Request.Form?.Files != null && Request.Form.Files.Count > 0)
            {
                audioFile =
                    Request.Form.Files.GetFile("audio_file") ??
                    Request.Form.Files.GetFile("audioFile") ??
                    Request.Form.Files.GetFile("audio") ??
                    Request.Form.Files.FirstOrDefault();
            }

            if (audioFile == null || audioFile.Length == 0)
                return BadRequest(new { message = "Ses dosyası yok. form-data'da audio_file / audioFile gönder." });

            // input bytes
            byte[] inputBytes;
            await using (var ms = new MemoryStream())
            {
                await audioFile.CopyToAsync(ms);
                inputBytes = ms.ToArray();
            }

            var client = _httpClientFactory.CreateClient();

            try
            {
                // 1) Token
                var token = await GetValidToken(client);

                // 2) Post oluştur
                var postRes = await CreatePostWithFallback(client, token, text);
                if (!postRes.isSuccess)
                {
                    return StatusCode((int)(postRes.status ?? HttpStatusCode.BadRequest),
                        new { message = "Post Hatası", detail = postRes.body });
                }

                var postId = postRes.postId!;
                Console.WriteLine($"[PRON] Post ID OK: {postId}");

                // 3) WAV normalize: 16kHz mono PCM16 WAV
                var normalizedWav = ConvertTo16kMonoPcm16(inputBytes);

                // Cloudinary'ye yükle (Değişken ismini düzelttim)
                var audioUrl = await _cloudinary.UploadAudioAsync(normalizedWav, "recording.wav");
                Console.WriteLine("[PRON] Uploaded WAV URL: " + audioUrl);

                // 4) SCORE'u URL ile gönder
                var scoreJson = await SendScoreToFluentMeByUrl(client, token, postId, audioUrl);

                // FluentMe score JSON'unu aynen dön
                return Content(scoreJson, "application/json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PRON][CRITICAL] {ex}");
                return StatusCode(500, new { message = "Sunucu Hatası", detail = ex.Message });
            }
        }

        private async Task<string> SendScoreToFluentMeByUrl(HttpClient client, string token, string postId, string audioUrl)
        {
            var url = $"{FLUENT_SCORE}{postId}?scale=100";

            using var req = new HttpRequestMessage(HttpMethod.Post, url);

            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.Add("x-access-token", token);
            req.Headers.Add("x-api-key", HARDCODED_KEY);

            var bodyJson = JsonSerializer.Serialize(new { audio_provided = audioUrl });
            req.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            using var resp = await client.SendAsync(req);
            var respBody = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[PRON] SCORE(URL) status={(int)resp.StatusCode} body={respBody}");

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"FluentMe score failed: {(int)resp.StatusCode} - {respBody}");

            return respBody;
        }

        private async Task<(bool isSuccess, HttpStatusCode? status, string body, string? postId)>
            CreatePostWithFallback(HttpClient client, string token, string text)
        {
            var attempt1Json = JsonSerializer.Serialize(new
            {
                post_title = "Unity Kaydı",
                post_content = text,
                post_language_id = 76
            });

            var attempt1 = await SendCreatePost(client, token, attempt1Json);
            if (attempt1.isSuccess) return attempt1;

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

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var obj = JsonSerializer.Deserialize<CreatePostResponse>(body, options);

            if (obj != null && !string.IsNullOrWhiteSpace(obj.post_id))
                return (true, resp.StatusCode, body, obj.post_id);

            return (false, resp.StatusCode, "Post response parse edilemedi: " + body, null);
        }

        private async Task<string> GetValidToken(HttpClient client)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry)
                return _cachedToken!;

            var loginJson = JsonSerializer.Serialize(new { username = HARDCODED_USER, password = HARDCODED_PASS });

            using var postReq = new HttpRequestMessage(HttpMethod.Post, FLUENT_LOGIN);
            postReq.Headers.Add("x-api-key", HARDCODED_KEY);
            postReq.Headers.Accept.Clear();
            postReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            postReq.Content = new StringContent(loginJson, Encoding.UTF8, "application/json");

            var postResp = await client.SendAsync(postReq);
            var postBody = await postResp.Content.ReadAsStringAsync();

            if (postResp.IsSuccessStatusCode)
            {
                _cachedToken = ExtractTokenFromLoginBody(postBody);
                _tokenExpiry = DateTime.Now.AddMinutes(50);
                return _cachedToken!;
            }

            var authBytes = Encoding.ASCII.GetBytes($"{HARDCODED_USER}:{HARDCODED_PASS}");
            var authString = Convert.ToBase64String(authBytes);

            using var getReq = new HttpRequestMessage(HttpMethod.Get, FLUENT_LOGIN);
            getReq.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
            getReq.Headers.Add("x-api-key", HARDCODED_KEY);
            getReq.Headers.Accept.Clear();
            getReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var getResp = await client.SendAsync(getReq);
            var getBody = await getResp.Content.ReadAsStringAsync();

            if (!getResp.IsSuccessStatusCode)
                throw new Exception("Login Başarısız: " + getBody);

            _cachedToken = ExtractTokenFromLoginBody(getBody);
            _tokenExpiry = DateTime.Now.AddMinutes(50);
            return _cachedToken!;
        }

        private static string ExtractTokenFromLoginBody(string body)
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("token", out var t) &&
                t.ValueKind == JsonValueKind.String)
            {
                return t.GetString()!;
            }
            if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                return doc.RootElement.GetString()!;
            }
            throw new Exception("Token alınamadı.");
        }

        private static byte[] ConvertTo16kMonoPcm16(byte[] wavBytes)
        {
            using var inputMs = new MemoryStream(wavBytes);
            using var reader = new WaveFileReader(inputMs);
            ISampleProvider sampleProvider = reader.ToSampleProvider();

            if (sampleProvider.WaveFormat.Channels > 1)
            {
                sampleProvider = new StereoToMonoSampleProvider(sampleProvider);
            }

            if (sampleProvider.WaveFormat.SampleRate != 16000)
            {
                sampleProvider = new WdlResamplingSampleProvider(sampleProvider, 16000);
            }

            using var outMs = new MemoryStream();
            using (var writer = new WaveFileWriter(outMs, new WaveFormat(16000, 16, 1)))
            {
                var pcm16 = new SampleToWaveProvider16(sampleProvider);
                byte[] buffer = new byte[pcm16.WaveFormat.AverageBytesPerSecond];
                int read;
                while ((read = pcm16.Read(buffer, 0, buffer.Length)) > 0)
                {
                    writer.Write(buffer, 0, read);
                }
            }
            return outMs.ToArray();
        }

        public class CreatePostResponse
        {
            public string? post_id { get; set; }
        }
    }
}