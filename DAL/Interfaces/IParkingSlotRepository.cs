using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IParkingSlotRepository : IGenericRepository<ParkingSlot>
    {
        Task<int> GetWalkInCapacityAsync(Guid vehicleTypeId);
        Task<ParkingSlot?> GetAvailableWalkInSlotAsync(Guid vehicleTypeId);
    }
}
