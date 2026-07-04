using Common.DTOs;
using Common.DTOs.Role;

namespace BLL.Interfaces
{
    public interface IRoleService
    {
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreateRoleDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdateRoleDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
