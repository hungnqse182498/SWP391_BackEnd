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
        Task<bool> HasUsablePlateAsync(string plate, Guid? ignoredSubscriptionId = null);
        Task<IEnumerable<MonthlySubscription>> GetAllWithDetailsAsync();
    }
}
