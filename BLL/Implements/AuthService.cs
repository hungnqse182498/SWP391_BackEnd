using BLL.Interfaces;
using Common.Constrants;
using Common.DTOs;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Common.DTOs.AuthDTO;

namespace BLL.Implements
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IMemoryCache _cache;

        public AuthService(IUnitOfWork unitOfWork, IEmailService emailService, IMemoryCache cache)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _cache = cache;
        }

        public async Task<ResponseDTO> Login(LoginDTO loginDTO)
        {
            var user = await _unitOfWork.UserRepo.FindByEmailAsync(loginDTO.Email);

            if (user == null)
            {
                return new ResponseDTO("Không tìm thấy người dùng", 400, false);
            }

            try
            {
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDTO.Password, user.Password);
                if (!isPasswordValid)
                {
                    return new ResponseDTO("Sai mật khẩu", 400, false);
                }
            }
            catch (BCrypt.Net.SaltParseException ex)
            {
                return new ResponseDTO("Lỗi xác thực mật khẩu (lỗi hệ thống): " + ex.Message, 500, false);
            }

            if (!IsActiveUser(user))
            {
                if (IsBannedUser(user))
                {
                    return new ResponseDTO("Tài khoản của bạn đã bị khóa", 403, false);
                }
                return new ResponseDTO("Tài khoản chưa được kích hoạt hoặc đã bị vô hiệu hóa", 403, false);
            }

            var claims = new List<Claim>
            {
                new Claim(JwtConstant.KeyClaim.Email, user.Email),
                new Claim(JwtConstant.KeyClaim.UserId, user.UserId.ToString()),
                new Claim(JwtConstant.KeyClaim.UserName, user.UserName),
                new Claim(JwtConstant.KeyClaim.RoleId, user.RoleId.ToString()),
                new Claim(JwtConstant.KeyClaim.Role, user.Role?.RoleName ?? "User")
            };

            var refreshTokenKey = JwtProvider.GenerateRefreshToken(user.UserId.ToString());
            var accessTokenKey = JwtProvider.GenerateAccessToken(claims);
            var refreshToken = new RefreshToken
            {
                RefreshTokenKey = refreshTokenKey,
                UserId = user.UserId,
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };
            _unitOfWork.TokenRepo.Add(refreshToken);
            try
            {
                await _unitOfWork.SaveChangeAsync();
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Lỗi lưu refresh token vào cơ sở dữ liệu", 500, false);
            }

            return new ResponseDTO("Đăng nhập thành công", 200, true, new
            {
                User = new
                {
                    user.UserId,
                    user.UserName,
                    user.Email,
                    user.FullName,
                    user.PhoneNumber,
                    user.Role.RoleName
                },
                AccessToken = accessTokenKey,
                RefreshToken = refreshTokenKey,
                
            });
        }

        public async Task<ResponseDTO> SendRegisterOtp(RegisterDTO registerDTO)
        {
            if (string.IsNullOrWhiteSpace(registerDTO.UserName))
            {
                return new ResponseDTO("Vui lòng nhập UserName", 400, false);
            }

            if (string.IsNullOrWhiteSpace(registerDTO.Email))
            {
                return new ResponseDTO("Vui lòng nhập Email", 400, false);
            }

            if (!IsValidEmail(registerDTO.Email))
            {
                return new ResponseDTO("Email sai định dạng", 400, false);
            }

            var existingUser = await _unitOfWork.UserRepo.FindByEmailAsync(registerDTO.Email);
            if (existingUser != null)
            {
                return new ResponseDTO("Email đã được đăng ký.", 400, false);
            }

            if (string.IsNullOrWhiteSpace(registerDTO.Password))
            {
                return new ResponseDTO("Vui lòng nhập mật khẩu", 400, false);
            }

            if (registerDTO.Password != registerDTO.ConfirmPassword)
            {
                return new ResponseDTO("Mật khẩu không khớp", 400, false);
            }

            if (registerDTO.FullName == null)
            {
                return new ResponseDTO("Vui lòng nhập tên đầy đủ", 400, false);
            }

            if (!string.IsNullOrWhiteSpace(registerDTO.PhoneNumber))
            {
                var phoneRegex = new Regex(@"^(0|\+84)(3|5|7|8|9)[0-9]{8}$");
                if (!phoneRegex.IsMatch(registerDTO.PhoneNumber.Trim()) || registerDTO.PhoneNumber.Trim().Length < 9 || registerDTO.PhoneNumber.Trim().Length > 12)
                    return new ResponseDTO("Số điện thoại không hợp lệ", 400, false);

                var existingPhoneNumber = await _unitOfWork.UserRepo.FindByPhoneNumberAsync(registerDTO.PhoneNumber.Trim());
                if (existingPhoneNumber != null)
                    return new ResponseDTO("Số điện thoại đã được đăng ký", 400, false);
            }

            string normalizedEmail = registerDTO.Email.Trim().ToLower();
            string otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            string otpHash = HashSHA256(otp);

            var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
            _cache.Set($"Register_{normalizedEmail}", new { Dto = registerDTO, OtpHash = otpHash }, cacheOptions);

            try
            {
                await _emailService.SendEmailAsync(
                    normalizedEmail,
                    "Mã OTP đăng ký tài khoản",
                    $"Mã OTP đăng ký của bạn là: {otp} (Mã này sẽ hết hạn trong 10 phút)"
                );

                return new ResponseDTO("Nếu email tồn tại, OTP đã được gửi", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Đã xảy ra lỗi khi gửi OTP đăng ký: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> VerifyRegisterOtp(VerifyRegisterOtpDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Otp))
                return new ResponseDTO("Vui lòng nhập đầy đủ thông tin", 400, false);

            string normalizedEmail = dto.Email.Trim().ToLower();

            if (!_cache.TryGetValue($"Register_{normalizedEmail}", out dynamic cachedData))
                return new ResponseDTO("Mã OTP không hợp lệ hoặc đã hết hạn", 400, false);

            string inputOtpHash = HashSHA256(dto.Otp);
            if (inputOtpHash != cachedData.OtpHash)
                return new ResponseDTO("Mã OTP không đúng", 400, false);
            
            RegisterDTO registerDTO = cachedData.Dto;

            var existingUser = await _unitOfWork.UserRepo.FindByEmailAsync(registerDTO.Email);
            if (existingUser != null)
                return new ResponseDTO("Email đã được đăng ký bởi người khác.", 400, false);

            var defaultRole = await _unitOfWork.RoleRepo.GetRoleByNameAsync("User");
            if (defaultRole == null)
                return new ResponseDTO("Lỗi cấu hình hệ thống: Không tìm thấy quyền 'User' mặc định", 500, false);

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDTO.Password);

            var newUser = new User
            {
                UserId = Guid.NewGuid(),
                UserName = registerDTO.UserName,
                Email = registerDTO.Email,
                Password = passwordHash,
                FullName = registerDTO.FullName,
                PhoneNumber = registerDTO.PhoneNumber,
                Status = UserStatus.Active.ToString(),
                RoleId = defaultRole.RoleId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                await _unitOfWork.UserRepo.AddAsync(newUser);
                await _unitOfWork.SaveChangeAsync();
                _cache.Remove($"Register_{normalizedEmail}");
                return new ResponseDTO("Đăng ký thành công", 200, true, new { userId = newUser.UserId });
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi lưu người dùng vào cơ sở dữ liệu (lỗi hệ thống): {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> RequestResetPasswordOtp(RequestOtpDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return new ResponseDTO("Vui lòng nhập Email", 400, false);

            string normalizedEmail = dto.Email.Trim().ToLower();

            var user = await _unitOfWork.UserRepo.FindByEmailAsync(normalizedEmail);
            if (user == null)
                return new ResponseDTO("Nếu email tồn tại, OTP đã được gửi", 200, true);

            string otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            string otpHash = HashSHA256(otp);

            var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
            _cache.Set($"Reset_{normalizedEmail}", otpHash, cacheOptions);

            try
            {
                await _emailService.SendEmailAsync(
                    normalizedEmail,
                    "Mã OTP reset mật khẩu",
                    $"Mã OTP đặt lại mật khẩu của bạn là: {otp} (Mã này sẽ hết hạn trong 10 phút)"
                );

                return new ResponseDTO("Nếu email tồn tại, OTP đã được gửi", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Đã xảy ra lỗi khi gửi OTP reset mật khẩu: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> VerifyResetPasswordOtp(VerifyResetPasswordOtpDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Otp) || string.IsNullOrWhiteSpace(dto.NewPassword))
                return new ResponseDTO("Vui lòng nhập đầy đủ thông tin", 400, false);

            if (dto.NewPassword != dto.ConfirmPassword)
                return new ResponseDTO("Mật khẩu xác nhận không khớp", 400, false);

            string normalizedEmail = dto.Email.Trim().ToLower();

            if (!_cache.TryGetValue($"Reset_{normalizedEmail}", out string cachedOtpHash))
                return new ResponseDTO("Mã OTP không hợp lệ hoặc đã hết hạn", 400, false);

            string inputOtpHash = HashSHA256(dto.Otp);
            if (inputOtpHash != cachedOtpHash)
                return new ResponseDTO("Mã OTP không đúng", 400, false);

            var user = await _unitOfWork.UserRepo.FindByEmailAsync(normalizedEmail);
            if (user == null)
                return new ResponseDTO("Người dùng không tồn tại trên hệ thống", 404, false);

            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.Password = newPasswordHash;
            user.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.UserRepo.UpdateAsync(user);
                await _unitOfWork.SaveChangeAsync();

                _cache.Remove($"Reset_{normalizedEmail}");

                return new ResponseDTO("Đặt lại mật khẩu thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi hệ thống khi cập nhật mật khẩu mới: {ex.Message}", 500, false);
            }
        }


        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ResponseDTO> RenewToken(RefreshTokenDTO tokenDTO)
        {
            if (string.IsNullOrWhiteSpace(tokenDTO.RefreshTokenKey))
            {
                return new ResponseDTO("Vui lòng nhập refresh token", 400, false);
            }

            bool isValid = JwtProvider.Validation(tokenDTO.RefreshTokenKey);
            if (!isValid)
            {
                return new ResponseDTO("Refresh token không hợp lệ hoặc hết hạn", 401, false);
            }

            var storedToken = await _unitOfWork.TokenRepo.GetRefreshTokenByKey(tokenDTO.RefreshTokenKey);
            if (storedToken == null || storedToken.IsRevoked == true)
            {
                return new ResponseDTO("Refresh token không tồn tại hoặc đã bị vô hiệu hóa", 401, false);
            }

            var userIdStr = JwtProvider.GetValueFromToken(tokenDTO.RefreshTokenKey, "nameid");
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
            {
                return new ResponseDTO("Token không chứa thông tin định danh hợp lệ", 401, false);
            }

            var user = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(userId);
            if (user == null)
            {
                return new ResponseDTO("Người dùng không tồn tại trên hệ thống", 404, false);
            }

            if (!IsActiveUser(user))
            {
                return new ResponseDTO("Tài khoản đã bị khóa hoặc vô hiệu hóa. Không thể làm mới token", 403, false);
            }

            var newClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("UserId", user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim("RoleId", user.RoleId.ToString()),
                new Claim(ClaimTypes.Role, user.Role.RoleName) 
            };

            var newAccessToken = JwtProvider.GenerateAccessToken(newClaims);

            return new ResponseDTO("Cấp token mới thành công", 200, true, new
            {
                accessToken = newAccessToken
            });
        }

        private static string HashSHA256(string input)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            StringBuilder builder = new();
            foreach (var b in bytes) builder.Append(b.ToString("x2"));
            return builder.ToString();
        }

        private static bool IsActiveUser(User user)
        {
            return string.Equals(user.Status, UserStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBannedUser(User user)
        {
            return string.Equals(user.Status, UserStatus.Banned.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ResponseDTO> Logout(RefreshTokenDTO tokenDTO)
        {
            if (string.IsNullOrWhiteSpace(tokenDTO.RefreshTokenKey))
            {
                return new ResponseDTO("Vui lòng nhập refresh token", 400, false);
            }
            var storedToken = await _unitOfWork.TokenRepo.GetRefreshTokenByKey(tokenDTO.RefreshTokenKey);
            if (storedToken != null)
            {
                storedToken.IsRevoked = true;

                await _unitOfWork.TokenRepo.UpdateAsync(storedToken); 
                await _unitOfWork.SaveChangeAsync();
            }

            return new ResponseDTO("Đăng xuất thành công", 200, true);
        }
    }
}
