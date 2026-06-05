using DAL.Models;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IParkingCardRepository : IGenericRepository<ParkingCard>
    {
        Task<ParkingCard> FindByCodeAsync(string cardCode);
    }
}
