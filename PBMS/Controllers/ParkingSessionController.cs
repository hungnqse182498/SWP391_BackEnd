using BLL.Interfaces;
using Common.DTOs.ParkingSession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBMS.Extensions;

namespace PBMS.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ParkingSessionController : ControllerBase
    {
        private readonly IParkingSessionService _parkingSessionService;
        private readonly IParkingOperationService _parkingOperationService;

        public ParkingSessionController(
            IParkingSessionService parkingSessionService,
            IParkingOperationService parkingOperationService)
        {
            _parkingSessionService = parkingSessionService;
            _parkingOperationService = parkingOperationService;
        }

        [HttpGet]
        [Authorize(Roles = "Manager, Staff")]
        public async Task<IActionResult> GetAll()
        {
            var res = await _parkingSessionService.GetAllAsync();
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("my")]
        [Authorize(Roles = "Customer, User")]
        public async Task<IActionResult> GetMy()
        {
            var res = await _parkingSessionService.GetMyAsync(User.GetUserId());
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("my/{id:guid}/fee-preview")]
        [Authorize(Roles = "Customer, User")]
        public async Task<IActionResult> GetMyFeePreview(Guid id)
        {
            var res = await _parkingOperationService.GetMyFeePreviewAsync(id, User.GetUserId());
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("my/{id:guid}/checkout-payment")]
        [Authorize(Roles = "Customer, User")]
        public async Task<IActionResult> GetMyCheckoutPayment(Guid id)
        {
            var res = await _parkingOperationService.GetMyCheckoutPaymentAsync(id, User.GetUserId());
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Manager, Staff")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var res = await _parkingSessionService.GetByIdAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost]
        [Authorize(Roles = "Manager, Staff")]
        public async Task<IActionResult> Create([FromBody] CreateParkingSessionDTO dto)
        {
            var res = await _parkingSessionService.CreateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut]
        [Authorize(Roles = "Manager, Staff")]
        public async Task<IActionResult> Update([FromBody] UpdateParkingSessionDTO dto)
        {
            var res = await _parkingSessionService.UpdateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Manager, Staff")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var res = await _parkingSessionService.DeleteAsync(id);
            return StatusCode(res.StatusCode, res);
        }
    }
}
