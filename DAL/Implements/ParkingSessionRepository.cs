using DAL.Interfaces;
using DAL.Models;
using Common.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace DAL.Implements
{
    public class ParkingSessionRepository : GenericRepository<ParkingSession>, IParkingSessionRepository
    { 
        public ParkingSessionRepository(ParkingDBContext context) : base(context) { }

        public async Task<List<ParkingSession>> GetAllSessionsWithDetailsAsync()
        {
            return await _context.ParkingSessions
                .Include(s => s.DriverUser)
                .Include(s => s.VehicleType)
                .Include(s => s.EntryGate)
                .Include(s => s.ExitGate)
                .Include(s => s.AssignedSlot)
                .Include(s => s.ActualSlot)
                .OrderByDescending(s => s.EntryTime)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<ParkingSession>> GetSessionsByDriverUserIdWithDetailsAsync(Guid userId)
        {
            return await _context.ParkingSessions
                .Include(s => s.DriverUser)
                .Include(s => s.VehicleType)
                .Include(s => s.EntryGate)
                .Include(s => s.ExitGate)
                .Include(s => s.AssignedSlot)
                .Include(s => s.ActualSlot)
                .Where(s => s.DriverUserId == userId)
                .OrderByDescending(s => s.EntryTime)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<ParkingSession?> GetSessionDetailAsync(Guid id)
        {
            return await _context.ParkingSessions
                .Include(s => s.DriverUser)
                .Include(s => s.VehicleType)
                .Include(s => s.EntryGate)
                .Include(s => s.ExitGate)
                .Include(s => s.AssignedSlot)
                .Include(s => s.ActualSlot)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SessionId == id);
        }

        public async Task<ParkingSession?> GetActiveSessionWithDetailsAsync(Guid? sessionId, string? licensePlate)
        {
            var query = _context.ParkingSessions
                .Include(s => s.DriverUser)
                .Include(s => s.VehicleType)
                .Include(s => s.EntryGate)
                .Include(s => s.ExitGate)
                .Include(s => s.AssignedSlot)
                .Include(s => s.ActualSlot)
                .Where(s => s.Status == SessionStatus.Active.ToString());

            if (sessionId.HasValue)
            {
                query = query.Where(s => s.SessionId == sessionId.Value);
            }

            if (!string.IsNullOrWhiteSpace(licensePlate))
            {
                var normalizedPlate = licensePlate.Trim().ToUpper();
                query = query.Where(s => s.LicensePlateIn.ToUpper() == normalizedPlate);
            }

            return await query.FirstOrDefaultAsync();
        }

        public async Task<bool> HasActiveSessionByLicensePlateAsync(string licensePlate, Guid? excludeSessionId = null)
        {
            if (string.IsNullOrWhiteSpace(licensePlate)) return false;

            var normalizedPlate = licensePlate.Trim().ToUpper();
            return await _context.ParkingSessions.AnyAsync(s =>
                s.Status == SessionStatus.Active.ToString() &&
                s.LicensePlateIn.ToUpper() == normalizedPlate &&
                (!excludeSessionId.HasValue || s.SessionId != excludeSessionId.Value));
        }
    }
}
