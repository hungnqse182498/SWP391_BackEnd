using BLL.Interfaces;
using Common.DTOs.IncidentReport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBMS.Extensions;
using System.Security.Claims;

namespace PBMS.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class IncidentReportController : ControllerBase
    {
        private readonly IIncidentReportService _incidentReportService;

        public IncidentReportController(IIncidentReportService incidentReportService)
        {
            _incidentReportService = incidentReportService;
        }

        [HttpGet]
        [Authorize(Roles = "Manager, Staff")]
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

        [HttpGet("my-reports")]
        public async Task<IActionResult> GetMyIncidents()
        {
            var userId = User.GetUserId();

            if (userId == Guid.Empty)
            {
                return Unauthorized(new { message = "Không thể xác thực danh tính từ Token" });
            }

            var res = await _incidentReportService.GetByUserIdAsync(userId);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateIncidentReportDTO dto)
        {
            var res = await _incidentReportService.CreateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Update([FromBody] UpdateIncidentReportDTO dto)
        {
            var res = await _incidentReportService.UpdateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut("{id:guid}/assign/{staffId:guid}")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> AssignStaff(Guid id, Guid staffId)
        {
            var response = await _incidentReportService.AssignToStaffAsync(id, staffId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id:guid}/resolve/{staffId:guid}")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> ResolveIncident(Guid id, Guid staffId, [FromBody] ResolveIncidentDTO dto)
        {
            var response = await _incidentReportService.ResolveAsync(id, staffId, dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var res = await _incidentReportService.DeleteAsync(id);
            return StatusCode(res.StatusCode, res);
        }
    }
}
