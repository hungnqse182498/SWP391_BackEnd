using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs.Reservation
{
    public class ReservationDTO
    {
        public Guid ReservationId { get; set; }
        public Guid UserId { get; set; }
        public string UserFullName { get; set; } 
        public Guid VehicleTypeId { get; set; }
        public string VehicleTypeName { get; set; } 
        public DateTime ExpectedEntryTime { get; set; }
        public string Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CreateReservationDTO
    {
        public DateTime ExpectedEntryTime { get; set; }
    }

    public class CreateReservationPaymentDTO
    {
        public Guid ReservationId { get; set; }
        public Guid PaymentId { get; set; }
        public decimal DepositAmount { get; set; }
        public string PaymentLinkId { get; set; }
        public string PaymentUrl { get; set; }
        public string OrderCode { get; set; }

    }

    public class UpdateReservationStatusDTO
    {
        public string Status { get; set; } = string.Empty;
    }
}
