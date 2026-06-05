using BLL.Interfaces;
using Common.DTOs.PricingPolicy;
using Microsoft.AspNetCore.Mvc;

namespace PBMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PricingPolicyController : ControllerBase
    {
        private readonly IPricingPolicyService _pricingPolicyService;

        public PricingPolicyController(IPricingPolicyService pricingPolicyService)
        {
            _pricingPolicyService = pricingPolicyService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var res = await _pricingPolicyService.GetAllAsync();
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var res = await _pricingPolicyService.GetByIdAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePricingPolicyDTO dto)
        {
            var res = await _pricingPolicyService.CreateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdatePricingPolicyDTO dto)
        {
            var res = await _pricingPolicyService.UpdateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var res = await _pricingPolicyService.DeleteAsync(id);
            return StatusCode(res.StatusCode, res);
        }
    }
}
