using BLL.Interfaces;
using Common.DTOs.ParkingSlot;
using Microsoft.AspNetCore.Mvc;

namespace PBMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ParkingSlotController : ControllerBase
    {
        private readonly IParkingSlotService _parkingSlotService;

        public ParkingSlotController(IParkingSlotService parkingSlotService)
        {
            _parkingSlotService = parkingSlotService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var res = await _parkingSlotService.GetAllAsync();
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var res = await _parkingSlotService.GetByIdAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateParkingSlotDTO dto)
        {
            var res = await _parkingSlotService.CreateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateParkingSlotDTO dto)
        {
            var res = await _parkingSlotService.UpdateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var res = await _parkingSlotService.DeleteAsync(id);
            return StatusCode(res.StatusCode, res);
        }
    }
}
