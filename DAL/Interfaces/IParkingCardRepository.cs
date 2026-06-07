using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IParkingCardRepository : IGenericRepository<ParkingCard>
    {
        Task<ParkingCard?> GetActiveCardAsync(string cardCode);
        Task<ParkingCard> FindByCodeAsync(string cardCode);
    }
}
