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
    public class SubscriptionPackageRepository : GenericRepository<SubscriptionPackage>, ISubscriptionPackageRepository
    {
        public SubscriptionPackageRepository(ParkingDBContext context) : base(context) { }

        public async Task<List<SubscriptionPackage>> GetAllWithVehicleTypeAsync()
        {
            return await _context.SubscriptionPackages
                .Include(p => p.VehicleType)
                .ToListAsync();
        }

        public async Task<SubscriptionPackage?> GetByIdWithVehicleTypeAsync(Guid id)
        {
            return await _context.SubscriptionPackages
                .Include(p => p.VehicleType)
                .FirstOrDefaultAsync(p => p.PackageId == id);
        }
    }
}
