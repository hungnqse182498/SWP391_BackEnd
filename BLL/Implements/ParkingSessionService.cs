using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.ParkingSession;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements
{
    public class ParkingSessionService : IParkingSessionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Active",
            "Completed",
            "Cancelled"
        };

        public ParkingSessionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var sessions = await QueryWithIncludes()
                .OrderByDescending(s => s.EntryTime)
                .ToListAsync();

            return new ResponseDTO("Lấy danh sách phiên gửi xe thành công", 200, true, sessions.Select(MapToDTO).ToList());
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập SessionId", 400, false);

            var session = await QueryWithIncludes().FirstOrDefaultAsync(s => s.SessionId == id);
            if (session == null) return new ResponseDTO("Không tìm thấy phiên gửi xe", 404, false);
            return new ResponseDTO("Lấy thông tin phiên gửi xe thành công", 200, true, MapToDTO(session));
        }

        public async Task<ResponseDTO> CreateAsync(CreateParkingSessionDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu tạo phiên gửi xe không hợp lệ", 400, false);

            var validation = await ValidateSessionAsync(dto.DriverUserId, dto.LicensePlateIn, null, dto.VehicleTypeId, dto.EntryGateId, null, dto.AssignedSlotId, dto.ActualSlotId, dto.Status ?? "Active");
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
                Status = validation.Status!
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
            session.Status = validation.Status!;

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

        private IQueryable<ParkingSession> QueryWithIncludes()
        {
            return _unitOfWork.ParkingSessionRepo.GetAll()
                .Include(s => s.DriverUser)
                .Include(s => s.VehicleType)
                .Include(s => s.EntryGate)
                .Include(s => s.ExitGate)
                .Include(s => s.AssignedSlot)
                .Include(s => s.ActualSlot);
        }

        private async Task<(string? Status, ResponseDTO? Error)> ValidateSessionAsync(
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
            if (string.IsNullOrWhiteSpace(licensePlateIn)) return (null, new ResponseDTO("Vui lòng nhập biển số vào", 400, false));
            if (vehicleTypeId == Guid.Empty) return (null, new ResponseDTO("Vui lòng chọn loại phương tiện", 400, false));
            if (entryGateId == Guid.Empty) return (null, new ResponseDTO("Vui lòng chọn cổng vào", 400, false));

            var normalizedStatus = NormalizeStatus(status);
            if (normalizedStatus == null) return (null, new ResponseDTO("Trạng thái phiên gửi xe chỉ được là Active, Completed hoặc Cancelled", 400, false));

            var vehicleType = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(vehicleTypeId);
            if (vehicleType == null) return (null, new ResponseDTO("Loại phương tiện không tồn tại", 400, false));

            var entryGate = await _unitOfWork.GateRepo.GetByIdAsync(entryGateId);
            if (entryGate == null) return (null, new ResponseDTO("Cổng vào không tồn tại", 400, false));

            if (exitGateId.HasValue)
            {
                var exitGate = await _unitOfWork.GateRepo.GetByIdAsync(exitGateId.Value);
                if (exitGate == null) return (null, new ResponseDTO("Cổng ra không tồn tại", 400, false));
            }


            if (driverUserId.HasValue && !await _unitOfWork.UserRepo.AnyAsync(u => u.UserId == driverUserId.Value))
            {
                return (null, new ResponseDTO("Người lái không tồn tại", 400, false));
            }

            if (assignedSlotId.HasValue && !await _unitOfWork.ParkingSlotRepo.AnyAsync(s => s.SlotId == assignedSlotId.Value))
            {
                return (null, new ResponseDTO("Vị trí đỗ được gán không tồn tại", 400, false));
            }

            if (actualSlotId.HasValue && !await _unitOfWork.ParkingSlotRepo.AnyAsync(s => s.SlotId == actualSlotId.Value))
            {
                return (null, new ResponseDTO("Vị trí đỗ thực tế không tồn tại", 400, false));
            }

            if (!string.IsNullOrWhiteSpace(licensePlateOut) && NormalizePlate(licensePlateOut) != NormalizePlate(licensePlateIn))
            {
                return (null, new ResponseDTO("Biển số ra không khớp biển số vào", 400, false));
            }

            return (normalizedStatus, null);
        }

        private static string? NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            return ValidStatuses.FirstOrDefault(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));
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
