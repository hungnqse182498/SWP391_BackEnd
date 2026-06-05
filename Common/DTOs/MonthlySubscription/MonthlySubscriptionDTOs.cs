using System;

namespace Common.DTOs.MonthlySubscription
{
    public class MonthlySubscriptionDTO
    {
        public Guid SubscriptionId { get; set; }
        public Guid UserId { get; set; }
        public string? UserFullName { get; set; }
        public Guid VehicleTypeId { get; set; }
        public string? VehicleTypeName { get; set; }
        public string LicensePlate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; }
    }

    public class CreateMonthlySubscriptionDTO
    {
        public Guid UserId { get; set; }
        public Guid VehicleTypeId { get; set; }
        public string LicensePlate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Price { get; set; }
        public string? Status { get; set; }
    }

    public class UpdateMonthlySubscriptionDTO
    {
        public Guid SubscriptionId { get; set; }
        public Guid UserId { get; set; }
        public Guid VehicleTypeId { get; set; }
        public string LicensePlate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; }
    }
}
