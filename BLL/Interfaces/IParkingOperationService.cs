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
        Task<ResponseDTO> DecodeQrImageAsync(Stream imageStream, string fileName, string? imageUrl = null, CancellationToken cancellationToken = default);
        Task<ResponseDTO> GetAvailabilityAsync(Guid? vehicleTypeId, string? floorKeyword);
    }
}
