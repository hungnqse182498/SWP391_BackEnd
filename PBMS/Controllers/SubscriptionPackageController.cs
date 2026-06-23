using BLL.Interfaces;
using Common.DTOs.Subscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PBMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionPackageController : ControllerBase
    {
        private readonly ISubscriptionPackageService _subscriptionPackageService;

        public SubscriptionPackageController(ISubscriptionPackageService subscriptionPackageService)
        {
            _subscriptionPackageService = subscriptionPackageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var res = await _subscriptionPackageService.GetAllPackagesAsync();
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var res = await _subscriptionPackageService.GetPackageByIdAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [Authorize(Roles = "Manager")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSubscriptionPackageDTO dto)
        {
            var res = await _subscriptionPackageService.CreatePackageAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [Authorize(Roles = "Manager")]
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubscriptionPackageDTO dto)
        {
            var res = await _subscriptionPackageService.UpdatePackageAsync(id, dto);
            return StatusCode(res.StatusCode, res);
        }

        [Authorize(Roles = "Manager")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var res = await _subscriptionPackageService.DeletePackageAsync(id);
            return StatusCode(res.StatusCode, res);
        }
    }
}
