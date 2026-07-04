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
        Task<List<ParkingSlot>> GetAllWithDetailsAsync();
        Task<ParkingSlot?> GetDetailWithFloorAndTypeAsync(Guid id);
        Task<ParkingSlot?> GetFirstAvailableByVehicleTypeAsync(Guid vehicleTypeId);
        Task<List<ParkingSlot>> GetAvailableByVehicleTypeAndResidentFlagAsync(Guid vehicleTypeId, bool isResident);
        Task<int> GetSlotsCountByFloorAsync(Guid floorId);
        Task<bool> IsSlotCodeDuplicateAsync(string slotCode, Guid? currentSlotId);
    }
}
