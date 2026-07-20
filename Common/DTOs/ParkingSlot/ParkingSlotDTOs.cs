using System;

namespace Common.DTOs.ParkingSlot
{
    public class ParkingSlotDTO
    {
        public Guid SlotId { get; set; }
        public Guid FloorId { get; set; }
        public string? FloorName { get; set; }
        public string SlotCode { get; set; }
        public Guid VehicleTypeId { get; set; }
        public string? VehicleTypeName { get; set; }
        public string Status { get; set; }
        public bool IsResident { get; set; }
    }

    public class CreateParkingSlotDTO
    {
        public Guid FloorId { get; set; }
        public string SlotCode { get; set; }
        public Guid VehicleTypeId { get; set; }
        public string? Status { get; set; }
    }

    public class UpdateParkingSlotDTO
    {
        public Guid SlotId { get; set; }
        public Guid FloorId { get; set; }
        public string SlotCode { get; set; }
        public Guid VehicleTypeId { get; set; }
        public string Status { get; set; }
    }
}
