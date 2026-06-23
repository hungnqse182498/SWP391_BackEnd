using System;

namespace Common.DTOs.ParkingOperation
{
    public class GuestCheckInDTO
    {
        public string LicensePlate { get; set; } = string.Empty;
        public Guid VehicleTypeId { get; set; }
        public string GateName { get; set; } = string.Empty;
        public string? EntryImageUrl { get; set; }
    }

    public class GuestCheckOutDTO
    {
        public string? LicensePlate { get; set; }
        public string GateName { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string? LicensePlateOut { get; set; }
        public string? ExitImageUrl { get; set; }
    }

    public class GuestCheckOutPreviewDTO
    {
        public Guid? SessionId { get; set; }
        public string? LicensePlate { get; set; }
    }

    public class GuestCheckOutLegacyDTO : GuestCheckOutPreviewDTO
    {
        public Guid GateId { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? TransactionReference { get; set; }
        public string? LicensePlateOut { get; set; }
        public string? ExitImageUrl { get; set; }
    }

    public class ResidentCheckInDTO
    {
        public string LicensePlate { get; set; } = string.Empty;
        public Guid VehicleTypeId { get; set; }
        public string GateName { get; set; } = string.Empty;
        public string? EntryImageUrl { get; set; }
    }

    public class ResidentCheckOutDTO
    {
        public string? LicensePlate { get; set; }
        public string GateName { get; set; } = string.Empty;
        public string? LicensePlateOut { get; set; }
        public string? ExitImageUrl { get; set; }
    }

    public class ReservationCheckInDTO
    {
        public Guid ReservationId { get; set; }
        public string LicensePlate { get; set; }
        public Guid GateId { get; set; }
        public string? EntryImageUrl { get; set; }
    }

    public class ParkingAvailabilityDTO
    {
        public Guid FloorId { get; set; }
        public string FloorName { get; set; }
        public Guid? VehicleTypeId { get; set; }
        public string? VehicleTypeName { get; set; }
        public int TotalSlots { get; set; }
        public int AvailableSlots { get; set; }
        public int OccupiedSlots { get; set; }
        public int ReservedSlots { get; set; }
    }

    public class ParkingFeePreviewDTO
    {
        public Guid SessionId { get; set; }
        public string LicensePlate { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public double TotalHours { get; set; }
        public decimal Amount { get; set; }
        public Guid? PricingPolicyId { get; set; }
    }
}
