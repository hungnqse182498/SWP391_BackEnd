using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace DAL.Implements
{
    public class VehicleTypeRepository : GenericRepository<VehicleType>, IVehicleTypeRepository
    {
        private readonly ParkingDBContext _context;

        public VehicleTypeRepository(ParkingDBContext context) : base(context)
        {
            _context = context;
        }

        public async Task<VehicleType> FindByNameAsync(string typeName)
        {
            return await _context.VehicleTypes.FirstOrDefaultAsync(v => v.TypeName == typeName);
        }
    }
}