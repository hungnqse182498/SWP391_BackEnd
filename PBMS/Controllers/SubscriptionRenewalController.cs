using BLL.Implements;
using BLL.Interfaces;
using Common.DTOs.Subscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBMS.Extensions;
using System;
using System.Threading.Tasks;

namespace WebAPI.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class SubscriptionRenewalController : ControllerBase
    {
        private readonly ISubscriptionRenewalService _renewalService;

        public SubscriptionRenewalController(ISubscriptionRenewalService renewalService)
        {
            _renewalService = renewalService;
        }

        [HttpPost("{id:guid}/renew")]
        public async Task<IActionResult> Renew(Guid id, [FromBody] RenewSubscriptionDTO dto)
        {
            var res = await _renewalService.RenewAsync(id, User.GetUserId(), dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}/renewals")]
        public async Task<IActionResult> Renewals(Guid id)
        {
            var res = await _renewalService.GetRenewalsAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [Authorize(Roles = "Manager")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _renewalService.GetAllAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            var result = await _renewalService.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Manager")]
        [HttpPost("direct-renew")]
        public async Task<IActionResult> DirectRenew([FromBody] CreateDirectRenewalDTO dto)
        {
            var result = await _renewalService.CreateDirectAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Manager")]
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateRenewalDTO dto)
        {
            var result = await _renewalService.UpdateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Manager")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            var result = await _renewalService.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}