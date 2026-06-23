using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface ISubscriptionRenewalRepository : IGenericRepository<SubscriptionRenewal>
    {
        Task<List<SubscriptionRenewal>> GetAllWithSubscriptionAsync();
        Task<SubscriptionRenewal> GetByIdWithSubscriptionAsync(Guid renewalId);
        Task<List<SubscriptionRenewal>> GetBySubscriptionIdAsync(Guid subscriptionId);
    }
}
