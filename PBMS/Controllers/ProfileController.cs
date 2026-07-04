using BLL.Interfaces;
using Common.DTOs.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBMS.Extensions;

namespace PBMS.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/profile")]
    public class ProfileController : ControllerBase
    {
        private readonly IUserService _userService;

        public ProfileController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var res = await _userService.GetProfileAsync(User.GetUserId());
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDTO dto)
        {
            var res = await _userService.UpdateProfileAsync(User.GetUserId(), dto);
            return StatusCode(res.StatusCode, res);
        }
    }
}
