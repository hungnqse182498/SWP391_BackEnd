using Common.DTOs;
using Common.DTOs.VehicleType;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IVehicleTypeService
    {
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreateVehicleTypeDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdateVehicleTypeDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}