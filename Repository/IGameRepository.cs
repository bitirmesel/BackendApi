using Microsoft.EntityFrameworkCore;
using DktApi.Models.Db; // AssetSet burada
// using SeninProjen.Data; -> DbContext'in olduğu namespace'i ekle

namespace DktApi.Repositories
{
    public interface IGameRepository
    {
        Task<AssetSet?> GetAssetSetAsync(long gameId, long letterId);
    }

    public class GameRepository : IGameRepository
    {
        // AppDbContext yerine senin Context adın neyse onu yaz
        private readonly AppDbContext _context; 

        public GameRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AssetSet?> GetAssetSetAsync(long gameId, long letterId)
        {
            // Veritabanından veriyi çekiyoruz
            return await _context.Set<AssetSet>() 
                .FirstOrDefaultAsync(x => x.GameId == gameId && x.LetterId == letterId);
        }
    }
}