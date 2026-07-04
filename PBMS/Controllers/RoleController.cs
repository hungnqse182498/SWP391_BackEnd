using BLL.Interfaces;
using Common.DTOs.Role;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PBMS.Controllers
{
    [ApiController]
    [Authorize(Roles = "Admin,admin")]
    [Route("api/[controller]")]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var res = await _roleService.GetAllAsync();
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var res = await _roleService.GetByIdAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRoleDTO dto)
        {
            var res = await _roleService.CreateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateRoleDTO dto)
        {
            var res = await _roleService.UpdateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var res = await _roleService.DeleteAsync(id);
            return StatusCode(res.StatusCode, res);
        }
    }
}
