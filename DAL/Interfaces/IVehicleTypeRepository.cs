using DAL.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IVehicleTypeRepository : IGenericRepository<VehicleType>
    {
        Task<VehicleType> FindByNameAsync(string typeName);
    }
}