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
    public class VehicleChangeRequestRepository : GenericRepository<VehicleChangeRequest>, IVehicleChangeRequestRepository
    {
        public VehicleChangeRequestRepository(ParkingDBContext context) : base(context) { }

        public async Task<List<VehicleChangeRequest>>GetBySubscriptionAsync(Guid id)
        {
            return await _context.VehicleChangeRequests
            .Include(x => x.Subscription)
            .Where(x => x.SubscriptionId == id)
            .ToListAsync();
        }
        public async Task<IEnumerable<VehicleChangeRequest>> GetRequestsWithDetailsAsync()
        {
            return await _context.VehicleChangeRequests
                .Include(x => x.Subscription)
                    .ThenInclude(s => s.User)
                .Include(x => x.Subscription)
                    .ThenInclude(s => s.Package)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<VehicleChangeRequest>> GetRequestsByUserIdAsync(Guid userId)
        {
            return await _context.VehicleChangeRequests
                .Include(x => x.Subscription)
                    .ThenInclude(s => s.User)
                .Include(x => x.Subscription)
                    .ThenInclude(s => s.Package)
                .Where(x => x.Subscription != null && x.Subscription.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }
    }
}
