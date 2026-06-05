using System;

namespace Common.DTOs.Floor
{
    public class CreateFloorDTO
    {
        public string FloorName { get; set; }
        public Guid? DedicatedVehicleTypeId { get; set; }
        public int TotalCapacity { get; set; }
        public bool IsResident { get; set; }
    }
}
