using System;

namespace Common.DTOs.Gate
{
    public class GateDTO
    {
        public Guid GateId { get; set; }
        public string GateName { get; set; }
        public string GateType { get; set; }
        public Guid FloorId { get; set; }
        public string? FloorName { get; set; }
    }

    public class CreateGateDTO
    {
        public string GateName { get; set; }
        public string GateType { get; set; }
        public Guid FloorId { get; set; }
    }

    public class UpdateGateDTO
    {
        public Guid GateId { get; set; }
        public string GateName { get; set; }
        public string GateType { get; set; }
        public Guid FloorId { get; set; }
    }
}
