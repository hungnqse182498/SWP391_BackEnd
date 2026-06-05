using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs.Reservation
{
    public class CreateReservationResponseDTO
    {
        public Guid ReservationId { get; set; }
        public Guid PaymentId { get; set; }
        public decimal DepositAmount { get; set; }
        public string PaymentLinkId { get; set; }
        public string PaymentUrl { get; set; }
        public string OrderCode { get; set; } 

    }
}
