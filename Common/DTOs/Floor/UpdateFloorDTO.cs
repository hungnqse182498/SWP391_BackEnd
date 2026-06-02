using System;

namespace Common.DTOs.Floor
{
    public class UpdateFloorDTO
    {
        public Guid FloorId { get; set; }
        public string FloorName { get; set; }
        public Guid? DedicatedVehicleTypeId { get; set; }
        public int TotalCapacity { get; set; }
    }
}
