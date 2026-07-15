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
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif" };

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
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAndRecognizePlate(IFormFile file)
        {
            var upload = await SaveUploadedImageAsync(file, "Vui lòng chọn ảnh biển số xe để upload");
            if (upload.Error != null) return upload.Error;

            // Run OCR recognition
            string? licensePlate;
            using (var stream = new FileStream(upload.FilePath!, FileMode.Open, FileAccess.Read))
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
                    imageUrl = upload.ImageUrl,
                    licensePlate = (string?)null,
                    message = "Không thể nhận diện biển số từ ảnh. Vui lòng chụp rõ và sát biển số hơn rồi thử lại."
                });
            }

            return Ok(new
            {
                imageUrl = upload.ImageUrl,
                licensePlate
            });
        }

        [HttpPost("upload-and-decode-qr")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAndDecodeQr(IFormFile file)
        {
            var upload = await SaveUploadedImageAsync(file, "Vui lòng chọn ảnh QR để upload");
            if (upload.Error != null) return upload.Error;

            using var stream = new FileStream(upload.FilePath!, FileMode.Open, FileAccess.Read);
            var res = await _parkingOperationService.DecodeQrImageAsync(
                stream,
                file.FileName,
                upload.ImageUrl,
                HttpContext.RequestAborted);

            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("check-in")]
        public async Task<IActionResult> CheckIn([FromBody] ParkingCheckInDTO dto)
        {
            var res = await _parkingOperationService.CheckInAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("check-out")]
        public async Task<IActionResult> CheckOut([FromBody] ParkingCheckOutDTO dto)
        {
            var res = await _parkingOperationService.CheckOutAsync(dto);
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("availability")]
        public async Task<IActionResult> GetAvailability([FromQuery] Guid? vehicleTypeId, [FromQuery] string? floorKeyword)
        {
            var res = await _parkingOperationService.GetAvailabilityAsync(vehicleTypeId, floorKeyword);
            return StatusCode(res.StatusCode, res);
        }

        private async Task<(string? ImageUrl, string? FilePath, IActionResult? Error)> SaveUploadedImageAsync(
            IFormFile file,
            string emptyMessage)
        {
            if (file == null || file.Length == 0)
            {
                return (null, null, BadRequest(new { message = emptyMessage }));
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !AllowedImageExtensions.Contains(ext))
            {
                return (null, null, BadRequest(new { message = "Chỉ cho phép upload file ảnh (.jpg, .jpeg, .png, .gif)" }));
            }

            var uploadRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadDir = Path.Combine(uploadRoot, "uploads");
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var request = HttpContext.Request;
            var imageUrl = $"{request.Scheme}://{request.Host}/uploads/{fileName}";
            return (imageUrl, filePath, null);
        }
    }
}
