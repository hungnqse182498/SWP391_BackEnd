using Common.DTOs.ParkingOperation;

namespace BLL.Interfaces
{
    public interface IPlateRecognitionService
    {
        Task<PlateRecognitionResultDTO> RecognizeLicensePlateAsync(
            Stream imageStream,
            string fileName,
            CancellationToken cancellationToken = default);
    }
}
