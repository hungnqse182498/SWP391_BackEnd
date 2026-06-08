using BLL.Interfaces;
using Common.DTOs.ParkingOperation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PBMS.Controllers
{
    [ApiController]
    [Authorize(Roles = "Staff,staff,Manager,manager,Admin,admin")]
    [Route("api/[controller]")]
    public class ParkingOperationController : ControllerBase
    {
        private readonly IParkingOperationService _parkingOperationService;

        public ParkingOperationController(IParkingOperationService parkingOperationService)
        {
            _parkingOperationService = parkingOperationService;
        }

        [HttpPost("guest/check-in")]
        public async Task<IActionResult> GuestCheckIn([FromBody] GuestCheckInDTO dto)
        {
            var res = await _parkingOperationService.GuestCheckInAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("guest/check-out")]
        public async Task<IActionResult> GuestCheckOut([FromBody] GuestCheckOutDTO dto)
        {
            var res = await _parkingOperationService.GuestCheckOutAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("resident/check-in")]
        public async Task<IActionResult> ResidentCheckIn([FromBody] ResidentCheckInDTO dto)
        {
            var res = await _parkingOperationService.ResidentCheckInAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("resident/check-out")]
        public async Task<IActionResult> ResidentCheckOut([FromBody] ResidentCheckOutDTO dto)
        {
            var res = await _parkingOperationService.ResidentCheckOutAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("reservation/check-in")]
        public async Task<IActionResult> ReservationCheckIn([FromBody] ReservationCheckInDTO dto)
        {
            var res = await _parkingOperationService.ReservationCheckInAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("availability")]
        public async Task<IActionResult> GetAvailability([FromQuery] Guid? vehicleTypeId, [FromQuery] string? floorKeyword)
        {
            var res = await _parkingOperationService.GetAvailabilityAsync(vehicleTypeId, floorKeyword);
            return StatusCode(res.StatusCode, res);
        }
    }
}
