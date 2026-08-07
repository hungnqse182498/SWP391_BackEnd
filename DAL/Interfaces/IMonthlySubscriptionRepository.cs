using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IMonthlySubscriptionRepository : IGenericRepository<MonthlySubscription>
    {
        Task<List<MonthlySubscription>> GetByUserAsync(Guid userId);
        Task<MonthlySubscription?> GetDetailAsync(Guid id);
        Task<MonthlySubscription?> GetActivationDetailAsync(Guid id);
        Task<MonthlySubscription?> GetActiveByPlateAndVehicleTypeAsync(string licensePlate, Guid vehicleTypeId, DateTime now);
        Task<bool> HasUsablePlateAsync(string plate, Guid? ignoredSubscriptionId = null);
        Task<IEnumerable<MonthlySubscription>> GetAllWithDetailsAsync();
        Task<bool> ExistsAsync(Guid subscriptionId);
        Task<bool> HasSubscriptionsByPackageIdAsync(Guid packageId);
        Task<List<MonthlySubscription>> GetNewSubscriptionsForReportAsync(DateTime from, DateTime to, Guid? vehicleTypeId);
        Task<int> CountActiveSubscriptionsForReportAsync(Guid? vehicleTypeId);
        Task<int> CountExpiredSubscriptionsForReportAsync(Guid? vehicleTypeId);
        Task<int> CountSubscriptionsEndingSoonForReportAsync(Guid? vehicleTypeId);
    }
}
