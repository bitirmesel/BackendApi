// Controllers/PronunciationController.cs

using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

// NAudio (NuGet: NAudio)
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DktApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PronunciationController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        // --- TEST İÇİN SABİT DEĞERLER (ENV yerine) ---
        // PROD'da bunları kesinlikle ENV'e taşı.
        private const string HARDCODED_USER = "meryem.kilic";
        private const string HARDCODED_PASS = "Melv18309";
        private const string HARDCODED_KEY  = "20251224164351-fQaj7AdeKhp-87831";

        private static string _cachedToken = "";
        private static DateTime _tokenExpiry = DateTime.MinValue;

        private const string FLUENT_LOGIN = "https://thefluent.me/api/swagger/login";
        private const string FLUENT_POST  = "https://thefluent.me/api/swagger/post";

        public PronunciationController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // Unity tarafı (şu an):
        // form.AddBinaryData("audio_file", bytes, "recording.wav", "audio/wav");
        // form.AddField("text", "kedi");
        //
        // Postman için de:
        // form-data:
        //  - audioFile (File)  veya audio_file (File)
        //  - text (Text)
        [HttpPost("check")]
        public async Task<IActionResult> CheckPronunciation(
            [FromForm] IFormFile? audioFile,
            [FromForm(Name = "audio_file")] IFormFile? audio_file,
            [FromForm] string text
        )
        {
            Console.WriteLine($"[PRON] Request arrived | text='{text}'");

            // audioFile veya audio_file hangisi geldiyse onu kullan
            var file = audioFile ?? audio_file;

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Ses dosyası yok (audioFile veya audio_file alanı)." });

            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(new { message = "Text alanı boş." });

            byte[] fileBytes;
            await using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            // Debug: gelen wav header bilgisi
            var inInfo = TryParseWavHeader(fileBytes);
            Console.WriteLine($"[PRON] IN file='{file.FileName}' len={file.Length} contentType='{file.ContentType}' " +
                              $"wav: fmt={inInfo?.AudioFormat} ch={inInfo?.NumChannels} hz={inInfo?.SampleRate} bps={inInfo?.BitsPerSample}");

            var client = _httpClientFactory.CreateClient();

            try
            {
                // 1) Token
                var token = await GetValidToken(client);

                // 2) Post create
                var postIdResult = await CreatePostWithFallback(client, token, text);
                if (!postIdResult.isSuccess)
                {
                    return StatusCode((int)(postIdResult.status ?? HttpStatusCode.BadRequest),
                        new { message = "Post Hatası", detail = postIdResult.body });
                }

                var postId = postIdResult.postId!;
                Console.WriteLine($"[PRON] Post ID OK: {postId}");

                // 3) Score için WAV normalize: 16kHz / mono / PCM16
                byte[] normalizedWav;
                try
                {
                    normalizedWav = ConvertTo16kMonoPcm16(fileBytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PRON][ERROR] WAV normalize failed: {ex.Message}");
                    return BadRequest(new
                    {
                        message = "WAV dönüştürme hatası",
                        detail = ex.Message,
                        postId,
                        wavInfo = inInfo
                    });
                }

                var outInfo = TryParseWavHeader(normalizedWav);
                Console.WriteLine($"[PRON] OUT normalized wav: fmt={outInfo?.AudioFormat} ch={outInfo?.NumChannels} hz={outInfo?.SampleRate} bps={outInfo?.BitsPerSample} bytes={normalizedWav.Length}");

                // 4) Score request
                var scoreResult = await SendScoreRequest(client, token, postId, normalizedWav);

                if (!scoreResult.isSuccess)
                {
                    return StatusCode((int)(scoreResult.status ?? HttpStatusCode.BadRequest),
                        new
                        {
                            message = "Puanlama Hatası",
                            detail = scoreResult.body,
                            postId,
                            wavInfo = new
                            {
                                input = inInfo,
                                normalized = outInfo
                            }
                        });
                }

                // FluentMe score JSON'u aynen dön
                return Ok(scoreResult.body);
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

            return await SendCreatePost(client, token, attempt2Json);
        }

        private async Task<(bool isSuccess, HttpStatusCode? status, string body, string? postId)>
            SendCreatePost(HttpClient client, string token, string jsonBody)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, FLUENT_POST);

            req.Headers.Clear();
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
                if (resp.StatusCode == HttpStatusCode.Unauthorized) _cachedToken = "";
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
            SendScoreRequest(HttpClient client, string token, string postId, byte[] wavBytes)
        {
            // FluentMe endpoint: /score/{postId}
            var scoreUrl = $"https://thefluent.me/api/swagger/score/{postId}";

            using var multipart = new MultipartFormDataContent();

            // IMPORTANT: field name audio_file olmalı
            var fileContent = new ByteArrayContent(wavBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
            multipart.Add(fileContent, "audio_file", "recording.wav");

            using var req = new HttpRequestMessage(HttpMethod.Post, scoreUrl);
            req.Headers.Clear();
            req.Headers.Add("x-access-token", token);
            req.Headers.Add("x-api-key", HARDCODED_KEY);
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = multipart;

            Console.WriteLine($"[PRON] POST /score/{postId} sending wavBytes={wavBytes.Length}");

            var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[PRON] POST /score status={(int)resp.StatusCode} body={body}");

            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized) _cachedToken = "";
                return (false, resp.StatusCode, body);
            }

            return (true, resp.StatusCode, body);
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
            var loginJson = JsonSerializer.Serialize(new { username = HARDCODED_USER, password = HARDCODED_PASS });

            using var loginReq = new HttpRequestMessage(HttpMethod.Post, FLUENT_LOGIN);
            loginReq.Headers.Clear();
            loginReq.Headers.Add("x-api-key", HARDCODED_KEY);
            loginReq.Headers.Accept.Clear();
            loginReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            loginReq.Content = new StringContent(loginJson, Encoding.UTF8, "application/json");

            var resp = await client.SendAsync(loginReq);
            var body = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[PRON] Login status={(int)resp.StatusCode} body={body}");

            if (!resp.IsSuccessStatusCode)
                throw new Exception("Login Başarısız: " + body);

            // Token parse
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("token", out var t) &&
                t.ValueKind == JsonValueKind.String)
            {
                _cachedToken = t.GetString() ?? "";
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                _cachedToken = doc.RootElement.GetString() ?? "";
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

            // 16kHz
            if (sampleProvider.WaveFormat.SampleRate != 16000)
            {
                sampleProvider = new WdlResamplingSampleProvider(sampleProvider, 16000);
            }

            // PCM16 WAV yaz
            var outFormat = new WaveFormat(16000, 16, 1);
            using var outMs = new MemoryStream();

            using (var writer = new WaveFileWriter(outMs, outFormat))
            {
                var pcm16Provider = new SampleToWaveProvider16(sampleProvider);

                byte[] buffer = new byte[pcm16Provider.WaveFormat.AverageBytesPerSecond]; // ~1sn
                int read;
                while ((read = pcm16Provider.Read(buffer, 0, buffer.Length)) > 0)
                {
                    writer.Write(buffer, 0, read);
                }
            }

            return outMs.ToArray();
        }

        // -------------------------
        // WAV header debug
        // -------------------------
        private class WavInfo
        {
            public int AudioFormat { get; set; }     // 1 = PCM
            public int NumChannels { get; set; }
            public int SampleRate { get; set; }
            public int BitsPerSample { get; set; }
            public int TotalBytes { get; set; }
        }

        private static WavInfo? TryParseWavHeader(byte[] wav)
        {
            try
            {
                if (wav == null || wav.Length < 44) return null;

                // WAV standard header offsets:
                // audioFormat: 20-21 (ushort)
                // numChannels: 22-23 (ushort)
                // sampleRate: 24-27 (int)
                // bitsPerSample: 34-35 (ushort)
                int audioFormat = BitConverter.ToUInt16(wav, 20);
                int numChannels = BitConverter.ToUInt16(wav, 22);
                int sampleRate = BitConverter.ToInt32(wav, 24);
                int bitsPerSample = BitConverter.ToUInt16(wav, 34);

                return new WavInfo
                {
                    AudioFormat = audioFormat,
                    NumChannels = numChannels,
                    SampleRate = sampleRate,
                    BitsPerSample = bitsPerSample,
                    TotalBytes = wav.Length
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
        public string post_id { get; set; } = "";
    }
}
