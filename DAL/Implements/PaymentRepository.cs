using DAL.Interfaces;
using Common.Enums;
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

        public async Task<List<Payment>> GetAllOrderedByPaymentTimeAsync()
        {
            return await _context.Payments
                .OrderByDescending(p => p.PaymentTime)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Payment?> GetByOrderCodeAsync(string orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode)) return null;

            var normalizedOrderCode = orderCode.Trim().ToLower();
            return await _context.Payments
                .FirstOrDefaultAsync(p => p.TransactionReference != null &&
                                          p.TransactionReference.ToLower() == normalizedOrderCode);
        }

        public async Task<Payment?> GetLatestPendingSubscriptionPaymentAsync(Guid subscriptionId)
        {
            return await _context.Payments
                .Where(p => p.SubscriptionId == subscriptionId &&
                            p.PaymentType == PaymentType.SubscriptionFee.ToString() &&
                            p.PaymentStatus == PaymentStatus.Pending.ToString())
                .OrderByDescending(p => p.PaymentTime)
                .FirstOrDefaultAsync();
        }
    }
}
