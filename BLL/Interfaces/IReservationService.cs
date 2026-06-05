using Common.DTOs;
using Common.DTOs.Reservation;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IReservationService
    {
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreateReservationDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdateReservationDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
