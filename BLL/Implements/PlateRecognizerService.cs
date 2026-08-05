using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BLL.Interfaces;
using Common.DTOs.ParkingOperation;
using Common.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BLL.Implements
{
    public class PlateRecognizerService : IPlateRecognitionService
    {
        private const string DefaultEndpoint = "https://api.platerecognizer.com/v1/plate-reader/";
        private const string DefaultRegions = "vn";
        private const decimal DefaultMinimumConfidence = 0.75m;

        private readonly HttpClient _httpClient;
        private readonly ILogger<PlateRecognizerService> _logger;
        private readonly string _apiKey;
        private readonly string _endpoint;
        private readonly string[] _regions;
        private readonly decimal _minimumConfidence;

        public PlateRecognizerService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<PlateRecognizerService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["PlateRecognizer:ApiKey"]
                ?? Environment.GetEnvironmentVariable("PLATE_RECOGNIZER_API_KEY")
                ?? string.Empty;
            _endpoint = configuration["PlateRecognizer:Endpoint"] ?? DefaultEndpoint;
            _regions = (configuration["PlateRecognizer:Regions"] ?? DefaultRegions)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (!decimal.TryParse(configuration["PlateRecognizer:MinimumConfidence"], out _minimumConfidence))
            {
                _minimumConfidence = DefaultMinimumConfidence;
            }
        }

        public async Task<PlateRecognitionResultDTO> RecognizeLicensePlateAsync(
            Stream imageStream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            if (imageStream == null || !imageStream.CanRead)
            {
                return new PlateRecognitionResultDTO
                {
                    Message = "File ảnh biển số không hợp lệ"
                };
            }

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return new PlateRecognitionResultDTO
                {
                    Message = "Chưa cấu hình Plate Recognizer API key"
                };
            }

            try
            {
                using var content = new MultipartFormDataContent();
                using var imageContent = new StreamContent(imageStream);
                imageContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(fileName));
                content.Add(imageContent, "upload", Path.GetFileName(fileName));

                foreach (var region in _regions)
                {
                    content.Add(new StringContent(region), "regions");
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
                {
                    Content = content
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Token", _apiKey);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Plate Recognizer returned HTTP {StatusCode}. Response: {ResponseBody}",
                        (int)response.StatusCode,
                        responseBody);

                    return new PlateRecognitionResultDTO
                    {
                        ProviderStatusCode = (int)response.StatusCode,
                        ProviderError = ExtractProviderError(responseBody),
                        ProviderResponse = responseBody,
                        Message = CreateProviderErrorMessage(response.StatusCode, responseBody)
                    };
                }

                PlateRecognizerResponse? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<PlateRecognizerResponse>(
                        responseBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Plate Recognizer returned invalid JSON: {ResponseBody}", responseBody);
                    return new PlateRecognitionResultDTO
                    {
                        ProviderStatusCode = (int)response.StatusCode,
                        ProviderError = "Invalid JSON response",
                        ProviderResponse = responseBody,
                        Message = "Plate Recognizer trả về dữ liệu không hợp lệ"
                    };
                }

                var candidates = parsed?.Results?
                    .SelectMany(CreateCandidates)
                    .Where(candidate => !string.IsNullOrWhiteSpace(candidate.LicensePlate))
                    .GroupBy(candidate => candidate.LicensePlate)
                    .Select(group => group.OrderByDescending(candidate => candidate.Confidence).First())
                    .OrderByDescending(candidate => candidate.Confidence)
                    .ToList() ?? new List<PlateRecognitionCandidateDTO>();

                var best = candidates.FirstOrDefault(candidate =>
                    candidate.Confidence >= _minimumConfidence &&
                    LicensePlateNormalizer.IsValid(candidate.LicensePlate));

                if (best == null)
                {
                    var message = candidates.Count == 0
                        ? "Plate Recognizer không tìm thấy biển số trong ảnh. Vui lòng chụp rõ và sát biển số hơn rồi thử lại."
                        : $"Plate Recognizer chưa đủ tự tin để dùng kết quả. Kết quả tốt nhất: {candidates[0].LicensePlate} ({FormatPercent(candidates[0].Confidence)}), thấp hơn ngưỡng {FormatPercent(_minimumConfidence)}.";

                    return new PlateRecognitionResultDTO
                    {
                        ProviderStatusCode = (int)response.StatusCode,
                        ProviderResponse = responseBody,
                        MinimumConfidence = _minimumConfidence,
                        Candidates = candidates,
                        Message = message
                    };
                }

                _logger.LogInformation(
                    "Plate Recognizer detected license plate {LicensePlate} with confidence {Confidence}",
                    best.LicensePlate,
                    best.Confidence);

                return new PlateRecognitionResultDTO
                {
                    LicensePlate = best.LicensePlate,
                    Confidence = best.Confidence,
                    RegionCode = best.RegionCode,
                    ProviderStatusCode = (int)response.StatusCode,
                    ProviderResponse = responseBody,
                    MinimumConfidence = _minimumConfidence,
                    Message = $"Nhận diện biển số thành công: {best.LicensePlate} ({FormatPercent(best.Confidence)})",
                    Candidates = candidates
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not recognize a license plate from {FileName}", fileName);
                return new PlateRecognitionResultDTO
                {
                    ProviderError = ex.Message,
                    Message = $"Không thể kết nối Plate Recognizer để nhận diện biển số: {ex.Message}"
                };
            }
        }

        private static IEnumerable<PlateRecognitionCandidateDTO> CreateCandidates(PlateRecognizerResult result)
        {
            var primaryPlate = LicensePlateNormalizer.Normalize(result.Plate);
            if (!string.IsNullOrWhiteSpace(primaryPlate))
            {
                yield return new PlateRecognitionCandidateDTO
                {
                    LicensePlate = primaryPlate,
                    Confidence = result.Score,
                    RegionCode = result.Region?.Code
                };
            }

            if (result.Candidates == null) yield break;

            foreach (var candidate in result.Candidates)
            {
                var plate = LicensePlateNormalizer.Normalize(candidate.Plate);
                if (string.IsNullOrWhiteSpace(plate)) continue;

                yield return new PlateRecognitionCandidateDTO
                {
                    LicensePlate = plate,
                    Confidence = candidate.Score,
                    RegionCode = result.Region?.Code
                };
            }
        }

        private static string GetContentType(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
        }

        private static string CreateProviderErrorMessage(HttpStatusCode statusCode, string responseBody)
        {
            var providerError = ExtractProviderError(responseBody);
            if (!string.IsNullOrWhiteSpace(providerError))
            {
                return $"Plate Recognizer trả lỗi HTTP {(int)statusCode}: {providerError}";
            }

            return statusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    "Plate Recognizer API key không hợp lệ hoặc không đủ quyền",
                (HttpStatusCode)429 =>
                    "Plate Recognizer đã hết quota hoặc đang bị giới hạn lượt gọi",
                _ => $"Plate Recognizer không xử lý được ảnh biển số (HTTP {(int)statusCode})"
            };
        }

        private static string? ExtractProviderError(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody)) return null;

            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;

                foreach (var property in new[] { "detail", "message", "error", "errors" })
                {
                    if (root.TryGetProperty(property, out var value))
                    {
                        return value.ValueKind == JsonValueKind.String
                            ? value.GetString()
                            : value.ToString();
                    }
                }
            }
            catch (JsonException)
            {
                return responseBody;
            }

            return null;
        }

        private static string FormatPercent(decimal value)
        {
            return $"{Math.Round(value * 100, 1)}%";
        }

        private class PlateRecognizerResponse
        {
            public List<PlateRecognizerResult>? Results { get; set; }
        }

        private class PlateRecognizerResult
        {
            public string? Plate { get; set; }
            public decimal Score { get; set; }
            public PlateRecognizerRegion? Region { get; set; }
            public List<PlateRecognizerCandidate>? Candidates { get; set; }
        }

        private class PlateRecognizerCandidate
        {
            public string? Plate { get; set; }
            public decimal Score { get; set; }
        }

        private class PlateRecognizerRegion
        {
            public string? Code { get; set; }
        }
    }
}
