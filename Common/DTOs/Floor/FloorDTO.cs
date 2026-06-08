using System;

namespace Common.DTOs.Floor
{
    public class FloorDTO
    {
        public Guid FloorId { get; set; }
        public string FloorName { get; set; }
        public Guid? DedicatedVehicleTypeId { get; set; }
        public string? DedicatedVehicleTypeName { get; set; }
        public int TotalCapacity { get; set; }
        public bool IsResident { get; set; }
    }
}
