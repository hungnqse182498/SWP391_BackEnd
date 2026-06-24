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
                .FirstOrDefaultAsync(s => s.SessionId == id);
        }
    }
}
