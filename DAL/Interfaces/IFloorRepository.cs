using DAL.Models;
using System;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IFloorRepository : IGenericRepository<Floor>
    {
        Task<Floor> FindByNameAsync(string floorName);
        Task<Floor> GetByIdWithVehicleTypeAsync(Guid id);
    }
}
