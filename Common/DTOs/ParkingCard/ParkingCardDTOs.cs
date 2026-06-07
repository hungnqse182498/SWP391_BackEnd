using System;

namespace Common.DTOs.ParkingCard
{
    public class ParkingCardDTO
    {
        public Guid CardId { get; set; }
        public string CardCode { get; set; }
        public string Status { get; set; }
    }

    public class CreateParkingCardDTO
    {
        public string CardCode { get; set; }
        public string? Status { get; set; }
    }

    public class UpdateParkingCardDTO
    {
        public Guid CardId { get; set; }
        public string CardCode { get; set; }
        public string Status { get; set; }
    }
}
