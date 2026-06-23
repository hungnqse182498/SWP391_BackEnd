using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IVehicleChangeRequestRepository : IGenericRepository<VehicleChangeRequest>
    {
        Task<List<VehicleChangeRequest>> GetBySubscriptionAsync(Guid id);
        Task<IEnumerable<VehicleChangeRequest>> GetRequestsWithDetailsAsync();
        Task<IEnumerable<VehicleChangeRequest>> GetRequestsByUserIdAsync(Guid userId);
    }
}

