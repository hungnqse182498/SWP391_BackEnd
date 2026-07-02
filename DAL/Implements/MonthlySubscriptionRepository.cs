using DAL.Interfaces;
using Common.Enums;
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

        public async Task<IEnumerable<MonthlySubscription>> GetAllWithDetailsAsync()
        {
            return await _context.MonthlySubscriptions
                .Include(s => s.User)
                .Include(s => s.VehicleType)
                .Include(s => s.Package)
                .Include(s => s.FixedSlot)
                .ToListAsync();
        }

        public async Task<MonthlySubscription?> GetDetailAsync(Guid id)
        {
            return await _context.MonthlySubscriptions
            .Include(x => x.User)
            .Include(x => x.Package)
            .Include(x => x.VehicleType)
            .Include(x => x.FixedSlot)
            .FirstOrDefaultAsync(x => x.SubscriptionId == id);

        }

        public async Task<MonthlySubscription?> GetActivationDetailAsync(Guid id)
        {
            return await _context.MonthlySubscriptions
                .Include(s => s.Package)
                .Include(s => s.User)
                    .ThenInclude(u => u.Role)
                .FirstOrDefaultAsync(s => s.SubscriptionId == id);
        }

        public async Task<bool> HasUsablePlateAsync(string plate, Guid? ignoredSubscriptionId = null)
        {
            return await _context.MonthlySubscriptions
                .AnyAsync(s =>
                    s.LicensePlate == plate &&
                    s.Status != MonthlySubscriptionStatus.Cancelled.ToString() &&
                    (!ignoredSubscriptionId.HasValue || s.SubscriptionId != ignoredSubscriptionId.Value) &&
                    (s.Status == MonthlySubscriptionStatus.PendingPayment.ToString() || s.EndDate >= DateTime.UtcNow));
        }
    }
}
