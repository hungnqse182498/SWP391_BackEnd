using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.User;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Implements
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;

        public UserService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<ResponseDTO> GetAllAsync()
        {
            var users = await _unitOfWork.UserRepo.GetAll().Include(u => u.Role).ToListAsync();

            if (users == null || !users.Any())
            {
                return new ResponseDTO("Không tìm thấy người dùng nào trong hệ thống", 404, false);
            }

            var userDTOs = users.Select(u => new UserDTO
            {
                UserId = u.UserId,
                UserName = u.UserName,
                Email = u.Email,
                FullName = u.FullName,
                PhoneNumber = u.PhoneNumber ?? "Chưa cập nhật",
                Status = u.Status,

                RoleName = u.Role?.RoleName ?? "Chưa phân quyền",

                CreatedAt = u.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = u.UpdatedAt ?? DateTime.UtcNow
            }).ToList();

            return new ResponseDTO("Lấy danh sách người dùng thành công", 200, true, userDTOs);
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                return new ResponseDTO("Vui lòng nhập UserId", 400, false);
            }

            var user = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(id);
            if (user == null)
            {
                return new ResponseDTO("Không tìm thấy người dùng", 404, false);
            }

            var userDTO = new UserDTO
            {
                UserId = user.UserId,
                UserName = user.UserName,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber ?? "Chưa cập nhật",
                Status = user.Status,

                RoleName = user.Role?.RoleName ?? "Chưa phân quyền",

                CreatedAt = user.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = user.UpdatedAt ?? DateTime.UtcNow
            };

            return new ResponseDTO("Tìm thấy người dùng thành công", 200, true, userDTO);
        }

        public Task<ResponseDTO> CreateAsync(CreateUserDTO createUserDTO)
        {
            throw new NotImplementedException();
        }

        public Task<ResponseDTO> UpdateAsync(UpdateUserDTO updateUserDTO)
        {
            throw new NotImplementedException();
        }

        public Task<ResponseDTO> DeleteAsync(Guid id)
        {
            throw new NotImplementedException();
        }
    }
            
}
