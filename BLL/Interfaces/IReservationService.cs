using Common.DTOs;
using Common.DTOs.Reservation;

namespace BLL.Interfaces
{
    public interface IReservationService
    {
        Task<ResponseDTO> CreateReservationAsync(Guid userId, CreateReservationDTO dto);
        Task<ResponseDTO> CheckPaymentStatusByOrderCodeAsync(string orderCode);
        Task<ResponseDTO> GetMyReservationsAsync(Guid userId);

        Task<ResponseDTO> GetReservationByIdAsync(Guid reservationId, Guid userId, string userRole);
        Task<ResponseDTO> CancelReservationAsync(Guid reservationId, Guid userId);

        Task<ResponseDTO> GetAllReservationsAsync(string? status, DateTime? date);
        Task<ResponseDTO> UpdateReservationStatusAsync(Guid reservationId, UpdateReservationStatusDTO dto);
    }
}