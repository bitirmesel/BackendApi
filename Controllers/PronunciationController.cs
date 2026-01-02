// Controllers/PronunciationController.cs

using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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

        private const string HARDCODED_USER = "meryem.kilic";
        private const string HARDCODED_PASS = "Melv18309";
        private const string HARDCODED_KEY = "20251224164351-fQaj7AdeKhp-87831";

        private static string? _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        private const string FLUENT_LOGIN = "https://thefluent.me/api/swagger/login";
        private const string FLUENT_POST = "https://thefluent.me/api/swagger/post";
        private const string FLUENT_SCORE = "https://thefluent.me/api/swagger/score/"; // + {postId}

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

                // debug info
                var inputInfo = ReadWavInfoSafe(inputBytes);
                var normInfo = ReadWavInfoSafe(normalizedWav);

                Console.WriteLine($"[PRON] WAV IN :  ch={inputInfo.numChannels} sr={inputInfo.sampleRate} bps={inputInfo.bitsPerSample} bytes={inputInfo.totalBytes}");
                Console.WriteLine($"[PRON] WAV OUT: ch={normInfo.numChannels}  sr={normInfo.sampleRate}  bps={normInfo.bitsPerSample}  bytes={normInfo.totalBytes}");

                // 4) SCORE gönder (WAV)
                // Fluent tarafında field adı bazen "audio_file", bazen "audioFile" olabiliyor.
                var scoreRes = await SendScoreWavWithDualField(client, token, postId, normalizedWav);

                if (!scoreRes.isSuccess)
                {
                    return StatusCode((int)(scoreRes.status ?? HttpStatusCode.BadRequest),
                        new
                        {
                            message = "Puanlama Hatası",
                            detail = scoreRes.body,
                            postId,
                            wavInfo = new
                            {
                                input = new { inputInfo.audioFormat, inputInfo.numChannels, inputInfo.sampleRate, inputInfo.bitsPerSample, inputInfo.totalBytes },
                                normalized = new { normInfo.audioFormat, normInfo.numChannels, normInfo.sampleRate, normInfo.bitsPerSample, normInfo.totalBytes }
                            }
                        });
                }

                // FluentMe score JSON'unu aynen dön (Unity DTO parse edecek)
                return Content(scoreRes.body, "application/json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PRON][CRITICAL] {ex}");
                return StatusCode(500, new { message = "Sunucu Hatası", detail = ex.Message });
            }
        }

        // -------------------------
        // SCORE (WAV) - dual field name
        // -------------------------
        private async Task<(bool isSuccess, HttpStatusCode? status, string body)>
            SendScoreWavWithDualField(HttpClient client, string token, string postId, byte[] wavBytes)
        {
            using var multipart = new MultipartFormDataContent();

            // Part-1: audio_file
            var file1 = new ByteArrayContent(wavBytes);
            file1.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            multipart.Add(file1, "audio_file", "recording.wav");

            // Part-2: audioFile (bazı backend’ler böyle bekliyor)
            var file2 = new ByteArrayContent(wavBytes);
            file2.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            multipart.Add(file2, "audioFile", "recording.wav");

            using var req = new HttpRequestMessage(HttpMethod.Post, FLUENT_SCORE + postId);
            req.Headers.Add("x-access-token", token);
            req.Headers.Add("x-api-key", HARDCODED_KEY);
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = multipart;

            var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[PRON] SCORE status={(int)resp.StatusCode} body={body}");

            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized) _cachedToken = null;
                return (false, resp.StatusCode, body);
            }

            return (true, resp.StatusCode, body);
        }

        // -------------------------
        // FluentMe: Create Post
        // -------------------------
        private async Task<(bool isSuccess, HttpStatusCode? status, string body, string? postId)>
            CreatePostWithFallback(HttpClient client, string token, string text)
        {
            // Deneme-1: int
            var attempt1Json = JsonSerializer.Serialize(new
            {
                post_title = "Unity Kaydı",
                post_content = text,
                post_language_id = 76
            });

            var attempt1 = await SendCreatePost(client, token, attempt1Json);
            if (attempt1.isSuccess) return attempt1;

            // Deneme-2: string
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
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var obj = JsonSerializer.Deserialize<CreatePostResponse>(body, options);

            if (obj != null && !string.IsNullOrWhiteSpace(obj.post_id))
                return (true, resp.StatusCode, body, obj.post_id);

            // bazen "P123..." string gelebilir
            if (LooksLikeJsonString(body))
            {
                var s = JsonSerializer.Deserialize<string>(body);
                if (!string.IsNullOrWhiteSpace(s))
                    return (true, resp.StatusCode, body, s);
            }

            return (false, resp.StatusCode, "Post response parse edilemedi: " + body, null);
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
                return _cachedToken!;

            // 1) POST dene
            var loginJson = JsonSerializer.Serialize(new { username = HARDCODED_USER, password = HARDCODED_PASS });

            using var postReq = new HttpRequestMessage(HttpMethod.Post, FLUENT_LOGIN);
            postReq.Headers.Add("x-api-key", HARDCODED_KEY);
            postReq.Headers.Accept.Clear();
            postReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            postReq.Content = new StringContent(loginJson, Encoding.UTF8, "application/json");

            var postResp = await client.SendAsync(postReq);
            var postBody = await postResp.Content.ReadAsStringAsync();

            Console.WriteLine($"[PRON] Login(POST) status={(int)postResp.StatusCode} body={postBody}");

            // POST başarılıysa parse et
            if (postResp.IsSuccessStatusCode)
            {
                _cachedToken = ExtractTokenFromLoginBody(postBody);
                _tokenExpiry = DateTime.Now.AddMinutes(50);
                return _cachedToken!;
            }

            // 2) POST 405 veya başarısızsa → GET BasicAuth fallback
            var authBytes = Encoding.ASCII.GetBytes($"{HARDCODED_USER}:{HARDCODED_PASS}");
            var authString = Convert.ToBase64String(authBytes);

            using var getReq = new HttpRequestMessage(HttpMethod.Get, FLUENT_LOGIN);
            getReq.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
            getReq.Headers.Add("x-api-key", HARDCODED_KEY);
            getReq.Headers.Accept.Clear();
            getReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var getResp = await client.SendAsync(getReq);
            var getBody = await getResp.Content.ReadAsStringAsync();

            Console.WriteLine($"[PRON] Login(GET) status={(int)getResp.StatusCode} body={getBody}");

            if (!getResp.IsSuccessStatusCode)
                throw new Exception("Login Başarısız: " + getBody);

            _cachedToken = ExtractTokenFromLoginBody(getBody);
            _tokenExpiry = DateTime.Now.AddMinutes(50);
            return _cachedToken!;
        }

        private static string ExtractTokenFromLoginBody(string body)
        {
            using var doc = JsonDocument.Parse(body);

            // { "token": "..." }
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("token", out var t) &&
                t.ValueKind == JsonValueKind.String)
            {
                var token = t.GetString();
                if (!string.IsNullOrWhiteSpace(token)) return token!;
            }

            // "tokenstring"
            if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                var token = doc.RootElement.GetString();
                if (!string.IsNullOrWhiteSpace(token)) return token!;
            }

            throw new Exception("Token alınamadı. Login response: " + body);
        }


        // -------------------------
        // WAV normalize: 16kHz mono PCM16
        // -------------------------
        private static byte[] ConvertTo16kMonoPcm16(byte[] wavBytes)
        {
            using var inputMs = new MemoryStream(wavBytes);
            using var reader = new WaveFileReader(inputMs);

            ISampleProvider sampleProvider = reader.ToSampleProvider();

            // Mono
            if (sampleProvider.WaveFormat.Channels > 1)
            {
                sampleProvider = new StereoToMonoSampleProvider(sampleProvider)
                {
                    LeftVolume = 0.5f,
                    RightVolume = 0.5f
                };
            }

            // 16kHz resample
            if (sampleProvider.WaveFormat.SampleRate != 16000)
            {
                sampleProvider = new WdlResamplingSampleProvider(sampleProvider, 16000);
            }

            var outFormat = new WaveFormat(16000, 16, 1);

            using var outMs = new MemoryStream();
            using (var writer = new WaveFileWriter(outMs, outFormat))
            {
                var pcm16 = new SampleToWaveProvider16(sampleProvider);

                byte[] buffer = new byte[pcm16.WaveFormat.AverageBytesPerSecond]; // ~1s
                int read;
                while ((read = pcm16.Read(buffer, 0, buffer.Length)) > 0)
                {
                    writer.Write(buffer, 0, read);
                }
            }

            return outMs.ToArray();
        }

        private static (int audioFormat, int numChannels, int sampleRate, int bitsPerSample, int totalBytes)
            ReadWavInfoSafe(byte[] wavBytes)
        {
            try
            {
                using var ms = new MemoryStream(wavBytes);
                using var r = new WaveFileReader(ms);
                var wf = r.WaveFormat;

                int audioFormat = wf.Encoding == WaveFormatEncoding.Pcm ? 1 : 0;

                return (audioFormat, wf.Channels, wf.SampleRate, wf.BitsPerSample, wavBytes.Length);
            }
            catch
            {
                // WAV değilse (Unity bazen raw gönderebilir) en azından bytes dönelim
                return (0, 0, 0, 0, wavBytes.Length);
            }
        }

        public class CreatePostResponse
        {
            public string? post_id { get; set; }
        }
    }
}
