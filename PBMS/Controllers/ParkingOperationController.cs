using BLL.Interfaces;
using Common.DTOs.ParkingOperation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace PBMS.Controllers
{
    [ApiController]
    [Authorize(Roles = "Staff, Manager")]
    [Route("api/[controller]")]
    public class ParkingOperationController : ControllerBase
    {
        private readonly IParkingOperationService _parkingOperationService;
        private readonly IOcrService _ocrService;
        private readonly IWebHostEnvironment _env;

        public ParkingOperationController(
            IParkingOperationService parkingOperationService,
            IOcrService ocrService,
            IWebHostEnvironment env)
        {
            _parkingOperationService = parkingOperationService;
            _ocrService = ocrService;
            _env = env;
        }

        [HttpPost("upload-and-recognize-plate")]
        public async Task<IActionResult> UploadAndRecognizePlate(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn ảnh biển số xe để upload" });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
            {
                return BadRequest(new { message = "Chỉ cho phép upload file ảnh (.jpg, .jpeg, .png, .gif)" });
            }

            // Create upload folder if not exists
            var uploadDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            // Generate unique filename to avoid collision
            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadDir, fileName);

            // Save file to disk
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Build static public URL
            var request = HttpContext.Request;
            var imageUrl = $"{request.Scheme}://{request.Host}/uploads/{fileName}";

            // Run OCR recognition
            string? licensePlate;
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                licensePlate = await _ocrService.RecognizeLicensePlateAsync(
                    stream,
                    file.FileName,
                    HttpContext.RequestAborted);
            }

            if (string.IsNullOrWhiteSpace(licensePlate))
            {
                return UnprocessableEntity(new
                {
                    imageUrl,
                    licensePlate = (string?)null,
                    message = "Không thể nhận diện biển số từ ảnh. Vui lòng chụp rõ và sát biển số hơn rồi thử lại."
                });
            }

            return Ok(new
            {
                imageUrl,
                licensePlate
            });
        }

        [HttpPost("guest/check-in")]
        public async Task<IActionResult> GuestCheckIn([FromBody] GuestCheckInDTO dto)
        {
            var res = await _parkingOperationService.GuestCheckInAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("guest/check-out")]
        public async Task<IActionResult> GuestCheckOut([FromBody] GuestCheckOutDTO dto)
        {
            var res = await _parkingOperationService.GuestCheckOutAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("resident/check-in")]
        public async Task<IActionResult> ResidentCheckIn([FromBody] ResidentCheckInDTO dto)
        {
            var res = await _parkingOperationService.ResidentCheckInAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("resident/check-out")]
        public async Task<IActionResult> ResidentCheckOut([FromBody] ResidentCheckOutDTO dto)
        {
            var res = await _parkingOperationService.ResidentCheckOutAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("reservation/check-in")]
        public async Task<IActionResult> ReservationCheckIn([FromBody] ReservationCheckInDTO dto)
        {
            var res = await _parkingOperationService.ReservationCheckInAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("availability")]
        public async Task<IActionResult> GetAvailability([FromQuery] Guid? vehicleTypeId, [FromQuery] string? floorKeyword)
        {
            var res = await _parkingOperationService.GetAvailabilityAsync(vehicleTypeId, floorKeyword);
            return StatusCode(res.StatusCode, res);
        }
    }
}
