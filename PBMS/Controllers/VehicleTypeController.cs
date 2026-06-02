using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.VehicleType;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace PBMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VehicleTypeController : ControllerBase
    {
        private readonly IVehicleTypeService _vehicleTypeService;

        public VehicleTypeController(IVehicleTypeService vehicleTypeService)
        {
            _vehicleTypeService = vehicleTypeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var res = await _vehicleTypeService.GetAllAsync();
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var res = await _vehicleTypeService.GetByIdAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateVehicleTypeDTO dto)
        {
            var res = await _vehicleTypeService.CreateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateVehicleTypeDTO dto)
        {
            var res = await _vehicleTypeService.UpdateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var res = await _vehicleTypeService.DeleteAsync(id);
            return StatusCode(res.StatusCode, res);
        }
    }
}