using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Implements
{
    public class MonthlySubscriptionRepository : GenericRepository<MonthlySubscription>, IMonthlySubscriptionRepository
    {
        public MonthlySubscriptionRepository(ParkingDBContext context) : base(context) { }
        public async Task<MonthlySubscription?> GetActiveSubscriptionAsync(string licensePlate)
        {
            return await _context.MonthlySubscriptions
                .FirstOrDefaultAsync(m => m.LicensePlate == licensePlate
                                       && m.Status == "Active"
                                       && m.EndDate >= DateTime.Now);
        }
    }
}
