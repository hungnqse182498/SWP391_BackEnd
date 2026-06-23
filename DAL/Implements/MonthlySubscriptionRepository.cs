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
        public async Task<List<MonthlySubscription>> GetByUserAsync(Guid userId)
        {
            return await _context.MonthlySubscriptions
            .Include(x => x.User)
            .Include(x => x.Package)
            .Include(x => x.VehicleType)
            .Include(x => x.FixedSlot)
            .Where(x => x.UserId == userId)
            .ToListAsync();
        }
        public async Task<MonthlySubscription?> GetDetailAsync(Guid id)
        {
            return await _context.MonthlySubscriptions
            .Include(x => x.User)
            .Include(x => x.Package)
            .Include(x => x.VehicleType)
            .Include(x => x.FixedSlot)
            .FirstOrDefaultAsync(x =>
            x.SubscriptionId == id);

        }

        public async Task<bool> HasUsablePlateAsync(string plate, Guid? ignoredSubscriptionId = null)
        {
            return await _context.MonthlySubscriptions
                .AnyAsync(s =>
                    s.LicensePlate == plate &&
                    s.Status != "Cancelled" &&
                    (!ignoredSubscriptionId.HasValue || s.SubscriptionId != ignoredSubscriptionId.Value) &&
                    (s.Status == "PendingPayment" || s.EndDate >= DateTime.UtcNow));
        }
    }
}
