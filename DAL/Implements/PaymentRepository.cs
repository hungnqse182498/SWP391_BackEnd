using DAL.Interfaces;
using DAL.Models;

namespace DAL.Implements
{
    public class PaymentRepository : GenericRepository<Payment>, IPaymentRepository
    {
        public PaymentRepository(ParkingDBContext context) : base(context)
        {
        }
    }
}
