using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace DAL.Implements
{
    public class FloorRepository : GenericRepository<Floor>, IFloorRepository
    {
        private readonly ParkingDBContext _context;

        public FloorRepository(ParkingDBContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Floor> FindByNameAsync(string floorName)
        {
            return await _context.Floors.FirstOrDefaultAsync(f => f.FloorName == floorName);
        }

        public async Task<Floor> GetByIdWithVehicleTypeAsync(Guid id)
        {
            return await _context.Floors.Include(f => f.DedicatedVehicleType).FirstOrDefaultAsync(f => f.FloorId == id);
        }
    }
}
