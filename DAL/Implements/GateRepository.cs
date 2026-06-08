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
    public class GateRepository : GenericRepository<Gate>, IGateRepository
    { 
        public GateRepository(ParkingDBContext context) : base(context) { }

        public async Task<IEnumerable<Gate>> GetAllWithFloorAsync()
        {
            return await _context.Gates
                .Include(g => g.Floor)
                .OrderBy(g => g.Floor.FloorName)
                .ThenBy(g => g.GateType)
                .ToListAsync();
        }

        public async Task<Gate?> GetByIdWithFloorAsync(Guid id)
        {
            return await _context.Gates
                .Include(g => g.Floor)
                .FirstOrDefaultAsync(g => g.GateId == id);
        }

        public async Task<bool> IsNameDuplicateAsync(string gateName, Guid? excludeGateId = null)
        {
            return await _context.Gates
                .AnyAsync(g => g.GateName == gateName && (!excludeGateId.HasValue || g.GateId != excludeGateId.Value));
        }
    }


}
