using DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.UnitOfWorks
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository UserRepo { get; }
        ITokenRepository TokenRepo { get; }
        IVehicleTypeRepository VehicleTypeRepo { get; }
        IFloorRepository FloorRepo { get; }
        Task<int> SaveAsync();  
        Task<bool> SaveChangeAsync();
    }
}
