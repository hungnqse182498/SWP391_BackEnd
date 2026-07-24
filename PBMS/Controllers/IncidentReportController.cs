using BLL.Interfaces;
using Common.DTOs.IncidentReport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBMS.Extensions;
using System.Security.Claims;
using System.IO;

namespace PBMS.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class IncidentReportController : ControllerBase
    {
        private readonly IIncidentReportService _incidentReportService;
        private readonly IWebHostEnvironment _environment;
        private static readonly string[] AllowedProofExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        public IncidentReportController(IIncidentReportService incidentReportService, IWebHostEnvironment environment)
        {
            _incidentReportService = incidentReportService;
            _environment = environment;
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
            dto.ReportedByUserId = User.GetUserId();
            dto.Status = "Open";
            dto.HandledByStaffId = null;
            var res = await _incidentReportService.CreateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("upload-proof")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadProof(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Vui lòng chọn ảnh minh chứng" });
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "Ảnh minh chứng không được vượt quá 5 MB" });

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedProofExtensions.Contains(extension))
                return BadRequest(new { message = "Chỉ hỗ trợ ảnh JPG, PNG hoặc WEBP" });

            var root = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var directory = Path.Combine(root, "uploads", "incidents");
            Directory.CreateDirectory(directory);
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(directory, fileName);
            await using (var stream = new FileStream(filePath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream, HttpContext.RequestAborted);
            }

            var request = HttpContext.Request;
            return Ok(new { imageUrl = $"{request.Scheme}://{request.Host}/uploads/incidents/{fileName}" });
        }

        [HttpPut]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Update([FromBody] UpdateIncidentReportDTO dto)
        {
            var res = await _incidentReportService.UpdateAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPut("{id:guid}/assign/{staffId:guid}")]
        [Authorize(Roles = "Staff,Manager")]
        public async Task<IActionResult> AssignStaff(Guid id, Guid staffId)
        {
            if (User.IsInRole("Staff")) staffId = User.GetUserId();
            var response = await _incidentReportService.AssignToStaffAsync(id, staffId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id:guid}/resolve/{staffId:guid}")]
        [Authorize(Roles = "Staff,Manager")]
        public async Task<IActionResult> ResolveIncident(Guid id, Guid staffId, [FromBody] ResolveIncidentDTO dto)
        {
            if (User.IsInRole("Staff")) staffId = User.GetUserId();
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
