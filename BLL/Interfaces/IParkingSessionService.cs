using Common.DTOs;
using Common.DTOs.ParkingSession;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IParkingSessionService
    {
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetMyAsync(Guid userId);
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreateParkingSessionDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdateParkingSessionDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
