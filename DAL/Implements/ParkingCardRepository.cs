using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace DAL.Implements
{
    public class ParkingCardRepository : GenericRepository<ParkingCard>, IParkingCardRepository
    {
        private readonly ParkingDBContext _context;

        public ParkingCardRepository(ParkingDBContext context) : base(context)
        {
            _context = context;
        }

        public async Task<ParkingCard> FindByCodeAsync(string cardCode)
        {
            var normalizedCode = cardCode.Trim().ToLower();
            return await _context.ParkingCards.FirstOrDefaultAsync(c => c.CardCode.ToLower() == normalizedCode);
        }
    }
}
