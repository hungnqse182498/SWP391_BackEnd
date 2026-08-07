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

        public async Task<decimal> GetSuccessfulDepositAmountAsync(Guid reservationId)
        {
            return await _context.Payments
                .Where(p => p.ReservationId == reservationId &&
                            p.PaymentStatus == PaymentStatus.Success.ToString() &&
                            p.PaymentType == PaymentType.Deposit.ToString())
                .SumAsync(p => p.Amount);
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

        public async Task<Payment?> GetPendingCheckoutPaymentAsync(Guid sessionId)
        {
            return await _context.Payments
                .Where(p => p.SessionId == sessionId &&
                            p.PaymentType == PaymentType.CheckoutFee.ToString() &&
                            p.PaymentStatus == PaymentStatus.Pending.ToString())
                .OrderByDescending(p => p.PaymentTime)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Payment>> GetSuccessfulPaymentsForReportAsync(DateTime from, DateTime to, Guid? vehicleTypeId)
        {
            var query = _context.Payments
                .AsNoTracking()
                .Include(p => p.Session)
                    .ThenInclude(s => s.VehicleType)
                .Include(p => p.Reservation)
                    .ThenInclude(r => r.VehicleType)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.VehicleType)
                .Where(p =>
                    p.PaymentStatus == PaymentStatus.Success.ToString() &&
                    p.PaymentTime >= from &&
                    p.PaymentTime <= to);

            if (vehicleTypeId.HasValue)
            {
                var vId = vehicleTypeId.Value;
                query = query.Where(p =>
                    (p.Session != null && p.Session.VehicleTypeId == vId) ||
                    (p.Reservation != null && p.Reservation.VehicleTypeId == vId) ||
                    (p.Subscription != null && p.Subscription.VehicleTypeId == vId));
            }

            return await query.ToListAsync();
        }
    }
}
