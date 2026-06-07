using BLL.Interfaces;
using Common.DTOs.IncidentReport;
using Microsoft.AspNetCore.Mvc;

namespace PBMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IncidentReportController : ControllerBase
    {
        private readonly IIncidentReportService _incidentReportService;

        public IncidentReportController(IIncidentReportService incidentReportService)
        {
            _incidentReportService = incidentReportService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var res = await _incidentReportService.GetAllAsync();
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var res = await _incidentReportService.GetByIdAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateIncidentReportDTO dto)
        {
            var res = await _incidentReportService.CreateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateIncidentReportDTO dto)
        {
            var res = await _incidentReportService.UpdateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var res = await _incidentReportService.DeleteAsync(id);
            return StatusCode(res.StatusCode, res);
        }
    }
}
