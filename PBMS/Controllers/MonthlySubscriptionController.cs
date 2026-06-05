using BLL.Interfaces;
using Common.DTOs.MonthlySubscription;
using Microsoft.AspNetCore.Mvc;

namespace PBMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MonthlySubscriptionController : ControllerBase
    {
        private readonly IMonthlySubscriptionService _monthlySubscriptionService;

        public MonthlySubscriptionController(IMonthlySubscriptionService monthlySubscriptionService)
        {
            _monthlySubscriptionService = monthlySubscriptionService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var res = await _monthlySubscriptionService.GetAllAsync();
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var res = await _monthlySubscriptionService.GetByIdAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateMonthlySubscriptionDTO dto)
        {
            var res = await _monthlySubscriptionService.CreateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateMonthlySubscriptionDTO dto)
        {
            var res = await _monthlySubscriptionService.UpdateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var res = await _monthlySubscriptionService.DeleteAsync(id);
            return StatusCode(res.StatusCode, res);
        }
    }
}
