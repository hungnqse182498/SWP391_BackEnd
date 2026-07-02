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
    public class ParkingSlotRepository : GenericRepository<ParkingSlot>, IParkingSlotRepository
    {
        public ParkingSlotRepository(ParkingDBContext context) : base(context)
        {
        }

        public async Task<List<ParkingSlot>> GetAllWithDetailsAsync()
        {
            return await _context.ParkingSlots
                .Include(s => s.Floor)
                .Include(s => s.VehicleType)
                .OrderBy(s => s.Floor.FloorName)
                .ThenBy(s => s.SlotCode)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<ParkingSlot?> GetDetailWithFloorAndTypeAsync(Guid id)
        {
            return await _context.ParkingSlots
                .Include(s => s.Floor)
                .Include(s => s.VehicleType)
                .FirstOrDefaultAsync(s => s.SlotId == id);
        }

        public async Task<ParkingSlot?> GetFirstAvailableByVehicleTypeAsync(Guid vehicleTypeId)
        {
            return await _context.ParkingSlots
                .Where(s => s.VehicleTypeId == vehicleTypeId &&
                            s.Status == ParkingSlotStatus.Available.ToString())
                .OrderBy(s => s.SlotCode)
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetSlotsCountByFloorAsync(Guid floorId)
        {
            return await _context.ParkingSlots
                .CountAsync(s => s.FloorId == floorId);
        }

        public async Task<bool> IsSlotCodeDuplicateAsync(string slotCode, Guid? currentSlotId)
        {
            var code = slotCode.Trim().ToLower();
            return await _context.ParkingSlots
                .AnyAsync(s => s.SlotCode.ToLower() == code &&
                               (!currentSlotId.HasValue || s.SlotId != currentSlotId.Value));
        }
    }
}
