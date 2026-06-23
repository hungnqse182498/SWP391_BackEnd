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

        [HttpPost("payment/{subscriptionId:guid}")]
        public async Task<IActionResult> CreatePayment(Guid subscriptionId)
        {
            var res = await _monthlySubscriptionService.CreatePaymentAsync(subscriptionId, User.GetUserId());
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("my")]
        public async Task<IActionResult> My()
        {
            var res = await _monthlySubscriptionService.GetMyAsync(User.GetUserId());
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Detail(Guid id)
        {
            var res = await _monthlySubscriptionService.GetDetailAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var res = await _monthlySubscriptionService.CancelAsync(id);
            return StatusCode(res.StatusCode, res);
        }
    }
}
