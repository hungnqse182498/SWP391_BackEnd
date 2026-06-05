using System;

namespace Common.DTOs.Reservation
{
    public class ReservationDTO
    {
        public Guid ReservationId { get; set; }
        public Guid UserId { get; set; }
        public string? UserFullName { get; set; }
        public Guid VehicleTypeId { get; set; }
        public string? VehicleTypeName { get; set; }
        public Guid AssignedSlotId { get; set; }
        public string? AssignedSlotCode { get; set; }
        public DateTime ExpectedEntryTime { get; set; }
        public DateTime ExpectedExitTime { get; set; }
        public string Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CreateReservationDTO
    {
        public Guid UserId { get; set; }
        public Guid VehicleTypeId { get; set; }
        public Guid AssignedSlotId { get; set; }
        public DateTime ExpectedEntryTime { get; set; }
        public DateTime ExpectedExitTime { get; set; }
        public string? Status { get; set; }
    }

    public class UpdateReservationDTO
    {
        public Guid ReservationId { get; set; }
        public Guid UserId { get; set; }
        public Guid VehicleTypeId { get; set; }
        public Guid AssignedSlotId { get; set; }
        public DateTime ExpectedEntryTime { get; set; }
        public DateTime ExpectedExitTime { get; set; }
        public string Status { get; set; }
    }
}
