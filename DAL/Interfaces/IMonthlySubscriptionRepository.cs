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
        Task<MonthlySubscription?> GetActiveSubscriptionAsync(string licensePlate);
    }
}
