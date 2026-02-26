using Newtonsoft.Json;
using DktApi.Repositories;
// Hatalı olan using DktApi.Dtos.Game satırını silebilirsin veya projenin modeline yönlendirebilirsin
using GraduationProject.Models; 

namespace DktApi.Services
{
    public interface IGameService
    {
        // Dönüş tipini GameAssetConfig olarak güncelledik
        Task<GameAssetConfig?> GetGameConfigAsync(long gameId, long letterId);
    }

    public class GameService : IGameService
    {
        private readonly IGameRepository _gameRepository;

        public GameService(IGameRepository gameRepository)
        {
            _gameRepository = gameRepository;
        }

        public async Task<GameAssetConfig?> GetGameConfigAsync(long gameId, long letterId)
        {
            var assetSet = await _gameRepository.GetAssetSetAsync(gameId, letterId);

            if (assetSet == null || string.IsNullOrEmpty(assetSet.AssetJson))
                return null;

            try
            {
                // Artık mevcut olan GameAssetConfig sınıfına deserialize ediyoruz
                return JsonConvert.DeserializeObject<GameAssetConfig>(assetSet.AssetJson);
            }
            catch
            {
                return null;
            }
        }
    }
}