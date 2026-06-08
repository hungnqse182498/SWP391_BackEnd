using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Floor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace PBMS.Controllers
{
    [ApiController]
    [Authorize(Roles = "Manager")]
    [Route("api/[controller]")]
    public class FloorController : ControllerBase
    {
        private readonly IFloorService _floorService;

        public FloorController(IFloorService floorService)
        {
            _floorService = floorService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var res = await _floorService.GetAllAsync();
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var res = await _floorService.GetByIdAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateFloorDTO dto)
        {
            var res = await _floorService.CreateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateFloorDTO dto)
        {
            var res = await _floorService.UpdateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var res = await _floorService.DeleteAsync(id);
            return StatusCode(res.StatusCode, res);
        }
    }
}