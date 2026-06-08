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
            var floors = await _unitOfWork.FloorRepo
                .GetAll()
                .Include(f => f.DedicatedVehicleType)
                .ToListAsync();

            if (floors == null || !floors.Any())
            {
                return new ResponseDTO("Không tìm thấy tầng nào", 404, false);
            }

            var dtos = floors.Select(f => new FloorDTO
            {
                FloorId = f.FloorId,
                FloorName = f.FloorName,
                DedicatedVehicleTypeId = f.DedicatedVehicleTypeId,
                DedicatedVehicleTypeName = f.DedicatedVehicleType?.TypeName,
                TotalCapacity = f.TotalCapacity,
                IsResident = f.IsResident
            }).ToList();

            return new ResponseDTO("Lấy danh sách tầng thành công", 200, true, dtos);
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
                return new ResponseDTO("Vui lòng nhập FloorId", 400, false);

            var floor = await _unitOfWork.FloorRepo.GetByIdWithVehicleTypeAsync(id);
            if (floor == null)
                return new ResponseDTO("Không tìm thấy tầng", 404, false);

            var dto = new FloorDTO
            {
                FloorId = floor.FloorId,
                FloorName = floor.FloorName,
                DedicatedVehicleTypeId = floor.DedicatedVehicleTypeId,
                DedicatedVehicleTypeName = floor.DedicatedVehicleType?.TypeName,
                TotalCapacity = floor.TotalCapacity,
                IsResident = floor.IsResident
            };

            return new ResponseDTO("Lấy thông tin tầng thành công", 200, true, dto);
        }

        public async Task<ResponseDTO> CreateAsync(CreateFloorDTO dto)
        {
            if (dto == null)
                return new ResponseDTO("Dữ liệu tạo tầng không hợp lệ", 400, false);

            if (string.IsNullOrWhiteSpace(dto.FloorName))
                return new ResponseDTO("Vui lòng nhập tên tầng", 400, false);

            var name = dto.FloorName.Trim();

            var exists = await _unitOfWork.FloorRepo.FindByNameAsync(name);
            if (exists != null)
                return new ResponseDTO("Tên tầng đã tồn tại", 400, false);

            if (dto.DedicatedVehicleTypeId.HasValue)
            {
                var vt = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(dto.DedicatedVehicleTypeId.Value);
                if (vt == null)
                    return new ResponseDTO("Loại phương tiện chuyên dụng không tồn tại", 400, false);
            }

            var entity = new Floor
            {
                FloorId = Guid.NewGuid(),
                FloorName = name,
                DedicatedVehicleTypeId = dto.DedicatedVehicleTypeId,
                TotalCapacity = dto.TotalCapacity,
                IsResident = dto.IsResident
            };

            try
            {
                await _unitOfWork.FloorRepo.AddAsync(entity);
                await _unitOfWork.SaveChangeAsync();

                var result = new FloorDTO
                {
                    FloorId = entity.FloorId,
                    FloorName = entity.FloorName,
                    DedicatedVehicleTypeId = entity.DedicatedVehicleTypeId,
                    DedicatedVehicleTypeName = null,
                    TotalCapacity = entity.TotalCapacity,
                    IsResident = entity.IsResident
                };

                if (entity.DedicatedVehicleTypeId.HasValue)
                {
                    var vt = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(entity.DedicatedVehicleTypeId.Value);
                    if (vt != null) result.DedicatedVehicleTypeName = vt.TypeName;
                }

                return new ResponseDTO("Tạo tầng thành công", 201, true, result);
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

            var existing = await _unitOfWork.FloorRepo.GetByIdWithVehicleTypeAsync(dto.FloorId);
            if (existing == null)
                return new ResponseDTO("Không tìm thấy tầng", 404, false);

            if (string.IsNullOrWhiteSpace(dto.FloorName))
                return new ResponseDTO("Vui lòng nhập tên tầng", 400, false);

            var name = dto.FloorName.Trim();

            var duplicate = await _unitOfWork.FloorRepo.GetAll()
                .FirstOrDefaultAsync(f => f.FloorName.ToLower() == name.ToLower() && f.FloorId != dto.FloorId);
            if (duplicate != null)
                return new ResponseDTO("Tên tầng đã tồn tại", 400, false);

            if (dto.DedicatedVehicleTypeId.HasValue)
            {
                var vt = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(dto.DedicatedVehicleTypeId.Value);
                if (vt == null)
                    return new ResponseDTO("Loại phương tiện chuyên dụng không tồn tại", 400, false);
            }

            existing.FloorName = name;
            existing.DedicatedVehicleTypeId = dto.DedicatedVehicleTypeId;
            existing.TotalCapacity = dto.TotalCapacity;

            try
            {
                await _unitOfWork.FloorRepo.UpdateAsync(existing);
                await _unitOfWork.SaveChangeAsync();

                var result = new FloorDTO
                {
                    FloorId = existing.FloorId,
                    FloorName = existing.FloorName,
                    DedicatedVehicleTypeId = existing.DedicatedVehicleTypeId,
                    DedicatedVehicleTypeName = existing.DedicatedVehicleType?.TypeName,
                    TotalCapacity = existing.TotalCapacity,
                    IsResident = existing.IsResident
                };

                // refresh DedicatedVehicleTypeName if needed
                if (existing.DedicatedVehicleTypeId.HasValue && string.IsNullOrWhiteSpace(result.DedicatedVehicleTypeName))
                {
                    var vt = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(existing.DedicatedVehicleTypeId.Value);
                    if (vt != null) result.DedicatedVehicleTypeName = vt.TypeName;
                }

                return new ResponseDTO("Cập nhật tầng thành công", 200, true, result);
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
    }
}