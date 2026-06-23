using System;

namespace Common.DTOs.Subscription
{
    public class MonthlySubscriptionDTO
    {
        public Guid SubscriptionId { get; set; }
        public string? FullName { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string? VehicleType { get; set; }
        public string? PackageName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? FixedSlot { get; set; }
    }


    public class RegisterMonthlySubscriptionDTO
    {
        public Guid PackageId { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
    }

    public class RegisterMonthlySubscriptionPaymentDTO
    {
        public Guid SubscriptionId { get; set; }
        public Guid PaymentId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentLinkId { get; set; }
        public string PaymentUrl { get; set; } = string.Empty;

    }


    
}
