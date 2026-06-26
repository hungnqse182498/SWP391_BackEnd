using DAL.Models;
using System;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IFloorRepository : IGenericRepository<Floor>
    {
        Task<int> GetTotalCapacityByVehicleTypeAsync(Guid vehicleTypeId, bool isResident);
        Task<Floor> FindByNameAsync(string floorName);
        Task<Floor> GetByIdWithVehicleTypeAsync(Guid id);
        Task<IEnumerable<Floor>> GetAllWithVehicleTypeAsync();
        Task<bool> IsNameDuplicateAsync(string floorName, Guid? excludeFloorId = null);
    }
}
