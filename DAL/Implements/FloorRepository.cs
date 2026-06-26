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

        public async Task<int> GetTotalCapacityByVehicleTypeAsync(Guid vehicleTypeId, bool isResident)
        {
            return await _context.Floors
                .Where(f => f.DedicatedVehicleTypeId == vehicleTypeId && f.IsResident == isResident)
                .SumAsync(f => f.TotalCapacity);
        }

        public async Task<Floor> FindByNameAsync(string floorName)
        {
            return await _context.Floors.FirstOrDefaultAsync(f => f.FloorName == floorName);
        }

        public async Task<Floor> GetByIdWithVehicleTypeAsync(Guid id)
        {
            return await _context.Floors.Include(f => f.DedicatedVehicleType).FirstOrDefaultAsync(f => f.FloorId == id);
        }

        public async Task<IEnumerable<Floor>> GetAllWithVehicleTypeAsync()
        {
            return await _context.Floors
                .Include(f => f.DedicatedVehicleType)
                .ToListAsync();
        }

        public async Task<bool> IsNameDuplicateAsync(string floorName, Guid? excludeFloorId = null)
        {
            return await _context.Floors
                .AnyAsync(f => f.FloorName == floorName && (!excludeFloorId.HasValue || f.FloorId != excludeFloorId.Value));
        }
    }
}
