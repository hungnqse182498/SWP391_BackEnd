using BLL.Interfaces;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;
using static Common.DTOs.AuthDTO;
namespace PBMS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO loginDTO)
    {
        var respone = await _authService.Login(loginDTO);

        return StatusCode(respone.StatusCode, respone);
    }

    [HttpPost("send-register-otp")]
    public async Task<IActionResult> SendRegisterOtp([FromBody] RegisterDTO registerDTO)
    {
        var response = await _authService.SendRegisterOtp(registerDTO);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("verify-register-otp")]
    public async Task<IActionResult> VerifyRegisterOtp([FromBody] VerifyRegisterOtpDTO verifyDTO)
    {
        var response = await _authService.VerifyRegisterOtp(verifyDTO);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("request-reset-password")]
    public async Task<IActionResult> RequestResetPasswordOtp([FromBody] RequestOtpDTO requestDTO)
    {
        var response = await _authService.RequestResetPasswordOtp(requestDTO);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("verify-reset-password")]
    public async Task<IActionResult> VerifyResetPasswordOtp([FromBody] VerifyResetPasswordOtpDTO verifyDTO)
    {
        var response = await _authService.VerifyResetPasswordOtp(verifyDTO);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDTO tokenDTO)
    {
        var response = await _authService.RenewToken(tokenDTO);

        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenDTO tokenDTO)
    {
        var response = await _authService.Logout(tokenDTO);

        if (response.StatusCode == 400) return BadRequest(response);
        if (!response.IsSuccess) return StatusCode(500, response);

        return Ok(response);
    }
}
