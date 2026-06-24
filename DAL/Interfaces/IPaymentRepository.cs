using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IPaymentRepository : IGenericRepository<Payment>
    {
        Task<Payment?> GetByOrderCodeAsync(string orderCode);
        Task<Payment?> GetLatestPendingSubscriptionPaymentAsync(Guid subscriptionId);

    }
}
