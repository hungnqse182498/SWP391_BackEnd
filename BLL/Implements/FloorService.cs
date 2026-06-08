using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Floor;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Implements
{
    public class FloorService : IFloorService
    {
        private readonly IUnitOfWork _unitOfWork;

        public FloorService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var floors = await _unitOfWork.FloorRepo.GetAllWithVehicleTypeAsync();

            var floorList = floors.ToList();
            if (floorList.Count == 0)
            {
                return new ResponseDTO("Không tìm thấy tầng nào", 404, false);
            }

            var dtos = floorList.Select(MapToDTO).ToList();
            return new ResponseDTO("Lấy danh sách tầng thành công", 200, true, dtos);
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
                return new ResponseDTO("Vui lòng nhập FloorId", 400, false);

            var floor = await _unitOfWork.FloorRepo.GetByIdWithVehicleTypeAsync(id);

            if (floor == null)
                return new ResponseDTO("Không tìm thấy tầng", 404, false);

            return new ResponseDTO("Lấy thông tin tầng thành công", 200, true, MapToDTO(floor));
        }

        public async Task<ResponseDTO> CreateAsync(CreateFloorDTO dto)
        {
            if (dto == null)
                return new ResponseDTO("Dữ liệu tạo tầng không hợp lệ", 400, false);

            var validation = await ValidateFloorAsync(dto.FloorName, dto.DedicatedVehicleTypeId, null);
            if (validation.Error != null)
                return validation.Error;

            var entity = new Floor
            {
                FloorId = Guid.NewGuid(),
                FloorName = dto.FloorName.Trim(),
                DedicatedVehicleTypeId = dto.DedicatedVehicleTypeId,
                TotalCapacity = dto.TotalCapacity,
                IsResident = dto.IsResident
            };

            try
            {
                await _unitOfWork.FloorRepo.AddAsync(entity);
                await _unitOfWork.SaveChangeAsync();

                entity.DedicatedVehicleType = validation.VehicleType;

                return new ResponseDTO("Tạo tầng thành công", 201, true, MapToDTO(entity));
            }
            catch (DbUpdateException)
            {
                return new ResponseDTO("Dữ liệu tầng bị trùng hoặc không hợp lệ", 400, false);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi tạo tầng: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> UpdateAsync(UpdateFloorDTO dto)
        {
            if (dto == null || dto.FloorId == Guid.Empty)
                return new ResponseDTO("Dữ liệu cập nhật không hợp lệ", 400, false);

            var existing = await _unitOfWork.FloorRepo.GetByIdAsync(dto.FloorId);
            if (existing == null)
                return new ResponseDTO("Không tìm thấy tầng", 404, false);

            var validation = await ValidateFloorAsync(dto.FloorName, dto.DedicatedVehicleTypeId, dto.FloorId);
            if (validation.Error != null)
                return validation.Error;

            existing.FloorName = dto.FloorName.Trim();
            existing.DedicatedVehicleTypeId = dto.DedicatedVehicleTypeId;
            existing.TotalCapacity = dto.TotalCapacity;
            existing.IsResident = dto.IsResident;

            try
            {
                await _unitOfWork.FloorRepo.UpdateAsync(existing);
                await _unitOfWork.SaveChangeAsync();

                existing.DedicatedVehicleType = validation.VehicleType;

                return new ResponseDTO("Cập nhật tầng thành công", 200, true, MapToDTO(existing));
            }
            catch (DbUpdateException)
            {
                return new ResponseDTO("Dữ liệu tầng bị trùng hoặc không hợp lệ", 400, false);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi cập nhật tầng: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty)
                return new ResponseDTO("Vui lòng nhập FloorId", 400, false);

            var existing = await _unitOfWork.FloorRepo.GetByIdAsync(id);
            if (existing == null)
                return new ResponseDTO("Không tìm thấy tầng", 404, false);

            try
            {
                _unitOfWork.FloorRepo.Delete(existing);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Xóa tầng thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi xóa tầng: {ex.Message}", 500, false);
            }
        }

        private async Task<(VehicleType? VehicleType, ResponseDTO? Error)> ValidateFloorAsync(string? floorName, Guid? vehicleTypeId, Guid? currentFloorId)
        {
            if (string.IsNullOrWhiteSpace(floorName))
                return (null, new ResponseDTO("Vui lòng nhập tên tầng", 400, false));

            var trimmedName = floorName.Trim();

            var duplicate = await _unitOfWork.FloorRepo.IsNameDuplicateAsync(trimmedName, currentFloorId);

            if (duplicate)
                return (null, new ResponseDTO("Tên tầng đã tồn tại", 400, false));

            VehicleType? vehicleType = null;
            if (vehicleTypeId.HasValue)
            {
                vehicleType = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(vehicleTypeId.Value);
                if (vehicleType == null)
                    return (null, new ResponseDTO("Loại phương tiện chuyên dụng không tồn tại", 400, false));
            }

            return (vehicleType, null);
        }

        private static FloorDTO MapToDTO(Floor floor)
        {
            return new FloorDTO
            {
                FloorId = floor.FloorId,
                FloorName = floor.FloorName,
                DedicatedVehicleTypeId = floor.DedicatedVehicleTypeId,
                DedicatedVehicleTypeName = floor.DedicatedVehicleType?.TypeName,
                TotalCapacity = floor.TotalCapacity,
                IsResident = floor.IsResident
            };
        }
    }
}