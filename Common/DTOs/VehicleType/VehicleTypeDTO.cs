using System;

namespace Common.DTOs.VehicleType
{
    public class VehicleTypeDTO
    {
        public Guid VehicleTypeId { get; set; }
        public string TypeName { get; set; }
        public string Dimensions { get; set; }
    }
}