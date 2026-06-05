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
        public async Task<ParkingSession?> GetActiveSessionByCardIdAsync(Guid cardId)
        {
            return await _context.ParkingSessions
                .FirstOrDefaultAsync(s => s.CardId == cardId && s.Status == "Active");
        }

        public async Task<ParkingSession?> GetActiveSessionByPlateAsync(string licensePlate)
        {
            return await _context.ParkingSessions
                .FirstOrDefaultAsync(s => s.LicensePlateIn == licensePlate
                                       && s.Status == "Active"
                                       && s.CardId == null);
        }
    }
}
