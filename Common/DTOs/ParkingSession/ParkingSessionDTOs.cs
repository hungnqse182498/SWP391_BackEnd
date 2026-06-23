using System;

namespace Common.DTOs.ParkingSession
{
    public class ParkingSessionDTO
    {
        public Guid SessionId { get; set; }
        public Guid? DriverUserId { get; set; }
        public string? DriverFullName { get; set; }
        public string LicensePlateIn { get; set; }
        public string? LicensePlateOut { get; set; }
        public string? EntryImageUrl { get; set; }
        public string? ExitImageUrl { get; set; }
        public Guid VehicleTypeId { get; set; }
        public string? VehicleTypeName { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public Guid EntryGateId { get; set; }
        public string? EntryGateName { get; set; }
        public Guid? ExitGateId { get; set; }
        public string? ExitGateName { get; set; }
        public Guid? AssignedSlotId { get; set; }
        public string? AssignedSlotCode { get; set; }
        public Guid? ActualSlotId { get; set; }
        public string? ActualSlotCode { get; set; }
        public string Status { get; set; }
    }

    public class CreateParkingSessionDTO
    {
        public Guid? DriverUserId { get; set; }
        public string LicensePlateIn { get; set; }
        public string? EntryImageUrl { get; set; }
        public Guid VehicleTypeId { get; set; }
        public DateTime? EntryTime { get; set; }
        public Guid EntryGateId { get; set; }
        public Guid? AssignedSlotId { get; set; }
        public Guid? ActualSlotId { get; set; }
        public string? Status { get; set; }
    }

    public class UpdateParkingSessionDTO
    {
        public Guid SessionId { get; set; }
        public Guid? DriverUserId { get; set; }
        public string LicensePlateIn { get; set; }
        public string? LicensePlateOut { get; set; }
        public string? EntryImageUrl { get; set; }
        public string? ExitImageUrl { get; set; }
        public Guid VehicleTypeId { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public Guid EntryGateId { get; set; }
        public Guid? ExitGateId { get; set; }
        public Guid? AssignedSlotId { get; set; }
        public Guid? ActualSlotId { get; set; }
        public string Status { get; set; }
    }
}
