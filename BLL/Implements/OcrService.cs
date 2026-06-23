using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using BLL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BLL.Implements
{
    public class OcrService : IOcrService
    {
        private const string DefaultEndpoint = "https://api.ocr.space/parse/image";
        private const double TwoLinePlateMaxAspectRatio = 1.9;
        private const int MaxProcessedWidth = 2400;

        private static readonly Regex CompactPlateRegex = new(
            @"(?<province>[0-9]{2})(?<series>[A-Z]{1,2}[0-9]?)(?<serial>[0-9]{4,5})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PlatePrefixRegex = new(
            @"(?<province>[0-9]{2})[^A-Z0-9]*(?<series>[A-Z]{1,2})[^A-Z0-9]*(?<seriesNumber>[0-9]?)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly HttpClient _httpClient;
        private readonly ILogger<OcrService> _logger;
        private readonly string _apiKey;
        private readonly string _endpoint;

        public OcrService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<OcrService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["OcrSpace:ApiKey"]
                ?? Environment.GetEnvironmentVariable("OCR_SPACE_API_KEY")
                ?? "helloworld";
            _endpoint = configuration["OcrSpace:Endpoint"] ?? DefaultEndpoint;
        }

        public async Task<string?> RecognizeLicensePlateAsync(
            Stream imageStream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);
                var aspectRatio = (double)image.Width / image.Height;

                var fullImage = await EncodeForOcrAsync(image, cancellationToken);
                var fullText = await SendToOcrAsync(
                    fullImage,
                    BuildPartFileName(fileName, "full"),
                    cancellationToken);

                var plate = ExtractCompletePlate(fullText);
                if (!string.IsNullOrWhiteSpace(plate))
                {
                    _logger.LogInformation(
                        "OCR recognized license plate {LicensePlate} from {FileName}",
                        plate,
                        fileName);
                    return plate;
                }

                if (aspectRatio <= TwoLinePlateMaxAspectRatio)
                {
                    var twoLinePlate = await RecognizeTwoLinePlateAsync(image, fileName, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(twoLinePlate))
                    {
                        return twoLinePlate;
                    }
                }

                _logger.LogWarning(
                    "OCR returned text but no valid Vietnamese license plate was found. File: {FileName}, Text: {OcrText}",
                    fileName,
                    fullText);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnknownImageFormatException ex)
            {
                _logger.LogWarning(ex, "Uploaded file {FileName} is not a valid supported image", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not recognize a license plate from {FileName}", fileName);
            }

            return null;
        }

        private async Task<string?> RecognizeTwoLinePlateAsync(
            Image<Rgba32> image,
            string fileName,
            CancellationToken cancellationToken)
        {
            var topHeight = Math.Clamp(
                (int)Math.Round(image.Height * 0.53, MidpointRounding.AwayFromZero),
                1,
                image.Height);
            var bottomStart = Math.Clamp(
                (int)Math.Round(image.Height * 0.45, MidpointRounding.AwayFromZero),
                0,
                image.Height - 1);

            using var topImage = image.Clone(context =>
                context.Crop(new Rectangle(0, 0, image.Width, topHeight)));
            using var bottomImage = image.Clone(context =>
                context.Crop(new Rectangle(0, bottomStart, image.Width, image.Height - bottomStart)));

            var topBytes = await EncodeForOcrAsync(topImage, cancellationToken);
            var bottomBytes = await EncodeForOcrAsync(bottomImage, cancellationToken);

            var topTask = SendToOcrAsync(
                topBytes,
                BuildPartFileName(fileName, "top"),
                cancellationToken);
            var bottomTask = SendToOcrAsync(
                bottomBytes,
                BuildPartFileName(fileName, "bottom"),
                cancellationToken);

            await Task.WhenAll(topTask, bottomTask);

            var prefix = ExtractPlatePrefix(await topTask);
            var serial = ExtractPlateSerial(await bottomTask);

            if (prefix == null || serial == null)
            {
                _logger.LogWarning(
                    "Could not combine two-line plate. File: {FileName}, TopText: {TopText}, BottomText: {BottomText}",
                    fileName,
                    await topTask,
                    await bottomTask);
                return null;
            }

            var plate = $"{prefix}{FormatSerial(serial)}";
            _logger.LogInformation(
                "OCR recognized two-line license plate {LicensePlate} from {FileName}",
                plate,
                fileName);
            return plate;
        }

        private async Task<string?> SendToOcrAsync(
            byte[] imageBytes,
            string fileName,
            CancellationToken cancellationToken)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(_apiKey), "apikey");
            content.Add(new StringContent("auto"), "language");
            content.Add(new StringContent("false"), "isOverlayRequired");
            content.Add(new StringContent("true"), "detectOrientation");
            content.Add(new StringContent("true"), "scale");
            content.Add(new StringContent("3"), "OCREngine");

            using var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "file", fileName);

            using var response = await _httpClient.PostAsync(_endpoint, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OCR.Space returned HTTP {StatusCode}. Response: {ResponseBody}",
                    (int)response.StatusCode,
                    responseBody);
                return null;
            }

            OcrSpaceResponse? result;
            try
            {
                result = JsonSerializer.Deserialize<OcrSpaceResponse>(
                    responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "OCR.Space returned invalid JSON: {ResponseBody}", responseBody);
                return null;
            }

            if (result?.IsErroredOnProcessing == true)
            {
                _logger.LogWarning(
                    "OCR.Space could not process the image. Error: {ErrorMessage}; Details: {ErrorDetails}",
                    GetJsonValue(result.ErrorMessage),
                    result.ErrorDetails);
                return null;
            }

            var parsedText = result?.ParsedResults?
                .Where(parsed => !string.IsNullOrWhiteSpace(parsed.ParsedText))
                .Select(parsed => parsed.ParsedText!.Trim())
                .ToArray();

            return parsedText is { Length: > 0 }
                ? string.Join(Environment.NewLine, parsedText)
                : null;
        }

        private static async Task<byte[]> EncodeForOcrAsync(
            Image<Rgba32> source,
            CancellationToken cancellationToken)
        {
            using var processed = source.Clone();
            var scale = Math.Min(3d, (double)MaxProcessedWidth / processed.Width);

            if (scale > 1.05)
            {
                var width = Math.Max(1, (int)Math.Round(processed.Width * scale));
                var height = Math.Max(1, (int)Math.Round(processed.Height * scale));
                processed.Mutate(context => context.Resize(width, height, KnownResamplers.Bicubic));
            }

            await using var output = new MemoryStream();
            await processed.SaveAsPngAsync(
                output,
                new PngEncoder
                {
                    CompressionLevel = PngCompressionLevel.BestCompression
                },
                cancellationToken);
            return output.ToArray();
        }

        private static string? ExtractCompletePlate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var compact = Regex.Replace(text.ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
            foreach (Match match in CompactPlateRegex.Matches(compact))
            {
                var province = match.Groups["province"].Value;
                var series = match.Groups["series"].Value;
                var serial = match.Groups["serial"].Value;

                if (IsValidProvince(province))
                {
                    return $"{province}-{series}{FormatSerial(serial)}";
                }
            }

            return null;
        }

        private static string? ExtractPlatePrefix(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var normalized = text.ToUpperInvariant();
            foreach (Match match in PlatePrefixRegex.Matches(normalized))
            {
                var province = match.Groups["province"].Value;
                if (!IsValidProvince(province))
                {
                    continue;
                }

                var series = match.Groups["series"].Value;
                var seriesNumber = match.Groups["seriesNumber"].Value;
                return $"{province}-{series}{seriesNumber}";
            }

            return null;
        }

        private static string? ExtractPlateSerial(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var digits = Regex.Replace(
                text.ToUpperInvariant()
                    .Replace('O', '0')
                    .Replace('Q', '0')
                    .Replace('I', '1')
                    .Replace('L', '1')
                    .Replace('S', '5')
                    .Replace('B', '8')
                    .Replace('Z', '2'),
                @"[^0-9]",
                string.Empty);

            var match = Regex.Match(digits, @"[0-9]{4,5}");
            return match.Success ? match.Value : null;
        }

        private static bool IsValidProvince(string province)
        {
            return int.TryParse(province, out var code) && code is >= 11 and <= 99;
        }

        private static string FormatSerial(string serial)
        {
            return serial.Length == 5
                ? $"{serial[..3]}.{serial[3..]}"
                : serial;
        }

        private static string BuildPartFileName(string originalFileName, string part)
        {
            var safeBaseName = Path.GetFileNameWithoutExtension(originalFileName);
            return $"{safeBaseName}-{part}.png";
        }

        private static string? GetJsonValue(JsonElement? element)
        {
            if (!element.HasValue)
            {
                return null;
            }

            return element.Value.ValueKind == JsonValueKind.String
                ? element.Value.GetString()
                : element.Value.ToString();
        }
    }

    public class OcrSpaceResponse
    {
        public List<OcrParsedResult>? ParsedResults { get; set; }
        public bool IsErroredOnProcessing { get; set; }
        public JsonElement? ErrorMessage { get; set; }
        public string? ErrorDetails { get; set; }
    }

    public class OcrParsedResult
    {
        public string? ParsedText { get; set; }
    }
}
