using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.VehicleType;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Implements
{
    public class VehicleTypeService : IVehicleTypeService
    {
        private readonly IUnitOfWork _unitOfWork;

        public VehicleTypeService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var vehicleTypes = await _unitOfWork.VehicleTypeRepo.GetAll().ToListAsync();
            if (vehicleTypes == null || !vehicleTypes.Any())
                return new ResponseDTO("Không tìm thấy loại phương tiện nào", 404, false);

            return new ResponseDTO("Lấy danh sách loại phương tiện thành công", 200, true, vehicleTypes.Select(MapToDTO).ToList());
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
                return new ResponseDTO("Vui lòng nhập VehicleTypeId", 400, false);

            var vehicleType = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(id);
            if (vehicleType == null)
                return new ResponseDTO("Không tìm thấy loại phương tiện", 404, false);

            return new ResponseDTO("Lấy loại phương tiện thành công", 200, true, MapToDTO(vehicleType));
        }

        public async Task<ResponseDTO> CreateAsync(CreateVehicleTypeDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.TypeName))
                return new ResponseDTO("Dữ liệu không hợp lệ", 400, false);

            var exists = await _unitOfWork.VehicleTypeRepo.FindByNameAsync(dto.TypeName.Trim());
            if (exists != null)
                return new ResponseDTO("Tên loại phương tiện đã tồn tại", 400, false);

            var entity = new VehicleType
            {
                VehicleTypeId = Guid.NewGuid(),
                TypeName = dto.TypeName.Trim(),
                Dimensions = string.IsNullOrWhiteSpace(dto.Dimensions) ? null : dto.Dimensions.Trim()
            };

            try
            {
                await _unitOfWork.VehicleTypeRepo.AddAsync(entity);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Tạo loại phương tiện thành công", 201, true, MapToDTO(entity));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi tạo loại phương tiện: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> UpdateAsync(UpdateVehicleTypeDTO dto)
        {
            if (dto == null || dto.VehicleTypeId == Guid.Empty)
                return new ResponseDTO("Dữ liệu không hợp lệ", 400, false);

            var existing = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(dto.VehicleTypeId);
            if (existing == null)
                return new ResponseDTO("Không tìm thấy loại phương tiện", 404, false);

            if (string.IsNullOrWhiteSpace(dto.TypeName))
                return new ResponseDTO("Vui lòng nhập TypeName", 400, false);

            var isDuplicate = await _unitOfWork.VehicleTypeRepo.IsTypeNameDuplicateAsync(dto.TypeName, dto.VehicleTypeId);
            if (isDuplicate)
                return new ResponseDTO("TypeName đã tồn tại", 400, false);

            existing.TypeName = dto.TypeName.Trim();
            existing.Dimensions = string.IsNullOrWhiteSpace(dto.Dimensions) ? null : dto.Dimensions.Trim();

            try
            {
                await _unitOfWork.VehicleTypeRepo.UpdateAsync(existing);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Cập nhật loại phương tiện thành công", 200, true, MapToDTO(existing));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi cập nhật loại phương tiện: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty)
                return new ResponseDTO("Vui lòng nhập VehicleTypeId", 400, false);

            var existing = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(id);
            if (existing == null)
                return new ResponseDTO("Không tìm thấy loại phương tiện", 404, false);

            try
            {
                _unitOfWork.VehicleTypeRepo.Delete(existing);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Xóa loại phương tiện thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi xóa loại phương tiện: {ex.Message}", 500, false);
            }
        }

        private static VehicleTypeDTO MapToDTO(VehicleType entity)
        {
            return new VehicleTypeDTO
            {
                VehicleTypeId = entity.VehicleTypeId,
                TypeName = entity.TypeName,
                Dimensions = entity.Dimensions
            };
        }
    }
}