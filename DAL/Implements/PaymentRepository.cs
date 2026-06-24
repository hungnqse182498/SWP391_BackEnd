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
    public class PaymentRepository : GenericRepository<Payment>, IPaymentRepository
    {
        public PaymentRepository(ParkingDBContext context) : base(context)
        {
        }

        public async Task<Payment?> GetByOrderCodeAsync(string orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode)) return null;
            return await _context.Payments.FirstOrDefaultAsync(p => p.TransactionReference.ToLower() == orderCode.ToLower());
        }

        public async Task<Payment?> GetLatestPendingSubscriptionPaymentAsync(Guid subscriptionId)
        {
            return await _context.Payments
                .Where(p => p.SubscriptionId == subscriptionId &&
                            p.PaymentType == "SubscriptionFee" &&
                            p.PaymentStatus == "Pending")
                .OrderByDescending(p => p.PaymentTime)
                .FirstOrDefaultAsync();
        }
    }
}
