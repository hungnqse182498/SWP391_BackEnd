using DAL.Interfaces;
using DAL.Models;

namespace DAL.Implements
{
    public class ParkingSlotRepository : GenericRepository<ParkingSlot>, IParkingSlotRepository
    {
        public ParkingSlotRepository(ParkingDBContext context) : base(context)
        {
        }
    }
}
