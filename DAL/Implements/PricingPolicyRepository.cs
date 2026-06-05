using DAL.Interfaces;
using DAL.Models;

namespace DAL.Implements
{
    public class PricingPolicyRepository : GenericRepository<PricingPolicy>, IPricingPolicyRepository
    {
        public PricingPolicyRepository(ParkingDBContext context) : base(context)
        {
        }
    }
}
