using Common.DTOs;
using Common.DTOs.Reservation;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IReservationService
    {
        Task<ResponseDTO> CreateReservationAsync(Guid userId, CreateReservationDTO dto);
        Task<ResponseDTO> CreatePaymentLinkForReservationAsync(Guid reservationId, Guid userId);
        Task<ResponseDTO> ChangeReservationTimeAsync(Guid reservationId, DateTime newExpectedTime);

        Task<ResponseDTO> CheckPaymentStatusByOrderCodeAsync(string orderCode);
        Task<ResponseDTO> GetMyReservationsAsync(Guid userId);

        Task<ResponseDTO> GetReservationByIdAsync(Guid reservationId, Guid userId, string userRole);
        Task<ResponseDTO> CancelReservationAsync(Guid reservationId, Guid userId);

        Task<ResponseDTO> GetAllReservationsAsync(string? status, DateTime? date);
        Task<ResponseDTO> UpdateReservationStatusAsync(Guid reservationId, UpdateReservationStatusDTO dto);
        Task ProcessNoShowTimeoutAsync(Guid reservationId);
        Task ProcessOverdueReservationsAsync();
    }
}
