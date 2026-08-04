using Common.DTOs;
using Common.DTOs.ParkingOperation;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IParkingOperationService
    {
        Task<ResponseDTO> CheckInAsync(ParkingCheckInDTO dto);
        Task<ResponseDTO> CheckOutAsync(ParkingCheckOutDTO dto);
        Task<ResponseDTO> GetCheckoutPaymentStatusAsync(Guid paymentId);
        Task<ResponseDTO> ConfirmCashCheckoutAsync(Guid paymentId);
        Task<ResponseDTO> CancelCheckoutAsync(Guid paymentId);
        Task<ResponseDTO> GetFeePreviewAsync(Guid sessionId);
        Task<ResponseDTO> GetMyFeePreviewAsync(Guid sessionId, Guid userId);
        Task<ResponseDTO> GetMyCheckoutPaymentAsync(Guid sessionId, Guid userId);
        Task<ResponseDTO> DecodeQrImageAsync(Stream imageStream, string fileName, string? imageUrl = null, CancellationToken cancellationToken = default);
        Task<ResponseDTO> ResolveQrPayloadAsync(string? qrPayload);
        Task<ResponseDTO> GetAvailabilityAsync(Guid? vehicleTypeId, string? floorKeyword);
    }
}
