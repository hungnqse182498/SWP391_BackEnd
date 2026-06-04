using System;

namespace Common.DTOs.Payment
{
    public class PaymentDTO
    {
        public Guid PaymentId { get; set; }
        public Guid SessionId { get; set; }
        public Guid? ReservationId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime PaymentTime { get; set; }
        public string PaymentStatus { get; set; }
        public string? TransactionReference { get; set; }
    }

    public class CreatePaymentDTO
    {
        public Guid SessionId { get; set; }
        public Guid? ReservationId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime? PaymentTime { get; set; }
        public string? PaymentStatus { get; set; }
        public string? TransactionReference { get; set; }
    }

    public class UpdatePaymentDTO
    {
        public Guid PaymentId { get; set; }
        public Guid SessionId { get; set; }
        public Guid? ReservationId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime PaymentTime { get; set; }
        public string PaymentStatus { get; set; }
        public string? TransactionReference { get; set; }
    }
}
