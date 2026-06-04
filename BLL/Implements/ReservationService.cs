using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Reservation;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements
{
    public class ReservationService : IReservationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Pending",
            "Confirmed",
            "CheckedIn",
            "Completed",
            "Cancelled",
            "Expired"
        };

        public ReservationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var reservations = await _unitOfWork.ReservationRepo.GetAll()
                .Include(r => r.User)
                .Include(r => r.VehicleType)
                .Include(r => r.AssignedSlot)
                .OrderByDescending(r => r.ExpectedEntryTime)
                .ToListAsync();

            return new ResponseDTO("Lấy danh sách đặt chỗ thành công", 200, true, reservations.Select(MapToDTO).ToList());
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập ReservationId", 400, false);

            var reservation = await _unitOfWork.ReservationRepo.GetAll()
                .Include(r => r.User)
                .Include(r => r.VehicleType)
                .Include(r => r.AssignedSlot)
                .FirstOrDefaultAsync(r => r.ReservationId == id);

            if (reservation == null) return new ResponseDTO("Không tìm thấy đặt chỗ", 404, false);
            return new ResponseDTO("Lấy thông tin đặt chỗ thành công", 200, true, MapToDTO(reservation));
        }

        public async Task<ResponseDTO> CreateAsync(CreateReservationDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu tạo đặt chỗ không hợp lệ", 400, false);

            var validation = await ValidateReservationAsync(dto.UserId, dto.VehicleTypeId, dto.AssignedSlotId, dto.ExpectedEntryTime, dto.ExpectedExitTime, dto.Status ?? "Confirmed");
            if (validation.Error != null) return validation.Error;

            var reservation = new Reservation
            {
                ReservationId = Guid.NewGuid(),
                UserId = dto.UserId,
                VehicleTypeId = dto.VehicleTypeId,
                AssignedSlotId = dto.AssignedSlotId,
                ExpectedEntryTime = dto.ExpectedEntryTime,
                ExpectedExitTime = dto.ExpectedExitTime,
                Status = validation.Status!,
                CreatedAt = DateTime.UtcNow
            };

            if (ShouldReserveSlot(reservation.Status))
            {
                validation.Slot!.Status = "Reserved";
                await _unitOfWork.ParkingSlotRepo.UpdateAsync(validation.Slot);
            }

            await _unitOfWork.ReservationRepo.AddAsync(reservation);
            await _unitOfWork.SaveChangeAsync();
            reservation.User = validation.User;
            reservation.VehicleType = validation.VehicleType;
            reservation.AssignedSlot = validation.Slot;

            return new ResponseDTO("Tạo đặt chỗ thành công", 201, true, MapToDTO(reservation));
        }

        public async Task<ResponseDTO> UpdateAsync(UpdateReservationDTO dto)
        {
            if (dto == null || dto.ReservationId == Guid.Empty) return new ResponseDTO("Dữ liệu cập nhật đặt chỗ không hợp lệ", 400, false);

            var reservation = await _unitOfWork.ReservationRepo.GetByIdAsync(dto.ReservationId);
            if (reservation == null) return new ResponseDTO("Không tìm thấy đặt chỗ", 404, false);

            var previousSlotId = reservation.AssignedSlotId;
            var previousStatus = reservation.Status;
            var validation = await ValidateReservationAsync(dto.UserId, dto.VehicleTypeId, dto.AssignedSlotId, dto.ExpectedEntryTime, dto.ExpectedExitTime, dto.Status);
            if (validation.Error != null) return validation.Error;

            reservation.UserId = dto.UserId;
            reservation.VehicleTypeId = dto.VehicleTypeId;
            reservation.AssignedSlotId = dto.AssignedSlotId;
            reservation.ExpectedEntryTime = dto.ExpectedEntryTime;
            reservation.ExpectedExitTime = dto.ExpectedExitTime;
            reservation.Status = validation.Status!;

            if (previousSlotId != dto.AssignedSlotId && ShouldReserveSlot(previousStatus))
            {
                var oldSlot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(previousSlotId);
                if (oldSlot != null && oldSlot.Status == "Reserved") oldSlot.Status = "Available";
            }

            if (ShouldReserveSlot(reservation.Status))
            {
                validation.Slot!.Status = "Reserved";
                await _unitOfWork.ParkingSlotRepo.UpdateAsync(validation.Slot);
            }
            else if (validation.Slot!.Status == "Reserved")
            {
                validation.Slot.Status = "Available";
                await _unitOfWork.ParkingSlotRepo.UpdateAsync(validation.Slot);
            }

            await _unitOfWork.ReservationRepo.UpdateAsync(reservation);
            await _unitOfWork.SaveChangeAsync();
            reservation.User = validation.User;
            reservation.VehicleType = validation.VehicleType;
            reservation.AssignedSlot = validation.Slot;

            return new ResponseDTO("Cập nhật đặt chỗ thành công", 200, true, MapToDTO(reservation));
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập ReservationId", 400, false);

            var reservation = await _unitOfWork.ReservationRepo.GetByIdAsync(id);
            if (reservation == null) return new ResponseDTO("Không tìm thấy đặt chỗ", 404, false);

            try
            {
                var slot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(reservation.AssignedSlotId);
                if (slot != null && slot.Status == "Reserved")
                {
                    slot.Status = "Available";
                    await _unitOfWork.ParkingSlotRepo.UpdateAsync(slot);
                }

                _unitOfWork.ReservationRepo.Delete(reservation);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Xóa đặt chỗ thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi xóa đặt chỗ: {ex.Message}", 500, false);
            }
        }

        private async Task<(User? User, VehicleType? VehicleType, ParkingSlot? Slot, string? Status, ResponseDTO? Error)> ValidateReservationAsync(
            Guid userId,
            Guid vehicleTypeId,
            Guid assignedSlotId,
            DateTime expectedEntryTime,
            DateTime expectedExitTime,
            string? status)
        {
            if (userId == Guid.Empty) return (null, null, null, null, new ResponseDTO("Vui lòng chọn người đặt", 400, false));
            if (vehicleTypeId == Guid.Empty) return (null, null, null, null, new ResponseDTO("Vui lòng chọn loại phương tiện", 400, false));
            if (assignedSlotId == Guid.Empty) return (null, null, null, null, new ResponseDTO("Vui lòng chọn vị trí đỗ", 400, false));
            if (expectedExitTime <= expectedEntryTime) return (null, null, null, null, new ResponseDTO("Thời gian ra dự kiến phải sau thời gian vào", 400, false));

            var normalizedStatus = NormalizeStatus(status);
            if (normalizedStatus == null) return (null, null, null, null, new ResponseDTO("Trạng thái đặt chỗ không hợp lệ", 400, false));

            var user = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(userId);
            if (user == null) return (null, null, null, null, new ResponseDTO("Người đặt không tồn tại", 400, false));

            var vehicleType = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(vehicleTypeId);
            if (vehicleType == null) return (null, null, null, null, new ResponseDTO("Loại phương tiện không tồn tại", 400, false));

            var slot = await _unitOfWork.ParkingSlotRepo.GetAll()
                .Include(s => s.Floor)
                .FirstOrDefaultAsync(s => s.SlotId == assignedSlotId);
            if (slot == null) return (null, null, null, null, new ResponseDTO("Vị trí đỗ không tồn tại", 400, false));
            if (slot.VehicleTypeId != vehicleTypeId) return (null, null, null, null, new ResponseDTO("Vị trí đỗ không phù hợp loại phương tiện", 400, false));
            if (ShouldReserveSlot(normalizedStatus) && slot.Status != "Available" && slot.Status != "Reserved")
            {
                return (null, null, null, null, new ResponseDTO("Vị trí đỗ không khả dụng để đặt trước", 409, false));
            }

            return (user, vehicleType, slot, normalizedStatus, null);
        }

        private static bool ShouldReserveSlot(string? status)
        {
            return string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            return ValidStatuses.FirstOrDefault(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static ReservationDTO MapToDTO(Reservation reservation)
        {
            return new ReservationDTO
            {
                ReservationId = reservation.ReservationId,
                UserId = reservation.UserId,
                UserFullName = reservation.User?.FullName,
                VehicleTypeId = reservation.VehicleTypeId,
                VehicleTypeName = reservation.VehicleType?.TypeName,
                AssignedSlotId = reservation.AssignedSlotId,
                AssignedSlotCode = reservation.AssignedSlot?.SlotCode,
                ExpectedEntryTime = reservation.ExpectedEntryTime,
                ExpectedExitTime = reservation.ExpectedExitTime,
                Status = reservation.Status,
                CreatedAt = reservation.CreatedAt
            };
        }
    }
}
