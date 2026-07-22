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
    public class ReservationRepository : GenericRepository<Reservation>, IReservationRepository
    {
        public ReservationRepository(ParkingDBContext context) : base(context)
        {
        }

        public async Task<List<Reservation>> GetByUserIdWithPaymentsAsync(Guid userId)
        {
            return await _context.Reservations
                .Include(r => r.Payments)
                .Include(r => r.User)
                .Include(r => r.VehicleType)
                .Where(x => x.UserId == userId)
                .ToListAsync();
        }

        public async Task<Reservation?> GetDetailWithRelationsAsync(Guid reservationId)
        {
            return await _context.Reservations
                .Include(r => r.User)
                .Include(r => r.VehicleType)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId);
        }

        public async Task<List<Reservation>> GetByAdminFiltersAsync(string? status, DateTime? date)
        {
            IQueryable<Reservation> query = _context.Reservations
                .Include(r => r.Payments)
                .Include(r => r.User)
                .Include(r => r.VehicleType);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(x => x.Status == status);
            }

            if (date.HasValue)
            {
                query = query.Where(x => x.ExpectedEntryTime.Date == date.Value.Date);
            }

            query = query.OrderByDescending(x => x.CreatedAt);

            return await query.ToListAsync();
        }

        public async Task<int> CountActiveReservationsAsync(Guid vehicleTypeId, string statusConfirmed, string statusModified, Guid excludeReservationId)
        {
            return await _context.Reservations
                .CountAsync(r => r.VehicleTypeId == vehicleTypeId
                              && (r.Status == statusConfirmed || r.Status == statusModified) 
                              && r.ReservationId != excludeReservationId); 
        }

        public async Task<List<Reservation>> GetOverdueAsync(DateTime overdueBeforeUtc, params string[] statuses)
        {
            return await _context.Reservations
                .Where(r => r.ExpectedEntryTime <= overdueBeforeUtc && statuses.Contains(r.Status))
                .ToListAsync();
        }
    }
}
