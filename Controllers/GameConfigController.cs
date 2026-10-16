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

        /// <summary>
        /// Oyun + harf için asset_sets.asset_json'ı olduğu gibi döner (snake_case: base_url, items…).
        /// Fonem kelime oyunları (4-7) kullanır. Kayıt yoksa 404.
        /// </summary>
        [HttpGet("{gameId}/{letterId}")]
        public async Task<IActionResult> GetConfig(long gameId, long letterId)
        {
            var configJson = await _gameService.GetGameConfigAsync(gameId, letterId);

            if (string.IsNullOrWhiteSpace(configJson))
                return NotFound("Veri bulunamadı.");

            // asset_json zaten JSON metni; text/plain yerine doğru içerik tipiyle dönülür
            return Content(configJson, "application/json");
        }
    }
}