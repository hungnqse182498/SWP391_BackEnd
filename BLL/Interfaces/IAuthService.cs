using Common.DTOs;
using System.Threading.Tasks;
using static Common.DTOs.AuthDTO;

namespace BLL.Interfaces
{
    public interface IAuthService
    {

        Task<ResponseDTO> Login(LoginDTO loginDTO);
        Task<ResponseDTO> SendRegisterOtp(RegisterDTO registerDTO);
        Task<ResponseDTO> VerifyRegisterOtp(VerifyRegisterOtpDTO dto);
        Task<ResponseDTO> RequestResetPasswordOtp(RequestOtpDTO dto);
        Task<ResponseDTO> VerifyResetPasswordOtp(VerifyResetPasswordOtpDTO dto);
        Task<ResponseDTO> RenewToken(RefreshTokenDTO refeshTokenDTO);
        Task<ResponseDTO> Logout(RefreshTokenDTO refeshTokenDTO);
    }
}