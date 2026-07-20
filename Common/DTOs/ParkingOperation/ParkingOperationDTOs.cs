using System;

namespace Common.DTOs.ParkingOperation
{
    public class ParkingCheckInDTO
    {
        public string? CustomerType { get; set; } // Guest, Resident, Reservation
        public string? QrPayload { get; set; }
        public Guid? ReservationId { get; set; }
        public string? LicensePlate { get; set; }
        public Guid? VehicleTypeId { get; set; }
        public Guid GateId { get; set; }
        public string? EntryImageUrl { get; set; }
    }

    public class ParkingCheckOutDTO
    {
        public string? CustomerType { get; set; } // Guest, Resident, Reservation
        public string? QrPayload { get; set; }
        public Guid? SessionId { get; set; }
        public string? LicensePlate { get; set; }
        public string? LicensePlateOut { get; set; }
        public Guid GateId { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ExitImageUrl { get; set; }
    }

    public class ParkingQrDecodeResultDTO
    {
        public string QrPayload { get; set; } = string.Empty;
        public string CodeType { get; set; } = string.Empty;
        public Guid? ReservationId { get; set; }
        public Guid? SessionId { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class ParkingAvailabilityDTO
    {
        public Guid FloorId { get; set; }
        public string FloorName { get; set; } = string.Empty;
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
        public string LicensePlate { get; set; } = string.Empty;
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public double TotalHours { get; set; }
        public decimal Amount { get; set; }
        public Guid? PricingPolicyId { get; set; }
    }
}
