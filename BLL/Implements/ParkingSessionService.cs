using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.ParkingSession;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;

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
            var sessionDtos = sessions.Select(session =>
            {
                var dto = MapToDTO(session);
                if (string.Equals(session.Status, SessionStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    dto.Ticket = ParkingOperationService.CreateSessionTicket(session.SessionId);
                }

                return dto;
            }).ToList();

            return new ResponseDTO("Lấy danh sách phiên gửi xe thành công", 200, true, sessionDtos);
        }

        public async Task<ResponseDTO> GetMyAsync(Guid userId)
        {
            if (userId == Guid.Empty) return new ResponseDTO("Vui lòng đăng nhập để xem phiên gửi xe của bạn", 401, false);

            var sessions = await _unitOfWork.ParkingSessionRepo.GetSessionsByDriverUserIdWithDetailsAsync(userId);
            var sessionDtos = sessions.Select(session =>
            {
                var dto = MapToDTO(session);
                if (string.Equals(session.Status, SessionStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    dto.Ticket = ParkingOperationService.CreateSessionTicket(session.SessionId);
                }

                return dto;
            }).ToList();

            return new ResponseDTO("Lấy danh sách phiên gửi xe của tôi thành công", 200, true, sessionDtos);
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập SessionId", 400, false);

            var session = await _unitOfWork.ParkingSessionRepo.GetSessionDetailAsync(id);
            if (session == null) return new ResponseDTO("Không tìm thấy phiên gửi xe", 404, false);
            var dto = MapToDTO(session);
            dto.Ticket = ParkingOperationService.CreateSessionTicket(session.SessionId);
            return new ResponseDTO("Lấy thông tin phiên gửi xe thành công", 200, true, dto);
        }

        public async Task<ResponseDTO> CreateAsync(CreateParkingSessionDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu tạo phiên gửi xe không hợp lệ", 400, false);

            var entryTime = dto.EntryTime ?? DateTime.UtcNow;
            var validation = await ValidateSessionAsync(
                dto.DriverUserId,
                dto.LicensePlateIn,
                null,
                dto.VehicleTypeId,
                entryTime,
                null,
                dto.EntryGateId,
                null,
                dto.AssignedSlotId,
                dto.ActualSlotId,
                dto.Status ?? SessionStatus.Active.ToString(),
                null);
            if (validation.Error != null) return validation.Error;

            var session = new ParkingSession
            {
                SessionId = Guid.NewGuid(),
                DriverUserId = dto.DriverUserId,
                LicensePlateIn = NormalizePlate(dto.LicensePlateIn),
                EntryImageUrl = string.IsNullOrWhiteSpace(dto.EntryImageUrl) ? null : dto.EntryImageUrl.Trim(),
                VehicleTypeId = dto.VehicleTypeId,
                EntryTime = entryTime,
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

            var validation = await ValidateSessionAsync(
                dto.DriverUserId,
                dto.LicensePlateIn,
                dto.LicensePlateOut,
                dto.VehicleTypeId,
                dto.EntryTime,
                dto.ExitTime,
                dto.EntryGateId,
                dto.ExitGateId,
                dto.AssignedSlotId,
                dto.ActualSlotId,
                dto.Status,
                dto.SessionId);
            if (validation.Error != null) return validation.Error;

            session.DriverUserId = dto.DriverUserId;
            session.LicensePlateIn = NormalizePlate(dto.LicensePlateIn);
            session.LicensePlateOut = string.IsNullOrWhiteSpace(dto.LicensePlateOut)
                ? (validation.Status == SessionStatus.Completed ? NormalizePlate(dto.LicensePlateIn) : null)
                : NormalizePlate(dto.LicensePlateOut);
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
            DateTime entryTime,
            DateTime? exitTime,
            Guid entryGateId,
            Guid? exitGateId,
            Guid? assignedSlotId,
            Guid? actualSlotId,
            string? status,
            Guid? currentSessionId)
        {
            if (string.IsNullOrWhiteSpace(licensePlateIn)) return (default, new ResponseDTO("Vui lòng nhập biển số vào", 400, false));
            if (vehicleTypeId == Guid.Empty) return (default, new ResponseDTO("Vui lòng chọn loại phương tiện", 400, false));
            if (entryGateId == Guid.Empty) return (default, new ResponseDTO("Vui lòng chọn cổng vào", 400, false));

            if (entryTime == default) return (default, new ResponseDTO("Vui lòng nhập thời gian vào", 400, false));

            if (string.IsNullOrWhiteSpace(status) || !Enum.TryParse<SessionStatus>(status.Trim(), true, out var parsedStatus))
            {
                return (default, new ResponseDTO("Trạng thái phiên gửi xe chỉ được là Active, Completed hoặc Exception", 400, false));
            }

            if (exitTime.HasValue && exitTime.Value < entryTime)
            {
                return (default, new ResponseDTO("Thời gian ra không được nhỏ hơn thời gian vào", 400, false));
            }

            if (parsedStatus == SessionStatus.Completed && (!exitTime.HasValue || !exitGateId.HasValue || exitGateId.Value == Guid.Empty))
            {
                return (default, new ResponseDTO("Phiên Completed cần có thời gian ra và cổng ra", 400, false));
            }

            var vehicleType = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(vehicleTypeId);
            if (vehicleType == null) return (default, new ResponseDTO("Loại phương tiện không tồn tại", 400, false));

            var entryGate = await _unitOfWork.GateRepo.GetByIdAsync(entryGateId);
            if (entryGate == null) return (default, new ResponseDTO("Cổng vào không tồn tại", 400, false));
            if (!IsGateType(entryGate, "Entry")) return (default, new ResponseDTO("Cổng vào phải là loại Entry", 400, false));

            if (exitGateId.HasValue)
            {
                if (exitGateId.Value == Guid.Empty) return (default, new ResponseDTO("ExitGateId không hợp lệ", 400, false));

                var exitGate = await _unitOfWork.GateRepo.GetByIdAsync(exitGateId.Value);
                if (exitGate == null) return (default, new ResponseDTO("Cổng ra không tồn tại", 400, false));
                if (!IsGateType(exitGate, "Exit")) return (default, new ResponseDTO("Cổng ra phải là loại Exit", 400, false));
            }

            if (driverUserId.HasValue)
            {
                if (driverUserId.Value == Guid.Empty) return (default, new ResponseDTO("DriverUserId không hợp lệ", 400, false));

                var user = await _unitOfWork.UserRepo.GetByIdAsync(driverUserId.Value);
                if (user == null) return (default, new ResponseDTO("Người lái không tồn tại", 400, false));
                if (parsedStatus == SessionStatus.Active &&
                    !string.Equals(user.Status, UserStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return (default, new ResponseDTO("Người lái không ở trạng thái Active", 400, false));
                }
            }

            if (assignedSlotId.HasValue)
            {
                if (assignedSlotId.Value == Guid.Empty) return (default, new ResponseDTO("AssignedSlotId không hợp lệ", 400, false));

                var slot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(assignedSlotId.Value);
                if (slot == null) return (default, new ResponseDTO("Vị trí đỗ được gán không tồn tại", 400, false));
                if (slot.VehicleTypeId != vehicleTypeId) return (default, new ResponseDTO("Vị trí đỗ được gán không cùng loại phương tiện", 400, false));
            }

            if (actualSlotId.HasValue)
            {
                if (actualSlotId.Value == Guid.Empty) return (default, new ResponseDTO("ActualSlotId không hợp lệ", 400, false));

                var slot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(actualSlotId.Value);
                if (slot == null) return (default, new ResponseDTO("Vị trí đỗ thực tế không tồn tại", 400, false));
                if (slot.VehicleTypeId != vehicleTypeId) return (default, new ResponseDTO("Vị trí đỗ thực tế không cùng loại phương tiện", 400, false));
            }

            if (!string.IsNullOrWhiteSpace(licensePlateOut) && NormalizePlate(licensePlateOut) != NormalizePlate(licensePlateIn))
            {
                return (default, new ResponseDTO("Biển số ra không khớp biển số vào", 400, false));
            }

            if (parsedStatus == SessionStatus.Active)
            {
                var hasActiveSession = await _unitOfWork.ParkingSessionRepo
                    .HasActiveSessionByLicensePlateAsync(NormalizePlate(licensePlateIn), currentSessionId);
                if (hasActiveSession)
                {
                    return (default, new ResponseDTO("Biển số đang có phiên gửi xe Active", 409, false));
                }
            }

            return (parsedStatus, null);
        }

        private static bool IsGateType(Gate gate, string gateType)
        {
            return string.Equals(gate.GateType, gateType, StringComparison.OrdinalIgnoreCase);
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
                ReservationId = session.ReservationId,
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
