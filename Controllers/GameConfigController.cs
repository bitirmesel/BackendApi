using Microsoft.AspNetCore.Mvc;
using DktApi.Services;

namespace DktApi.Controllers
{
    [ApiController]
    [Route("api/gameconfig")]
    public class GameConfigController : ControllerBase
    {
        private readonly IGameService _gameService;

        public GameConfigController(IGameService gameService)
        {
            _gameService = gameService;
        }

        /* [HttpGet("{gameId}/{letterId}")]
        public async Task<IActionResult> GetConfig(long gameId, long letterId)
        {
            var config = await _gameService.GetGameConfigAsync(gameId, letterId);

            if (config == null)
                return NotFound("Veri bulunamadı.");

            return Ok(config);
        } */

        [HttpGet("{gameId}/{letterId}")]
public async Task<IActionResult> GetConfig(long gameId, long letterId)
{
    // --- PGADMIN YOKKEN GEÇİCİ TEST KODU BAŞLANGICI ---
    // Eğer Hafıza Oyunu (GameId = 1) isteniyorsa, veritabanına bakmadan bu JSON'ı yolla!
    if (gameId == 1) 
    {
        var mockJson = @"{
            ""config_id"": ""K_Kelime_Memory"",
            ""base_url"": ""https://res.cloudinary.com/dd6zijhry/image/upload/v1764791490/graduationProject/letters/K/kelime/memoryGame/"",
            ""items"": [
                { ""key"": ""kedi"", ""file"": ""kedi.png"" },
                { ""key"": ""kopek"", ""file"": ""kopek.png"" },
                { ""key"": ""kus"", ""file"": ""kus.png"" },
                { ""key"": ""kurbaga"", ""file"": ""kurbaga.png"" },
                { ""key"": ""kartal"", ""file"": ""kartal.png"" },
                { ""key"": ""koyun"", ""file"": ""koyun.png"" }
            ]
        }";
        
        // JSON metnini sanki veritabanından gelmiş gibi Flutter'a gönderiyoruz
        return Content(mockJson, "application/json");
    }
    // --- GEÇİCİ TEST KODU BİTİŞİ ---

    // Orijinal Kod (İleride pgAdmin'e girince üstteki kısmı sileceksin, burası çalışacak)
    var config = await _gameService.GetGameConfigAsync(gameId, letterId);

    if (config == null)
        return NotFound("Veri bulunamadı.");

    return Ok(config);
}
    }
}