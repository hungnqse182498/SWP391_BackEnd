using DAL.Interfaces;
using DAL.Models;

namespace DAL.Implements
{
    public class ParkingSessionRepository : GenericRepository<ParkingSession>, IParkingSessionRepository
    {
        public ParkingSessionRepository(ParkingDBContext context) : base(context)
        {
        }
    }
}
