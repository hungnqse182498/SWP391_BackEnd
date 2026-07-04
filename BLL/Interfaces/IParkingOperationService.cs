using Common.DTOs;
using Common.DTOs.ParkingOperation;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IParkingOperationService
    {
        Task<ResponseDTO> GuestCheckInAsync(GuestCheckInDTO dto);
        Task<ResponseDTO> GuestCheckOutPreviewAsync(GuestCheckOutPreviewDTO dto);
        Task<ResponseDTO> GuestCheckOutAsync(GuestCheckOutDTO dto);
        Task<ResponseDTO> ResidentCheckInAsync(ResidentCheckInDTO dto);
        Task<ResponseDTO> ResidentCheckOutAsync(ResidentCheckOutDTO dto);
        Task<ResponseDTO> ReservationCheckInAsync(ReservationCheckInDTO dto);
        Task<ResponseDTO> GetAvailabilityAsync(Guid? vehicleTypeId, string? floorKeyword);
    }
}
