using Common.DTOs;
using Common.DTOs.ParkingCard;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IParkingCardService
    {
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreateParkingCardDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdateParkingCardDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
