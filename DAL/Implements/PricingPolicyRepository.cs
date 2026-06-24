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
    public class PricingPolicyRepository : GenericRepository<PricingPolicy>, IPricingPolicyRepository
    {
        public PricingPolicyRepository(ParkingDBContext context) : base(context) { }
        public async Task<PricingPolicy?> GetActivePolicyAsync(Guid vehicleTypeId)
        {
            return await _context.PricingPolicies.FirstOrDefaultAsync(p => p.VehicleTypeId == vehicleTypeId && p.Status == "Active");
        }
        public async Task<IEnumerable<PricingPolicy>> GetAllWithVehicleTypeAsync()
        {
            return await _context.PricingPolicies
                .Include(p => p.VehicleType)
                .OrderByDescending(p => p.EffectiveDate)
                .ToListAsync();
        }

        public async Task<PricingPolicy?> GetByIdWithVehicleTypeAsync(Guid id)
        {
            return await _context.PricingPolicies
                .Include(p => p.VehicleType)
                .FirstOrDefaultAsync(p => p.PolicyId == id);
        }
    }
}
