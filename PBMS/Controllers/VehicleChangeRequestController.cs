using BLL.Implements;
using BLL.Interfaces;
using Common.DTOs.Subscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBMS.Extensions;

namespace PBMS.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class VehicleChangeRequestController : ControllerBase
    {
        private readonly IVehicleChangeRequestService _vehicleChangeRequestService;

        public VehicleChangeRequestController(IVehicleChangeRequestService vehicleChangeRequestService)
        {
            _vehicleChangeRequestService = vehicleChangeRequestService;
        }

        [Authorize(Roles = "Customer,User")]
        [HttpPost("change-vehicle")]
        public async Task<IActionResult> ChangeVehicle([FromBody] CreateVehicleChangeDTO dto)
        {
            var res = await _vehicleChangeRequestService.CreateVehicleChangeAsync(User.GetUserId(), dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("change-vehicle/{id:guid}")]
        public async Task<IActionResult> GetVehicleChangeRequestById(Guid id)
        {
            var res = await _vehicleChangeRequestService.GetVehicleChangeRequestByIdAsync(
                id,
                User.GetUserId(),
                User.IsInRole("Manager"));
            return StatusCode(res.StatusCode, res);
        }

        [Authorize(Roles = "Customer,User")]
        [HttpGet("my-requests")]
        public async Task<IActionResult> GetMyVehicleChangeRequests()
        {
            var res = await _vehicleChangeRequestService.GetRequestsByUserIdAsync(User.GetUserId());
            return StatusCode(res.StatusCode, res);
        }

        [Authorize(Roles = "Manager")]
        [HttpGet("change-vehicle")]
        public async Task<IActionResult> GetVehicleChangeRequests()
        {
            var res = await _vehicleChangeRequestService.GetVehicleChangeRequestsAsync();
            return StatusCode(res.StatusCode, res);
        }

        [Authorize(Roles = "Manager")]
        [HttpPut("change-vehicle/{id:guid}/approve")]
        public async Task<IActionResult> ApproveChangeVehicle(Guid id)
        {
            var res = await _vehicleChangeRequestService.ApproveVehicleChangeAsync(id, User.GetUserId());
            return StatusCode(res.StatusCode, res);
        }

        [Authorize(Roles = "Manager")]
        [HttpPut("change-vehicle/{id:guid}/reject")]
        public async Task<IActionResult> RejectChangeVehicle(Guid id, [FromBody] RejectVehicleChangeDTO dto)
        {
            var res = await _vehicleChangeRequestService.RejectVehicleChangeAsync(id, User.GetUserId(), dto);
            return StatusCode(res.StatusCode, res);
        }

        [Authorize(Roles = "Customer,User")]
        [HttpPut("change-vehicle/{id:guid}")]
        public async Task<IActionResult> UpdateVehicleChangeRequest(Guid id, [FromBody] UpdateVehicleChangeDTO dto)
        {
            var res = await _vehicleChangeRequestService.UpdateVehicleChangeRequestAsync(id, User.GetUserId(), dto);
            return StatusCode(res.StatusCode, res);
        }

        [Authorize(Roles = "Customer,User")]
        [HttpDelete("change-vehicle/{id:guid}")]
        public async Task<IActionResult> DeleteVehicleChangeRequest(Guid id)
        {
            var res = await _vehicleChangeRequestService.DeleteVehicleChangeRequestAsync(id, User.GetUserId());
            return StatusCode(res.StatusCode, res);
        }
    }
}
