using Common.DTOs;
using Common.DTOs.Floor;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IFloorService
    {
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreateFloorDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdateFloorDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
