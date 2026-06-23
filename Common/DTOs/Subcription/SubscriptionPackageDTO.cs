using System;

namespace Common.DTOs.Subscription
{
    public class SubscriptionPackageDTO
    {
        public Guid PackageId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public Guid VehicleTypeId { get; set; }
        public string? VehicleTypeName { get; set; }
        public int DurationMonths { get; set; }
        public decimal Price { get; set; }
        public bool RequireFixedSlot { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class CreateSubscriptionPackageDTO
    {
        public string PackageName { get; set; } = string.Empty;
        public Guid VehicleTypeId { get; set; }
        public int DurationMonths { get; set; }
        public decimal Price { get; set; }
        public bool RequireFixedSlot { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = "Active";
    }

    public class UpdateSubscriptionPackageDTO : CreateSubscriptionPackageDTO
    {
    }
}