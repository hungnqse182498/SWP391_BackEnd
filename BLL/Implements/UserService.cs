using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.User;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace BLL.Implements
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private const string AdminRoleName = "Admin";

        public UserService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var users = await _unitOfWork.UserRepo.GetAllWithRoleAsync();
            if (users.Count == 0)
            {
                return new ResponseDTO("Không tìm thấy người dùng nào trong hệ thống", 404, false);
            }

            return new ResponseDTO("Lấy danh sách người dùng thành công", 200, true, users.Select(MapToUserDTO).ToList());
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

            return new ResponseDTO("Tìm thấy người dùng thành công", 200, true, MapToUserDTO(user));
        }

        public async Task<ResponseDTO> GetProfileAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                return new ResponseDTO("Vui lòng đăng nhập", 401, false);
            }

            var user = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(userId);
            if (user == null)
            {
                return new ResponseDTO("Không tìm thấy người dùng", 404, false);
            }

            return new ResponseDTO("Lấy thông tin cá nhân thành công", 200, true, MapToUserDTO(user));
        }

        public async Task<ResponseDTO> UpdateProfileAsync(Guid userId, UpdateProfileDTO dto)
        {
            if (userId == Guid.Empty)
            {
                return new ResponseDTO("Vui lòng đăng nhập", 401, false);
            }

            if (dto == null)
            {
                return new ResponseDTO("Dữ liệu cập nhật thông tin cá nhân không hợp lệ", 400, false);
            }

            var user = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(userId);
            if (user == null)
            {
                return new ResponseDTO("Không tìm thấy người dùng", 404, false);
            }

            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                var email = dto.Email.Trim();
                if (!IsValidEmail(email))
                {
                    return new ResponseDTO("Email sai định dạng", 400, false);
                }

                if (await _unitOfWork.UserRepo.IsEmailDuplicateAsync(email, userId))
                {
                    return new ResponseDTO("Email đã được sử dụng", 400, false);
                }

                user.Email = email;
            }

            if (!string.IsNullOrWhiteSpace(dto.FullName))
            {
                user.FullName = dto.FullName.Trim();
            }

            var phoneNumber = NormalizeOptionalText(dto.PhoneNumber);
            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                if (await _unitOfWork.UserRepo.IsPhoneNumberDuplicateAsync(phoneNumber, userId))
                {
                    return new ResponseDTO("Số điện thoại đã được sử dụng", 400, false);
                }

                user.PhoneNumber = phoneNumber;
            }

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            }

            user.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.UserRepo.UpdateAsync(user);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Cập nhật thông tin cá nhân thành công", 200, true, MapToUserDTO(user));
            }
            catch (DbUpdateException)
            {
                return new ResponseDTO("Dữ liệu thông tin cá nhân bị trùng hoặc không hợp lệ", 400, false);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi cập nhật thông tin cá nhân: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> GetManageableRolesAsync()
        {
            var roles = await _unitOfWork.RoleRepo.GetAssignableRolesAsync(AdminRoleName);
            if (roles.Count == 0)
            {
                return new ResponseDTO("Không tìm thấy quyền có thể gán trong hệ thống", 404, false);
            }

            return new ResponseDTO("Lấy danh sách quyền có thể gán thành công", 200, true, roles.Select(MapToRoleDTO).ToList());
        }

        public async Task<ResponseDTO> CreateAsync(CreateUserDTO createUserDTO)
        {
            if (createUserDTO == null)
            {
                return new ResponseDTO("Dữ liệu tạo người dùng không hợp lệ", 400, false);
            }

            var validation = ValidateUserFields(createUserDTO.UserName, createUserDTO.Email);
            if (validation != null)
            {
                return validation;
            }

            if (string.IsNullOrWhiteSpace(createUserDTO.Password))
            {
                return new ResponseDTO("Vui lòng nhập mật khẩu", 400, false);
            }

            var roleValidation = await ValidateAssignableRoleAsync(createUserDTO.RoleId, createUserDTO.RoleName);
            if (roleValidation.Error != null)
            {
                return roleValidation.Error;
            }

            var userName = createUserDTO.UserName.Trim();
            var email = createUserDTO.Email.Trim();
            var phoneNumber = NormalizeOptionalText(createUserDTO.PhoneNumber);

            var duplicateValidation = await ValidateDuplicateUserAsync(userName, email, phoneNumber, null);
            if (duplicateValidation != null)
            {
                return duplicateValidation;
            }

            var now = DateTime.UtcNow;
            var user = new User
            {
                UserId = Guid.NewGuid(),
                UserName = userName,
                Email = email,
                Password = BCrypt.Net.BCrypt.HashPassword(createUserDTO.Password),
                FullName = NormalizeFullName(createUserDTO.FullName),
                PhoneNumber = phoneNumber,
                RoleId = roleValidation.Role!.RoleId,
                Status = UserStatus.Active.ToString(),
                CreatedAt = now,
                UpdatedAt = now
            };

            try
            {
                await _unitOfWork.UserRepo.AddAsync(user);
                await _unitOfWork.SaveChangeAsync();

                user.Role = roleValidation.Role;
                return new ResponseDTO("Tạo người dùng thành công", 201, true, MapToUserDTO(user));
            }
            catch (DbUpdateException)
            {
                return new ResponseDTO("Dữ liệu người dùng bị trùng hoặc không hợp lệ, vui lòng kiểm tra lại", 400, false);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi tạo người dùng: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> UpdateAsync(UpdateUserDTO updateUserDTO)
        {
            if (updateUserDTO == null)
            {
                return new ResponseDTO("Dữ liệu cập nhật người dùng không hợp lệ", 400, false);
            }

            if (updateUserDTO.UserId == Guid.Empty)
            {
                return new ResponseDTO("Vui lòng nhập UserId", 400, false);
            }

            var user = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(updateUserDTO.UserId);
            if (user == null)
            {
                return new ResponseDTO("Không tìm thấy người dùng", 404, false);
            }

            if (IsAdminRole(user.Role))
            {
                return new ResponseDTO("Không thể chỉnh sửa tài khoản admin", 400, false);
            }

            var validation = ValidateUserFields(updateUserDTO.UserName, updateUserDTO.Email);
            if (validation != null)
            {
                return validation;
            }

            var roleValidation = await ValidateAssignableRoleAsync(updateUserDTO.RoleId, updateUserDTO.RoleName);
            if (roleValidation.Error != null)
            {
                return roleValidation.Error;
            }

            var userName = updateUserDTO.UserName.Trim();
            var email = updateUserDTO.Email.Trim();
            var phoneNumber = NormalizeOptionalText(updateUserDTO.PhoneNumber);

            var duplicateValidation = await ValidateDuplicateUserAsync(userName, email, phoneNumber, updateUserDTO.UserId);
            if (duplicateValidation != null)
            {
                return duplicateValidation;
            }

            user.UserName = userName;
            user.Email = email;
            user.FullName = NormalizeFullName(updateUserDTO.FullName);
            user.PhoneNumber = phoneNumber;
            user.RoleId = roleValidation.Role!.RoleId;
            user.Role = roleValidation.Role;
            user.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(updateUserDTO.Password))
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(updateUserDTO.Password);
            }

            try
            {
                await _unitOfWork.UserRepo.UpdateAsync(user);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Cập nhật người dùng thành công", 200, true, MapToUserDTO(user));
            }
            catch (DbUpdateException)
            {
                return new ResponseDTO("Dữ liệu người dùng bị trùng hoặc không hợp lệ, vui lòng kiểm tra lại", 400, false);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi cập nhật người dùng: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> UpdateStatusAsync(Guid id, UpdateUserStatusDTO updateUserStatusDTO)
        {
            if (id == Guid.Empty)
            {
                return new ResponseDTO("Vui lòng nhập UserId", 400, false);
            }

            if (updateUserStatusDTO == null || string.IsNullOrWhiteSpace(updateUserStatusDTO.Status))
            {
                return new ResponseDTO("Vui lòng nhập trạng thái người dùng", 400, false);
            }

            var normalizedStatus = NormalizeEnum<UserStatus>(updateUserStatusDTO.Status);
            if (normalizedStatus == null)
            {
                return new ResponseDTO("Trạng thái chỉ được là Active, Inactive hoặc Banned", 400, false);
            }

            var user = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(id);
            if (user == null)
            {
                return new ResponseDTO("Không tìm thấy người dùng", 404, false);
            }

            if (IsAdminRole(user.Role))
            {
                return new ResponseDTO("Không thể thay đổi trạng thái tài khoản admin", 400, false);
            }

            user.Status = normalizedStatus;
            user.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.UserRepo.UpdateAsync(user);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Cập nhật trạng thái người dùng thành công", 200, true, MapToUserDTO(user));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi cập nhật trạng thái người dùng: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
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

            if (IsAdminRole(user.Role))
            {
                return new ResponseDTO("Không thể xóa tài khoản admin", 400, false);
            }

            user.Status = UserStatus.Inactive.ToString();
            user.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.UserRepo.UpdateAsync(user);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Xóa người dùng thành công", 200, true, MapToUserDTO(user));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi xóa người dùng: {ex.Message}", 500, false);
            }
        }

        private async Task<(Role? Role, ResponseDTO? Error)> ValidateAssignableRoleAsync(Guid? roleId, string? roleName)
        {
            if ((!roleId.HasValue || roleId.Value == Guid.Empty) && string.IsNullOrWhiteSpace(roleName))
            {
                return (null, new ResponseDTO("Vui lòng chọn quyền cho người dùng", 400, false));
            }

            Role? role;
            if (roleId.HasValue && roleId.Value != Guid.Empty)
            {
                role = await _unitOfWork.RoleRepo.GetByIdAsync(roleId.Value);
            }
            else
            {
                role = await _unitOfWork.RoleRepo.GetRoleByNameAsync(roleName!);
            }

            if (role == null)
            {
                return (null, new ResponseDTO("Quyền người dùng không tồn tại", 404, false));
            }

            if (IsAdminRole(role))
            {
                return (null, new ResponseDTO("Không được tạo hoặc gán quyền admin", 400, false));
            }

            return (role, null);
        }

        private async Task<ResponseDTO?> ValidateDuplicateUserAsync(
           string userName,
           string email,
           string? phoneNumber,
           Guid? currentUserId)
        {
            if (await _unitOfWork.UserRepo.IsUserNameDuplicateAsync(userName, currentUserId))
            {
                return new ResponseDTO("UserName đã tồn tại", 400, false);
            }

            if (await _unitOfWork.UserRepo.IsEmailDuplicateAsync(email, currentUserId))
            {
                return new ResponseDTO("Email đã được sử dụng", 400, false);
            }

            if (!string.IsNullOrWhiteSpace(phoneNumber) &&
                await _unitOfWork.UserRepo.IsPhoneNumberDuplicateAsync(phoneNumber, currentUserId))
            {
                return new ResponseDTO("Số điện thoại đã được sử dụng", 400, false);
            }

            return null;
        }

        private static ResponseDTO? ValidateUserFields(string? userName, string? email)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return new ResponseDTO("Vui lòng nhập UserName", 400, false);
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return new ResponseDTO("Vui lòng nhập Email", 400, false);
            }

            if (!IsValidEmail(email.Trim()))
            {
                return new ResponseDTO("Email sai định dạng", 400, false);
            }

            return null;
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var address = new MailAddress(email);
                return address.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsAdminRole(Role? role)
        {
            return string.Equals(role?.RoleName, AdminRoleName, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeFullName(string? fullName)
        {
            return string.IsNullOrWhiteSpace(fullName) ? "Chưa đặt tên" : fullName.Trim();
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string? NormalizeEnum<TEnum>(string? value) where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return Enum.TryParse<TEnum>(value.Trim(), true, out var parsed) ? parsed.ToString() : null;
        }

        private static UserDTO MapToUserDTO(User user)
        {
            return new UserDTO
            {
                UserId = user.UserId,
                UserName = user.UserName,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Status = user.Status,
                RoleId = user.RoleId,
                RoleName = user.Role?.RoleName ?? "Chưa phân quyền",
                CreatedAt = user.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = user.UpdatedAt ?? DateTime.UtcNow
            };
        }

        private static UserRoleDTO MapToRoleDTO(Role role)
        {
            return new UserRoleDTO
            {
                RoleId = role.RoleId,
                RoleName = role.RoleName,
                Description = role.Description ?? string.Empty
            };
        }
    }
}
