using BLL.Interfaces;
using Common.DTOs.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PBMS.Controllers
{
    [ApiController]
    [Authorize(Roles = "Manager,manager,Admin,admin")]
    [Route("api/reports")]
    public class ReportController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("types")]
        public async Task<IActionResult> GetReportTypes()
        {
            var res = await _reportService.GetReportTypesAsync();
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] ReportFilterDTO filter)
        {
            var res = await _reportService.GetSummaryAsync(filter);
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("revenue")]
        public async Task<IActionResult> GetRevenue([FromQuery] ReportFilterDTO filter)
        {
            var res = await _reportService.GetRevenueAsync(filter);
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("operations")]
        public async Task<IActionResult> GetParkingOperations([FromQuery] ReportFilterDTO filter)
        {
            var res = await _reportService.GetParkingOperationsAsync(filter);
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] ReportExportRequestDTO request)
        {
            var res = await _reportService.ExportAsync(request);
            if (!res.IsSuccess || res.Result is not ReportExportFileDTO file)
            {
                return StatusCode(res.StatusCode, res);
            }

            return File(file.Content, file.ContentType, file.FileName);
        }
    }
}
