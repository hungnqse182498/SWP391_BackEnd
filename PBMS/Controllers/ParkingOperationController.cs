using BLL.Interfaces;
using Common.DTOs.ParkingOperation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PBMS.Controllers
{
    [ApiController]
    [Authorize(Roles = "Staff, Manager")]
    [Route("api/[controller]")]
    public class ParkingOperationController : ControllerBase
    {
        private readonly IParkingOperationService _parkingOperationService;
        private readonly IPlateRecognitionService _plateRecognitionService;
        private readonly IWebHostEnvironment _env;
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        public ParkingOperationController(
            IParkingOperationService parkingOperationService,
            IPlateRecognitionService plateRecognitionService,
            IWebHostEnvironment env)
        {
            _parkingOperationService = parkingOperationService;
            _plateRecognitionService = plateRecognitionService;
            _env = env;
        }

        [HttpPost("upload-and-recognize-plate")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAndRecognizePlate(IFormFile file)
        {
            var upload = await SaveUploadedImageAsync(file, "Vui long chon anh bien so xe de upload");
            if (upload.Error != null) return upload.Error;

            PlateRecognitionResultDTO recognition;
            using (var stream = new FileStream(upload.FilePath!, FileMode.Open, FileAccess.Read))
            {
                recognition = await _plateRecognitionService.RecognizeLicensePlateAsync(
                    stream,
                    file.FileName,
                    HttpContext.RequestAborted);
            }

            recognition.ImageUrl = upload.ImageUrl;

            if (string.IsNullOrWhiteSpace(recognition.LicensePlate))
            {
                return UnprocessableEntity(recognition);
            }

            return Ok(recognition);
        }

        [HttpPost("upload-and-decode-qr")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAndDecodeQr(IFormFile file)
        {
            var upload = await SaveUploadedImageAsync(file, "Vui long chon anh QR de upload");
            if (upload.Error != null) return upload.Error;

            using var stream = new FileStream(upload.FilePath!, FileMode.Open, FileAccess.Read);
            var res = await _parkingOperationService.DecodeQrImageAsync(
                stream,
                file.FileName,
                upload.ImageUrl,
                HttpContext.RequestAborted);

            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("resolve-qr-payload")]
        public async Task<IActionResult> ResolveQrPayload([FromBody] ResolveQrPayloadDTO dto)
        {
            var res = await _parkingOperationService.ResolveQrPayloadAsync(dto?.QrPayload);
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

        [HttpGet("check-out/payment/{paymentId:guid}")]
        public async Task<IActionResult> GetCheckoutPaymentStatus(Guid paymentId)
        {
            var res = await _parkingOperationService.GetCheckoutPaymentStatusAsync(paymentId);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("check-out/payment/{paymentId:guid}/confirm-cash")]
        public async Task<IActionResult> ConfirmCashCheckout(Guid paymentId)
        {
            var res = await _parkingOperationService.ConfirmCashCheckoutAsync(paymentId);
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("check-out/payment/{paymentId:guid}/cancel")]
        public async Task<IActionResult> CancelCheckout(Guid paymentId)
        {
            var res = await _parkingOperationService.CancelCheckoutAsync(paymentId);
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("fee-preview/{sessionId:guid}")]
        public async Task<IActionResult> GetFeePreview(Guid sessionId)
        {
            var res = await _parkingOperationService.GetFeePreviewAsync(sessionId);
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("availability")]
        [AllowAnonymous]
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
                return (null, null, BadRequest(new { message = "Chi cho phep upload file anh (.jpg, .jpeg, .png, .gif, .webp)" }));
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
