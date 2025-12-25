using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using UnityBackend.Models; // Modellerin olduğu namespace
using UnityEngine;

namespace UnityBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PronunciationController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        
        // Render Environment Variables'dan okuyacaklarımız
        private readonly string _apiUsername;
        private readonly string _apiPassword;
        private readonly string _apiKey; // 2025... ile başlayan anahtarın

        // Token'ı hafızada tutalım ki her seferinde tekrar login olmasın
        private static string _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        public PronunciationController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _apiUsername = configuration["FLUENT_USER"];
            _apiPassword = configuration["FLUENT_PASS"];
            // Eğer API Key de lazımsa environment'a ekleyip buradan çekebilirsin
            // Şimdilik login için User/Pass yetiyor gibi görünüyor ama Swagger resminde key de var.
             _apiKey = configuration["THE_FLUENT_ME_API_KEY"]; 
        }

        [HttpPost("check")]
        public async Task<IActionResult> CheckPronunciation([FromForm] IFormFile audioFile, [FromForm] string text)
        {
            if (audioFile == null || audioFile.Length == 0) 
                return BadRequest("Ses dosyası yok.");

            var client = _httpClientFactory.CreateClient();

            try
            {
                // 1. ADIM: OTOMATİK LOGIN OL VE TOKEN AL
                string token = await GetValidToken(client);

                // Token'ı başlığa ekle (Authorization: Bearer ...)
                // VEYA resimdeki gibi x-access-token olabilir, Swagger sonucuna göre token'ı direkt header'a gömüyoruz.
                client.DefaultRequestHeaders.Clear();
                // Swagger çıktısında genelde JWT tokenlar "Bearer " ile kullanılır ama
                // senin önceki denemelerinde x-access-token kullanılmıştı.
                // Resimde token cevabı saf string dönmüş.
                // En güvenlisi token'ı "x-access-token" olarak eklemek.
                client.DefaultRequestHeaders.Add("x-access-token", token);

                // 2. ADIM: POST OLUŞTUR (ID: 76 Türkçe - Resminden teyitli)
                var postContent = new
                {
                    post_title = "Unity Kaydı",
                    post_content = text,
                    post_language_id = "76" // Türkçe Kadın Sesi (Resimden 76 olduğu kesinleşti)
                };

                var postResponse = await client.PostAsJsonAsync("https://thefluent.me/api/swagger/post", postContent);
                
                if (!postResponse.IsSuccessStatusCode)
                {
                    // Token süresi dolmuş olabilir, cache'i temizle
                    if(postResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        _cachedToken = null; 
                    
                    return BadRequest("Post Oluşturma Hatası: " + await postResponse.Content.ReadAsStringAsync());
                }

                // Post ID'yi al
                var postRespStr = await postResponse.Content.ReadAsStringAsync();
                var postObj = JsonSerializer.Deserialize<CreatePostResponse>(postRespStr);
                string postId = postObj.post_id;

                // 3. ADIM: SESİ GÖNDER
                using (var content = new MultipartFormDataContent())
                {
                    using (var stream = audioFile.OpenReadStream())
                    {
                        var fileContent = new StreamContent(stream);
                        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                        content.Add(fileContent, "audio_file", "recording.wav");

                        var scoreResponse = await client.PostAsync($"https://thefluent.me/api/swagger/score/{postId}", content);

                        if (scoreResponse.IsSuccessStatusCode)
                            return Ok(await scoreResponse.Content.ReadAsStringAsync());
                        else
                            return StatusCode((int)scoreResponse.StatusCode, "Puanlama Hatası: " + await scoreResponse.Content.ReadAsStringAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Sunucu Hatası: " + ex.Message);
            }
        }

        // --- LOGIN YARDIMCISI ---
        private async Task<string> GetValidToken(HttpClient client)
        {
            // Eğer elimizde geçerli bir token varsa tekrar sorma
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry)
                return _cachedToken;

            // Swagger resmindeki gibi Basic Auth başlığı oluşturuyoruz
            var authBytes = Encoding.ASCII.GetBytes($"{_apiUsername}:{_apiPassword}");
            var authString = Convert.ToBase64String(authBytes);
            
            var request = new HttpRequestMessage(HttpMethod.Get, "https://thefluent.me/api/swagger/login");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
            
            // Eğer Swagger resmindeki gibi API Key de istiyorsa ekleyelim:
            if(!string.IsNullOrEmpty(_apiKey))
                request.Headers.Add("x-api-key", _apiKey);

            var response = await client.SendAsync(request);
            var respStr = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Login Başarısız! Hata: {respStr}");

            // JSON Cevabını Parse Et: { "token": "eyJ..." }
            using (JsonDocument doc = JsonDocument.Parse(respStr))
            {
                if (doc.RootElement.TryGetProperty("token", out var tokenProp))
                {
                    _cachedToken = tokenProp.GetString();
                    // Token'ı 50 dakika geçerli say (Genelde 1 saattir)
                    _tokenExpiry = DateTime.Now.AddMinutes(50);
                    return _cachedToken;
                }
                else
                {
                    throw new Exception("Token bulunamadı: " + respStr);
                }
            }
        }
    }
}