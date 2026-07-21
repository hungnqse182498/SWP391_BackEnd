using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs.Subscription
{
    public class VehicleChangeRequestDTO
    {
        public Guid RequestId { get; set; }
        public Guid SubscriptionId { get; set; }
        public string? OldLicensePlate { get; set; }
        public string? NewLicensePlate { get; set; }
        public string? Reason { get; set; }
        public string? RejectionReason { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? UserFullName { get; set; }
        public string? PackageName { get; set; }
        public Guid? HandledByStaffId { get; set; }
        public string? HandledByFullName { get; set; }
    }

    public class CreateVehicleChangeDTO
    {
        public Guid SubscriptionId { get; set; }
        public string NewLicensePlate { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class UpdateVehicleChangeDTO
    {
        public string NewLicensePlate { get; set; } = null!;
        public string? Reason { get; set; }
    }

    public class RejectVehicleChangeDTO
    {
        public string Reason { get; set; } = string.Empty;
    }
}
