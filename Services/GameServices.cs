using Newtonsoft.Json;
using DktApi.Dtos;
using DktApi.Repositories;
using DktApi.DTOs.Game;
using DktApi.Dtos.Game;

namespace DktApi.Services
{
    public interface IGameService
    {
        Task<GameAssetConfigDto?> GetGameConfigAsync(long gameId, long letterId);
    }

    public class GameService : IGameService
    {
        private readonly IGameRepository _gameRepository;

        public GameService(IGameRepository gameRepository)
        {
            _gameRepository = gameRepository;
        }

        public async Task<GameAssetConfigDto?> GetGameConfigAsync(long gameId, long letterId)
        {
            var assetSet = await _gameRepository.GetAssetSetAsync(gameId, letterId);

            if (assetSet == null || string.IsNullOrEmpty(assetSet.AssetJson))
                return null;

            try
            {
                return JsonConvert.DeserializeObject<GameAssetConfig>(assetSet.AssetJson);
            }
            catch
            {
                return null;
            }
        }
    }
}