using Common.DTOs;
using Common.DTOs.Subscription;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IVehicleChangeRequestService
    {
        Task<ResponseDTO> CreateVehicleChangeAsync(Guid userId, CreateVehicleChangeDTO dto);
        Task<ResponseDTO> GetVehicleChangeRequestsAsync();
        Task<ResponseDTO> GetVehicleChangeRequestByIdAsync(Guid id, Guid requesterId, bool canManage);
        Task<ResponseDTO> GetRequestsByUserIdAsync(Guid userId);
        Task<ResponseDTO> UpdateVehicleChangeRequestAsync(Guid id, Guid userId, UpdateVehicleChangeDTO dto);
        Task<ResponseDTO> ApproveVehicleChangeAsync(Guid id, Guid handlerId);
        Task<ResponseDTO> RejectVehicleChangeAsync(Guid id, Guid handlerId, RejectVehicleChangeDTO dto);
        Task<ResponseDTO> DeleteVehicleChangeRequestAsync(Guid id, Guid userId);
    }
}
