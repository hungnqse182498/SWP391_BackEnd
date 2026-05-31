using Common.DTOs;
using Common.DTOs.User;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IUserService
    {
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreateUserDTO createUserDTO);
        Task<ResponseDTO> UpdateAsync(UpdateUserDTO updateUserDTO);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
