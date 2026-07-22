using BLL.Interfaces;
using Common.DTOs.Subscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBMS.Extensions;
using System;
using System.Threading.Tasks;

namespace PBMS.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class MonthlySubscriptionController : ControllerBase
    {
        private readonly IMonthlySubscriptionService _monthlySubscriptionService;

        public MonthlySubscriptionController(IMonthlySubscriptionService monthlySubscriptionService)
        {
            _monthlySubscriptionService = monthlySubscriptionService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterMonthlySubscriptionDTO dto)
        {
            var res = await _monthlySubscriptionService.RegisterAsync(User.GetUserId(), dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Create([FromBody] ManagerCreateMonthlySubscriptionDTO dto)
        {
            var res = await _monthlySubscriptionService.CreateForUserAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetAll()
        {
            var res = await _monthlySubscriptionService.GetAllAsync();
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("my")]
        public async Task<IActionResult> My()
        {
            var res = await _monthlySubscriptionService.GetMyAsync(User.GetUserId());
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("user/{userId:guid}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetByUser(Guid userId)
        {
            var res = await _monthlySubscriptionService.GetByUserAsync(userId);
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Detail(Guid id)
        {
            var res = await _monthlySubscriptionService.GetDetailAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMonthlySubscriptionDTO dto)
        {
            var res = await _monthlySubscriptionService.UpdateAsync(id, dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var res = await _monthlySubscriptionService.CancelAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var res = await _monthlySubscriptionService.DeleteAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("payment/{subscriptionId:guid}")]
        public async Task<IActionResult> CreatePayment(Guid subscriptionId)
        {
            var res = await _monthlySubscriptionService.CreatePaymentAsync(subscriptionId, User.GetUserId());
            return StatusCode(res.StatusCode, res);
        }
    }
}
