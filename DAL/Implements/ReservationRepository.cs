using DAL.Interfaces;
using DAL.Models;

namespace DAL.Implements
{
    public class ReservationRepository : GenericRepository<Reservation>, IReservationRepository
    {
        public ReservationRepository(ParkingDBContext context) : base(context)
        {
        }
    }
}
