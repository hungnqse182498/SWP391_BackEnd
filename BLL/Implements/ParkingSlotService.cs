using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.ParkingSlot;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements
{
    public class ParkingSlotService : IParkingSlotService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ParkingSlotService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var slots = await _unitOfWork.ParkingSlotRepo.GetAllWithDetailsAsync();
            return new ResponseDTO("Lấy danh sách vị trí đỗ thành công", 200, true, slots.Select(MapToDTO).ToList());
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập SlotId", 400, false);

            var slot = await _unitOfWork.ParkingSlotRepo.GetDetailWithFloorAndTypeAsync(id);

            if (slot == null) return new ResponseDTO("Không tìm thấy vị trí đỗ", 404, false);
            return new ResponseDTO("Lấy thông tin vị trí đỗ thành công", 200, true, MapToDTO(slot));
        }

        public async Task<ResponseDTO> CreateAsync(CreateParkingSlotDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu tạo vị trí đỗ không hợp lệ", 400, false);

            var validation = await ValidateSlotAsync(dto.SlotCode, dto.FloorId, dto.VehicleTypeId, dto.Status ?? ParkingSlotStatus.Available.ToString(), null);
            if (validation.Error != null) return validation.Error;

            var slot = new ParkingSlot
            {
                SlotId = Guid.NewGuid(),
                FloorId = dto.FloorId,
                SlotCode = dto.SlotCode.Trim(),
                VehicleTypeId = dto.VehicleTypeId,
                Status = validation.Status.ToString()
            };

            await _unitOfWork.ParkingSlotRepo.AddAsync(slot);
            await _unitOfWork.SaveChangeAsync();
            slot.Floor = validation.Floor;
            slot.VehicleType = validation.VehicleType;

            return new ResponseDTO("Tạo vị trí đỗ thành công", 201, true, MapToDTO(slot));
        }

        public async Task<ResponseDTO> UpdateAsync(UpdateParkingSlotDTO dto)
        {
            if (dto == null || dto.SlotId == Guid.Empty) return new ResponseDTO("Dữ liệu cập nhật vị trí đỗ không hợp lệ", 400, false);

            var slot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(dto.SlotId);
            if (slot == null) return new ResponseDTO("Không tìm thấy vị trí đỗ", 404, false);

            var validation = await ValidateSlotAsync(dto.SlotCode, dto.FloorId, dto.VehicleTypeId, dto.Status, dto.SlotId);
            if (validation.Error != null) return validation.Error;

            slot.FloorId = dto.FloorId;
            slot.SlotCode = dto.SlotCode.Trim();
            slot.VehicleTypeId = dto.VehicleTypeId;
            slot.Status = validation.Status.ToString();

            await _unitOfWork.ParkingSlotRepo.UpdateAsync(slot);
            await _unitOfWork.SaveChangeAsync();
            slot.Floor = validation.Floor;
            slot.VehicleType = validation.VehicleType;

            return new ResponseDTO("Cập nhật vị trí đỗ thành công", 200, true, MapToDTO(slot));
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập SlotId", 400, false);

            var slot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(id);
            if (slot == null) return new ResponseDTO("Không tìm thấy vị trí đỗ", 404, false);

            try
            {
                _unitOfWork.ParkingSlotRepo.Delete(slot);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Xóa vị trí đỗ thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi xóa vị trí đỗ: {ex.Message}", 500, false);
            }
        }

        private async Task<(Floor? Floor, VehicleType? VehicleType, ParkingSlotStatus Status, ResponseDTO? Error)> ValidateSlotAsync(string? slotCode, Guid floorId, Guid vehicleTypeId, string? status, Guid? currentSlotId)
        {
            if (string.IsNullOrWhiteSpace(slotCode)) return (null, null, default, new ResponseDTO("Vui lòng nhập mã vị trí đỗ", 400, false));
            if (floorId == Guid.Empty) return (null, null, default, new ResponseDTO("Vui lòng chọn tầng", 400, false));
            if (vehicleTypeId == Guid.Empty) return (null, null, default, new ResponseDTO("Vui lòng chọn loại phương tiện", 400, false));

            var floor = await _unitOfWork.FloorRepo.GetByIdAsync(floorId);
            if (floor == null) return (null, null, default, new ResponseDTO("Tầng không tồn tại", 400, false));

            if (!currentSlotId.HasValue)
            {
                var currentSlotsCount = await _unitOfWork.ParkingSlotRepo.GetSlotsCountByFloorAsync(floorId);

                if (currentSlotsCount >= floor.TotalCapacity)
                {
                    return (null, null, default, new ResponseDTO($"Tầng này đã đạt giới hạn sức chứa tối đa ({floor.TotalCapacity} vị trí đỗ). Không thể tạo thêm!", 400, false));
                }
            }

            if (string.IsNullOrWhiteSpace(status) || !Enum.TryParse<ParkingSlotStatus>(status.Trim(), true, out var parsedStatus))
            {
                return (null, null, default, new ResponseDTO("Trạng thái vị trí đỗ không hợp lệ (Chỉ nhận: Available, Occupied, Reserved, Assigned)", 400, false));
            }

            var vehicleType = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(vehicleTypeId);
            if (vehicleType == null) return (null, null, default, new ResponseDTO("Loại phương tiện không tồn tại", 400, false));

            var code = slotCode.Trim().ToLower();
            var duplicate = await _unitOfWork.ParkingSlotRepo.IsSlotCodeDuplicateAsync(slotCode, currentSlotId);
            if (duplicate) return (null, null, default, new ResponseDTO("Mã vị trí đỗ đã tồn tại", 400, false));

            return (floor, vehicleType, parsedStatus, null);
        }

        private static ParkingSlotDTO MapToDTO(ParkingSlot slot)
        {
            return new ParkingSlotDTO
            {
                SlotId = slot.SlotId,
                FloorId = slot.FloorId,
                FloorName = slot.Floor?.FloorName,
                SlotCode = slot.SlotCode,
                VehicleTypeId = slot.VehicleTypeId,
                VehicleTypeName = slot.VehicleType?.TypeName,
                Status = slot.Status,
                IsResident = slot.Floor?.IsResident ?? false
            };
        }
    }
}
