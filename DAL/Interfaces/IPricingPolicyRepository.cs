using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IPricingPolicyRepository : IGenericRepository<PricingPolicy>
    {
        Task<PricingPolicy?> GetActivePolicyAsync(Guid vehicleTypeId);
        Task<IEnumerable<PricingPolicy>> GetAllWithVehicleTypeAsync();
        Task<PricingPolicy?> GetByIdWithVehicleTypeAsync(Guid id);
    }
}
