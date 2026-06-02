using Common.DTOs;
using System.Threading.Tasks;
using static Common.DTOs.AuthDTO;

namespace BLL.Interfaces
{
    public interface IAuthService
    {

        Task<ResponseDTO> Login(LoginDTO loginDTO);
        Task<ResponseDTO> Register(RegisterDTO registerDTO);
        Task<ResponseDTO> RenewToken(RefreshTokenDTO refeshTokenDTO);
        Task<ResponseDTO> Logout(RefreshTokenDTO refeshTokenDTO);
    }
}