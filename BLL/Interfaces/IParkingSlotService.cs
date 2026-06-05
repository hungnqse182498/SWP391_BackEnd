using Common.DTOs;
using Common.DTOs.ParkingSlot;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IParkingSlotService
    {
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreateParkingSlotDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdateParkingSlotDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
