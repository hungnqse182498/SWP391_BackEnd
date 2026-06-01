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

            var vehicleTypeDTOs = vehicleTypes.Select(t => new VehicleTypeDTO
            {
                VehicleTypeId = t.VehicleTypeId,
                TypeName = t.TypeName,
                Dimensions = t.Dimensions
            }).ToList();

            return new ResponseDTO("Lấy danh sách loại phương tiện thành công", 200, true, vehicleTypeDTOs);
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
                return new ResponseDTO("Vui lòng nhập VehicleTypeId", 400, false);

            var vehicleType = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(id);
            if (vehicleType == null)
                return new ResponseDTO("Không tìm thấy loại phương tiện", 404, false);

            var vehicleTypeDTO = new VehicleTypeDTO
            {
                VehicleTypeId = vehicleType.VehicleTypeId,
                TypeName = vehicleType.TypeName,
                Dimensions = vehicleType.Dimensions
            };

            return new ResponseDTO("Lấy loại phương tiện thành công", 200, true, vehicleTypeDTO);
        }

        public async Task<ResponseDTO> CreateAsync(CreateVehicleTypeDTO createVehicleTypeDTO)
        {
            if (createVehicleTypeDTO == null || string.IsNullOrWhiteSpace(createVehicleTypeDTO.TypeName))
                return new ResponseDTO("Dữ liệu không hợp lệ", 400, false);

            var exists = await _unitOfWork.VehicleTypeRepo.FindByNameAsync(createVehicleTypeDTO.TypeName.Trim());
            if (exists != null)
                return new ResponseDTO("Tên loại phương tiện đã tồn tại", 400, false);

            var now = DateTime.UtcNow;
            var entity = new VehicleType
            {
                VehicleTypeId = Guid.NewGuid(),
                TypeName = createVehicleTypeDTO.TypeName.Trim(),
                Dimensions = string.IsNullOrWhiteSpace(createVehicleTypeDTO.Dimensions) ? null : createVehicleTypeDTO.Dimensions.Trim()
            };

            try
            {
                await _unitOfWork.VehicleTypeRepo.AddAsync(entity);
                await _unitOfWork.SaveChangeAsync();

                var result = new VehicleTypeDTO
                {
                    VehicleTypeId = entity.VehicleTypeId,
                    TypeName = entity.TypeName,
                    Dimensions = entity.Dimensions
                };

                return new ResponseDTO("Tạo loại phương tiện thành công", 201, true, result);
            }
            catch (DbUpdateException)
            {
                return new ResponseDTO("Dữ liệu không hợp lệ hoặc bị trùng", 400, false);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi tạo loại phương tiện: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> UpdateAsync(UpdateVehicleTypeDTO updateVehicleTypeDTO)
        {
            if (updateVehicleTypeDTO == null || updateVehicleTypeDTO.VehicleTypeId == Guid.Empty)
                return new ResponseDTO("Dữ liệu không hợp lệ", 400, false);

            var existing = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(updateVehicleTypeDTO.VehicleTypeId);
            if (existing == null)
                return new ResponseDTO("Không tìm thấy loại phương tiện", 404, false);

            if (string.IsNullOrWhiteSpace(updateVehicleTypeDTO.TypeName))
                return new ResponseDTO("Vui lòng nhập TypeName", 400, false);

            var duplicate = await _unitOfWork.VehicleTypeRepo.GetAll()
                .FirstOrDefaultAsync(v => v.TypeName.ToLower() == updateVehicleTypeDTO.TypeName.Trim().ToLower() && v.VehicleTypeId != updateVehicleTypeDTO.VehicleTypeId);
            if (duplicate != null)
                return new ResponseDTO("TypeName đã tồn tại", 400, false);

            existing.TypeName = updateVehicleTypeDTO.TypeName.Trim();
            existing.Dimensions = string.IsNullOrWhiteSpace(updateVehicleTypeDTO.Dimensions) ? null : updateVehicleTypeDTO.Dimensions.Trim();

            try
            {
                await _unitOfWork.VehicleTypeRepo.UpdateAsync(existing);
                await _unitOfWork.SaveChangeAsync();

                var result = new VehicleTypeDTO
                {
                    VehicleTypeId = existing.VehicleTypeId,
                    TypeName = existing.TypeName,
                    Dimensions = existing.Dimensions
                };

                return new ResponseDTO("Cập nhật loại phương tiện thành công", 200, true, result);
            }
            catch (DbUpdateException)
            {
                return new ResponseDTO("Dữ liệu không hợp lệ hoặc bị trùng", 400, false);
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
    }
}