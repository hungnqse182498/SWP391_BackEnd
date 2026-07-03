using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Role;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements
{
    public class RoleService : IRoleService
    {
        private readonly IUnitOfWork _unitOfWork;
        private const string AdminRoleName = "Admin";

        public RoleService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var roles = await _unitOfWork.RoleRepo.GetAllOrderedByNameAsync();
            if (roles.Count == 0)
            {
                return new ResponseDTO("Không tìm thấy quyền nào trong hệ thống", 404, false);
            }

            return new ResponseDTO("Lấy danh sách quyền thành công", 200, true, roles.Select(MapToDTO).ToList());
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                return new ResponseDTO("Vui lòng nhập RoleId", 400, false);
            }

            var role = await _unitOfWork.RoleRepo.GetByIdAsync(id);
            if (role == null)
            {
                return new ResponseDTO("Không tìm thấy quyền", 404, false);
            }

            return new ResponseDTO("Lấy thông tin quyền thành công", 200, true, MapToDTO(role));
        }

        public async Task<ResponseDTO> CreateAsync(CreateRoleDTO dto)
        {
            if (dto == null)
            {
                return new ResponseDTO("Dữ liệu tạo quyền không hợp lệ", 400, false);
            }

            var validation = await ValidateRoleAsync(dto.RoleName, null);
            if (validation != null)
            {
                return validation;
            }

            var role = new Role
            {
                RoleId = Guid.NewGuid(),
                RoleName = dto.RoleName.Trim(),
                Description = NormalizeOptionalText(dto.Description) ?? string.Empty
            };

            try
            {
                await _unitOfWork.RoleRepo.AddAsync(role);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Tạo quyền thành công", 201, true, MapToDTO(role));
            }
            catch (DbUpdateException)
            {
                return new ResponseDTO("Tên quyền đã tồn tại hoặc dữ liệu không hợp lệ", 400, false);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi tạo quyền: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> UpdateAsync(UpdateRoleDTO dto)
        {
            if (dto == null || dto.RoleId == Guid.Empty)
            {
                return new ResponseDTO("Dữ liệu cập nhật quyền không hợp lệ", 400, false);
            }

            var role = await _unitOfWork.RoleRepo.GetByIdAsync(dto.RoleId);
            if (role == null)
            {
                return new ResponseDTO("Không tìm thấy quyền", 404, false);
            }

            if (IsAdminRole(role))
            {
                return new ResponseDTO("Không thể chỉnh sửa quyền admin", 400, false);
            }

            var validation = await ValidateRoleAsync(dto.RoleName, dto.RoleId);
            if (validation != null)
            {
                return validation;
            }

            role.RoleName = dto.RoleName.Trim();
            role.Description = NormalizeOptionalText(dto.Description) ?? string.Empty;

            try
            {
                await _unitOfWork.RoleRepo.UpdateAsync(role);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Cập nhật quyền thành công", 200, true, MapToDTO(role));
            }
            catch (DbUpdateException)
            {
                return new ResponseDTO("Tên quyền đã tồn tại hoặc dữ liệu không hợp lệ", 400, false);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi cập nhật quyền: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                return new ResponseDTO("Vui lòng nhập RoleId", 400, false);
            }

            var role = await _unitOfWork.RoleRepo.GetByIdAsync(id);
            if (role == null)
            {
                return new ResponseDTO("Không tìm thấy quyền", 404, false);
            }

            if (IsAdminRole(role))
            {
                return new ResponseDTO("Không thể xóa quyền admin", 400, false);
            }

            if (await _unitOfWork.RoleRepo.HasUsersAsync(id))
            {
                return new ResponseDTO("Không thể xóa quyền đang được gán cho người dùng", 400, false);
            }

            try
            {
                _unitOfWork.RoleRepo.Delete(role);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Xóa quyền thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi xóa quyền: {ex.Message}", 500, false);
            }
        }

        private async Task<ResponseDTO?> ValidateRoleAsync(string? roleName, Guid? currentRoleId)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return new ResponseDTO("Vui lòng nhập tên quyền", 400, false);
            }

            var trimmedRoleName = roleName.Trim();
            if (string.Equals(trimmedRoleName, AdminRoleName, StringComparison.OrdinalIgnoreCase))
            {
                return new ResponseDTO("Không được tạo hoặc đổi tên quyền admin", 400, false);
            }

            if (trimmedRoleName.Length > 50)
            {
                return new ResponseDTO("Tên quyền không được vượt quá 50 ký tự", 400, false);
            }

            if (await _unitOfWork.RoleRepo.IsRoleNameDuplicateAsync(trimmedRoleName, currentRoleId))
            {
                return new ResponseDTO("Tên quyền đã tồn tại", 400, false);
            }

            return null;
        }

        private static bool IsAdminRole(Role? role)
        {
            return string.Equals(role?.RoleName, AdminRoleName, StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static RoleDTO MapToDTO(Role role)
        {
            return new RoleDTO
            {
                RoleId = role.RoleId,
                RoleName = role.RoleName,
                Description = role.Description ?? string.Empty
            };
        }
    }
}
