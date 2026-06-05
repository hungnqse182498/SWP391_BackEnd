using Common.DTOs;
using Common.DTOs.Gate;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IGateService
    {
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreateGateDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdateGateDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
