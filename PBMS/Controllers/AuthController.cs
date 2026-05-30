using BLL.Interfaces;
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

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDTO registerDTO)
    {
        var respone = await _authService.Register(registerDTO);

        return StatusCode(respone.StatusCode, respone);
    }
}
