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
    public class SubscriptionRenewalRepository : GenericRepository<SubscriptionRenewal>, ISubscriptionRenewalRepository
    {
        public SubscriptionRenewalRepository(ParkingDBContext context) : base(context) { }
        public async Task<List<SubscriptionRenewal>> GetAllWithSubscriptionAsync()
        {
            return await _context.SubscriptionRenewals
                .Include(x => x.Subscription)
                .OrderByDescending(x => x.RenewalDate)
                .ToListAsync();
        }

        public async Task<SubscriptionRenewal> GetByIdWithSubscriptionAsync(Guid renewalId)
        {
            return await _context.SubscriptionRenewals
                .Include(x => x.Subscription)
                .FirstOrDefaultAsync(x => x.RenewalId == renewalId);
        }

        public async Task<List<SubscriptionRenewal>> GetBySubscriptionIdAsync(Guid subscriptionId)
        {
            return await _context.SubscriptionRenewals
                .Include(x => x.Subscription)
                .Where(x => x.SubscriptionId == subscriptionId)
                .OrderByDescending(x => x.RenewalDate)
                .ToListAsync();
        }
    }
}
