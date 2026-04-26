using DktApi.Repositories;

namespace DktApi.Services
{
    public interface IGameService
    {
        Task<string?> GetGameConfigAsync(long gameId, long letterId);
    }

    public class GameService : IGameService
    {
        private readonly IGameRepository _gameRepository;

        public GameService(IGameRepository gameRepository)
        {
            _gameRepository = gameRepository;
        }

        public async Task<string?> GetGameConfigAsync(long gameId, long letterId)
        {
            var assetSet = await _gameRepository.GetAssetSetAsync(gameId, letterId);

            if (assetSet == null || string.IsNullOrWhiteSpace(assetSet.AssetJson))
                return null;

            return assetSet.AssetJson;
        }
    }
}