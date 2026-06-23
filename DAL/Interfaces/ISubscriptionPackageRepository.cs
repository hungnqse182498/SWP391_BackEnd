using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface ISubscriptionPackageRepository : IGenericRepository<SubscriptionPackage>
    {
        Task<List<SubscriptionPackage>> GetAllWithVehicleTypeAsync();
        Task<SubscriptionPackage?> GetByIdWithVehicleTypeAsync(Guid id);
    }
}
