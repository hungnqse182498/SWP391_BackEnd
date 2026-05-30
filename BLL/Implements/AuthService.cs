using BLL.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using static Common.DTOs.AuthDTO;
using Common.Constrants;

namespace BLL.Implements
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AuthService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
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

            var existRefreshToken = await _unitOfWork.TokenRepo.GetRefreshTokenByUserID(user.UserId);
            if (existRefreshToken != null)
            {
                existRefreshToken.IsRevoked = true;
                await _unitOfWork.TokenRepo.UpdateAsync(existRefreshToken);
            }

            var claims = new List<Claim>
            {
                new Claim(JwtConstant.KeyClaim.Email, user.Email),
                new Claim(JwtConstant.KeyClaim.UserId, user.UserId.ToString()),
                new Claim(JwtConstant.KeyClaim.UserName, user.UserName),
                new Claim(JwtConstant.KeyClaim.RoleId, user.RoleId.ToString())
            };

            var refreshTokenKey = JwtProvider.GenerateRefreshToken(claims);
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
                AccessToken = accessTokenKey,
                RefreshToken = refreshTokenKey,
                User = new
                {
                    user.UserId,
                    user.UserName,
                    user.Email,
                    user.FullName,
                    user.PhoneNumber,
                    user.RoleId
                }
            });
        }

        public async Task<ResponseDTO> Register(RegisterDTO registerDTO)
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

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDTO.Password);

            var newUser = new User
            {
                UserName = registerDTO.UserName,
                Email = registerDTO.Email,
                Password = passwordHash,                      
                FullName = registerDTO.FullName ?? "Chưa đặt tên", 
                PhoneNumber = registerDTO.PhoneNumber ?? "",       
                Status = "Active",                                 
                RoleId = 1,                                        
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                await _unitOfWork.UserRepo.AddAsync(newUser);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Đăng ký thành công", 200, true, new { userId = newUser.UserId });
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi lưu người dùng vào cơ sở dữ liệu (lỗi hệ thống): {ex.Message}", 500, false);
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
    }
}