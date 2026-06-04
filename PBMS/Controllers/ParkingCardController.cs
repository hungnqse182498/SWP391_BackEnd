using BLL.Interfaces;
using Common.DTOs.ParkingCard;
using Microsoft.AspNetCore.Mvc;

namespace PBMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ParkingCardController : ControllerBase
    {
        private readonly IParkingCardService _parkingCardService;

        public ParkingCardController(IParkingCardService parkingCardService)
        {
            _parkingCardService = parkingCardService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var res = await _parkingCardService.GetAllAsync();
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var res = await _parkingCardService.GetByIdAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateParkingCardDTO dto)
        {
            var res = await _parkingCardService.CreateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateParkingCardDTO dto)
        {
            var res = await _parkingCardService.UpdateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var res = await _parkingCardService.DeleteAsync(id);
            return StatusCode(res.StatusCode, res);
        }
    }
}
