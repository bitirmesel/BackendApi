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

        [HttpGet("{gameId}/{letterId}")]
        public async Task<IActionResult> GetConfig(long gameId, long letterId)
        {
            var config = await _gameService.GetGameConfigAsync(gameId, letterId);

            if (config == null)
                return NotFound("Veri bulunamadı.");

            return Ok(config);
        }
    }
}