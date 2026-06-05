using DAL.Interfaces;
using DAL.Models;

namespace DAL.Implements
{
    public class MonthlySubscriptionRepository : GenericRepository<MonthlySubscription>, IMonthlySubscriptionRepository
    {
        public MonthlySubscriptionRepository(ParkingDBContext context) : base(context)
        {
        }
    }
}
