using BLL.Interfaces;
using Common.DTOs.Reservation;
using Common.DTOs.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers
{
    [Route("api/reservations")]
    [ApiController]
    [Authorize]
    public class ReservationController : ControllerBase
    {
        private readonly IReservationService _reservationService;

        public ReservationController(IReservationService reservationService)

        {
            _reservationService = reservationService;
        }


        private Guid GetUserId() => Guid.Parse(User.FindFirst("UserId")!.Value);
        private string GetUserRole() => User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        [HttpPost]
        public async Task<IActionResult> CreateReservation(CreateReservationDTO dto)
        {
            var userId = GetUserId();
            var result = await _reservationService.CreateReservationAsync(userId, dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("my-reservations")]
        public async Task<IActionResult> GetMyReservations()
        {
            var userId = GetUserId();
            var result = await _reservationService.GetMyReservationsAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{reservationId}")]
        public async Task<IActionResult> GetReservationById(Guid reservationId)
        {
            var userId = GetUserId();
            var role = GetUserRole();
            var result = await _reservationService.GetReservationByIdAsync(reservationId, userId, role);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{reservationId}/cancel")]
        public async Task<IActionResult> CancelReservation(Guid reservationId)
        {
            var userId = GetUserId();
            var result = await _reservationService.CancelReservationAsync(reservationId, userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("check-payment-status/{orderCode}")]
        public async Task<IActionResult> CheckPaymentStatus(string orderCode)
        {
            var result = await _reservationService.CheckPaymentStatusByOrderCodeAsync(orderCode);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet]
        [Authorize(Roles = "Manager,Staff")]
        public async Task<IActionResult> GetAllReservations([FromQuery] string? status, [FromQuery] DateTime? date)
        {
            var result = await _reservationService.GetAllReservationsAsync(status, date);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{reservationId}/status")]
        [Authorize(Roles = "Manager,Staff")]
        public async Task<IActionResult> UpdateReservationStatus(Guid reservationId, [FromBody] UpdateReservationStatusDTO dto)
        {
            var result = await _reservationService.UpdateReservationStatusAsync(reservationId, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}