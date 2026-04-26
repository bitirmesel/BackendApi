using Newtonsoft.Json.Linq;
using DktApi.Repositories;

namespace DktApi.Services
{
    public interface IGameService
    {
        // Artık raw JSON dönecek
        Task<object?> GetGameConfigAsync(long gameId, long letterId);
    }

    public class GameService : IGameService
    {
        private readonly IGameRepository _gameRepository;

        public GameService(IGameRepository gameRepository)
        {
            _gameRepository = gameRepository;
        }

        public async Task<object?> GetGameConfigAsync(long gameId, long letterId)
        {
            var assetSet = await _gameRepository.GetAssetSetAsync(gameId, letterId);

            if (assetSet == null || string.IsNullOrWhiteSpace(assetSet.AssetJson))
                return null;

            try
            {
                // 🔥 KRİTİK: JSON’u olduğu gibi döndür
                return JObject.Parse(assetSet.AssetJson);
            }
            catch
            {
                return null;
            }
        }
    }
}