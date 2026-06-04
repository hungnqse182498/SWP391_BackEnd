using DAL.Interfaces;
using DAL.Models;

namespace DAL.Implements
{
    public class GateRepository : GenericRepository<Gate>, IGateRepository
    {
        public GateRepository(ParkingDBContext context) : base(context)
        {
        }
    }
}
