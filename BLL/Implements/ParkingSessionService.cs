using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.ParkingSession;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements
{
    public class ParkingSessionService : IParkingSessionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ParkingSessionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var sessions = await _unitOfWork.ParkingSessionRepo.GetAllSessionsWithDetailsAsync();

            return new ResponseDTO("Lấy danh sách phiên gửi xe thành công", 200, true, sessions.Select(MapToDTO).ToList());
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập SessionId", 400, false);

            var session = await _unitOfWork.ParkingSessionRepo.GetSessionDetailAsync(id);
            if (session == null) return new ResponseDTO("Không tìm thấy phiên gửi xe", 404, false);
            return new ResponseDTO("Lấy thông tin phiên gửi xe thành công", 200, true, MapToDTO(session));
        }

        public async Task<ResponseDTO> CreateAsync(CreateParkingSessionDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu tạo phiên gửi xe không hợp lệ", 400, false);

            var validation = await ValidateSessionAsync(dto.DriverUserId, dto.LicensePlateIn, null, dto.VehicleTypeId, dto.EntryGateId, null, dto.AssignedSlotId, dto.ActualSlotId, dto.Status ?? SessionStatus.Active.ToString());
            if (validation.Error != null) return validation.Error;

            var session = new ParkingSession
            {
                SessionId = Guid.NewGuid(),
                DriverUserId = dto.DriverUserId,
                LicensePlateIn = NormalizePlate(dto.LicensePlateIn),
                EntryImageUrl = string.IsNullOrWhiteSpace(dto.EntryImageUrl) ? null : dto.EntryImageUrl.Trim(),
                VehicleTypeId = dto.VehicleTypeId,
                EntryTime = dto.EntryTime ?? DateTime.UtcNow,
                EntryGateId = dto.EntryGateId,
                AssignedSlotId = dto.AssignedSlotId,
                ActualSlotId = dto.ActualSlotId,
                Status = validation.Status.ToString()
            };

            await _unitOfWork.ParkingSessionRepo.AddAsync(session);
            await _unitOfWork.SaveChangeAsync();

            return await GetByIdAsync(session.SessionId);
        }

        public async Task<ResponseDTO> UpdateAsync(UpdateParkingSessionDTO dto)
        {
            if (dto == null || dto.SessionId == Guid.Empty) return new ResponseDTO("Dữ liệu cập nhật phiên gửi xe không hợp lệ", 400, false);

            var session = await _unitOfWork.ParkingSessionRepo.GetByIdAsync(dto.SessionId);
            if (session == null) return new ResponseDTO("Không tìm thấy phiên gửi xe", 404, false);

            var validation = await ValidateSessionAsync(dto.DriverUserId, dto.LicensePlateIn, dto.LicensePlateOut, dto.VehicleTypeId, dto.EntryGateId, dto.ExitGateId, dto.AssignedSlotId, dto.ActualSlotId, dto.Status);
            if (validation.Error != null) return validation.Error;

            session.DriverUserId = dto.DriverUserId;
            session.LicensePlateIn = NormalizePlate(dto.LicensePlateIn);
            session.LicensePlateOut = string.IsNullOrWhiteSpace(dto.LicensePlateOut) ? null : NormalizePlate(dto.LicensePlateOut);
            session.EntryImageUrl = string.IsNullOrWhiteSpace(dto.EntryImageUrl) ? null : dto.EntryImageUrl.Trim();
            session.ExitImageUrl = string.IsNullOrWhiteSpace(dto.ExitImageUrl) ? null : dto.ExitImageUrl.Trim();
            session.VehicleTypeId = dto.VehicleTypeId;
            session.EntryTime = dto.EntryTime;
            session.ExitTime = dto.ExitTime;
            session.EntryGateId = dto.EntryGateId;
            session.ExitGateId = dto.ExitGateId;
            session.AssignedSlotId = dto.AssignedSlotId;
            session.ActualSlotId = dto.ActualSlotId;
            session.Status = validation.Status.ToString();

            await _unitOfWork.ParkingSessionRepo.UpdateAsync(session);
            await _unitOfWork.SaveChangeAsync();

            return await GetByIdAsync(session.SessionId);
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập SessionId", 400, false);

            var session = await _unitOfWork.ParkingSessionRepo.GetByIdAsync(id);
            if (session == null) return new ResponseDTO("Không tìm thấy phiên gửi xe", 404, false);

            try
            {
                _unitOfWork.ParkingSessionRepo.Delete(session);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Xóa phiên gửi xe thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi xóa phiên gửi xe: {ex.Message}", 500, false);
            }
        }

        private async Task<(SessionStatus Status, ResponseDTO? Error)> ValidateSessionAsync(
            Guid? driverUserId,
            string? licensePlateIn,
            string? licensePlateOut,
            Guid vehicleTypeId,
            Guid entryGateId,
            Guid? exitGateId,
            Guid? assignedSlotId,
            Guid? actualSlotId,
            string? status)
        {
            if (string.IsNullOrWhiteSpace(licensePlateIn)) return (default, new ResponseDTO("Vui lòng nhập biển số vào", 400, false));
            if (vehicleTypeId == Guid.Empty) return (default, new ResponseDTO("Vui lòng chọn loại phương tiện", 400, false));
            if (entryGateId == Guid.Empty) return (default, new ResponseDTO("Vui lòng chọn cổng vào", 400, false));

            if (string.IsNullOrWhiteSpace(status) || !Enum.TryParse<SessionStatus>(status.Trim(), true, out var parsedStatus))
            {
                return (default, new ResponseDTO("Trạng thái phiên gửi xe chỉ được là Active, Completed hoặc Cancelled", 400, false));
            }

            var vehicleType = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(vehicleTypeId);
            if (vehicleType == null) return (default, new ResponseDTO("Loại phương tiện không tồn tại", 400, false));

            var entryGate = await _unitOfWork.GateRepo.GetByIdAsync(entryGateId);
            if (entryGate == null) return (default, new ResponseDTO("Cổng vào không tồn tại", 400, false));

            if (exitGateId.HasValue)
            {
                var exitGate = await _unitOfWork.GateRepo.GetByIdAsync(exitGateId.Value);
                if (exitGate == null) return (default, new ResponseDTO("Cổng ra không tồn tại", 400, false));
            }

            if (driverUserId.HasValue)
            {
                var user = await _unitOfWork.UserRepo.GetByIdAsync(driverUserId.Value);
                if (user == null) return (default, new ResponseDTO("Người lái không tồn tại", 400, false));
            }

            if (assignedSlotId.HasValue)
            {
                var slot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(assignedSlotId.Value);
                if (slot == null) return (default, new ResponseDTO("Vị trí đỗ được gán không tồn tại", 400, false));
            }

            if (actualSlotId.HasValue)
            {
                var slot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(actualSlotId.Value);
                if (slot == null) return (default, new ResponseDTO("Vị trí đỗ thực tế không tồn tại", 400, false));
            }

            if (!string.IsNullOrWhiteSpace(licensePlateOut) && NormalizePlate(licensePlateOut) != NormalizePlate(licensePlateIn))
            {
                return (default, new ResponseDTO("Biển số ra không khớp biển số vào", 400, false));
            }

            return (parsedStatus, null);
        }

        private static string NormalizePlate(string plate)
        {
            return plate.Trim().ToUpper();
        }

        internal static ParkingSessionDTO MapToDTO(ParkingSession session)
        {
            return new ParkingSessionDTO
            {
                SessionId = session.SessionId,
                DriverUserId = session.DriverUserId,
                DriverFullName = session.DriverUser?.FullName,
                LicensePlateIn = session.LicensePlateIn,
                LicensePlateOut = session.LicensePlateOut,
                EntryImageUrl = session.EntryImageUrl,
                ExitImageUrl = session.ExitImageUrl,
                VehicleTypeId = session.VehicleTypeId,
                VehicleTypeName = session.VehicleType?.TypeName,
                EntryTime = session.EntryTime,
                ExitTime = session.ExitTime,
                EntryGateId = session.EntryGateId,
                EntryGateName = session.EntryGate?.GateName,
                ExitGateId = session.ExitGateId,
                ExitGateName = session.ExitGate?.GateName,
                AssignedSlotId = session.AssignedSlotId,
                AssignedSlotCode = session.AssignedSlot?.SlotCode,
                ActualSlotId = session.ActualSlotId,
                ActualSlotCode = session.ActualSlot?.SlotCode,
                Status = session.Status
            };
        }
    }
}
