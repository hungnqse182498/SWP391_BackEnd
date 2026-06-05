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
    public class ParkingSlotRepository : GenericRepository<ParkingSlot>, IParkingSlotRepository
    {
        public ParkingSlotRepository(ParkingDBContext context) : base(context)
        {
        }

        public async Task<int> GetWalkInCapacityAsync(Guid vehicleTypeId)
        {
            return await _context.ParkingSlots
                .CountAsync(s => s.Floor.DedicatedVehicleTypeId == vehicleTypeId
                              && s.Floor.FloorName.Contains("Vãng lai"));
        }

        public async Task<ParkingSlot?> GetAvailableWalkInSlotAsync(Guid vehicleTypeId)
        {
            return await _context.ParkingSlots
                .FirstOrDefaultAsync(s => s.Floor.DedicatedVehicleTypeId == vehicleTypeId
                                      && s.Status == "Available"
                                      && s.Floor.FloorName.Contains("Vãng lai"));
        }
    }
}
