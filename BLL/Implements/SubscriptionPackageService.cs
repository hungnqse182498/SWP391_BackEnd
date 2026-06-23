using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Subscription;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements
{
    public class SubscriptionPackageService : ISubscriptionPackageService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SubscriptionPackageService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllPackagesAsync()
        {
            var entities = await _unitOfWork.SubscriptionPackageRepo.GetAllWithVehicleTypeAsync();
            var dtos = entities
                .OrderBy(p => p.VehicleType?.TypeName)
                .ThenBy(p => p.DurationMonths)
                .Select(MapToDTO)
                .ToList();

            return new ResponseDTO("Lấy danh sách gói thành công", 200, true, dtos);
        }

        public async Task<ResponseDTO> GetPackageByIdAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Mã cấu hình gói không hợp lệ", 400);

            var package = await _unitOfWork.SubscriptionPackageRepo.GetByIdWithVehicleTypeAsync(id);
            if (package == null) return new ResponseDTO("Không tìm thấy gói cước", 404);

            return new ResponseDTO("Lấy chi tiết gói thành công", 200, true, MapToDTO(package));
        }

        public async Task<ResponseDTO> CreatePackageAsync(CreateSubscriptionPackageDTO dto)
        {
            var validation = await ValidatePackageDataAsync(dto.PackageName, dto.VehicleTypeId, dto.DurationMonths, dto.Price);
            if (validation != null) return validation;

            if (!Enum.TryParse<PackageStatus>(dto.Status, true, out var parsedStatus))
            {
                parsedStatus = PackageStatus.Active;
            }

            var package = new DAL.Models.SubscriptionPackage
            {
                PackageId = Guid.NewGuid(),
                PackageName = dto.PackageName.Trim(),
                VehicleTypeId = dto.VehicleTypeId,
                DurationMonths = dto.DurationMonths,
                Price = dto.Price,
                RequireFixedSlot = dto.RequireFixedSlot,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                Status = parsedStatus.ToString()
            };

            await _unitOfWork.SubscriptionPackageRepo.AddAsync(package);
            await _unitOfWork.SaveAsync();

            var fullPackage = await _unitOfWork.SubscriptionPackageRepo.GetByIdWithVehicleTypeAsync(package.PackageId);
            return new ResponseDTO("Tạo gói thành công", 201, true, MapToDTO(fullPackage ?? package));
        }

        public async Task<ResponseDTO> UpdatePackageAsync(Guid id, UpdateSubscriptionPackageDTO dto)
        {
            if (id == Guid.Empty) return new ResponseDTO("Mã cấu hình gói không hợp lệ", 400);

            var package = await _unitOfWork.SubscriptionPackageRepo.GetByIdWithVehicleTypeAsync(id);
            if (package == null) return new ResponseDTO("Không tìm thấy gói cước cần cập nhật", 404);

            var validation = await ValidatePackageDataAsync(dto.PackageName, dto.VehicleTypeId, dto.DurationMonths, dto.Price);
            if (validation != null) return validation;

            if (!Enum.TryParse<PackageStatus>(dto.Status, true, out var parsedStatus))
            {
                parsedStatus = PackageStatus.Active;
            }

            package.PackageName = dto.PackageName.Trim();
            package.VehicleTypeId = dto.VehicleTypeId;
            package.DurationMonths = dto.DurationMonths;
            package.Price = dto.Price;
            package.RequireFixedSlot = dto.RequireFixedSlot;
            package.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            package.Status = parsedStatus.ToString();

            await _unitOfWork.SubscriptionPackageRepo.UpdateAsync(package);
            await _unitOfWork.SaveAsync();

            return new ResponseDTO("Cập nhật gói thành công", 200, true, MapToDTO(package));
        }

        public async Task<ResponseDTO> DeletePackageAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Mã cấu hình gói không hợp lệ", 400);

            var package = await _unitOfWork.SubscriptionPackageRepo.GetByIdAsync(id);
            if (package == null) return new ResponseDTO("Không tìm thấy gói cước", 404);

            package.Status = "Inactive";
            await _unitOfWork.SubscriptionPackageRepo.UpdateAsync(package);
            await _unitOfWork.SaveAsync();

            return new ResponseDTO("Xóa gói thành công", 200, true);
        }

        private async Task<ResponseDTO?> ValidatePackageDataAsync(string? packageName, Guid vehicleTypeId, int durationMonths, decimal price)
        {
            if (string.IsNullOrWhiteSpace(packageName)) return new ResponseDTO("Vui lòng nhập tên gói", 400);
            if (vehicleTypeId == Guid.Empty) return new ResponseDTO("Vui lòng chọn loại phương tiện", 400);
            if (durationMonths <= 0) return new ResponseDTO("Thời hạn gói phải lớn hơn 0 tháng", 400);
            if (price <= 0) return new ResponseDTO("Giá gói phải lớn hơn 0 VNĐ", 400);

            var vehicleTypeExists = await _unitOfWork.VehicleTypeRepo.AnyAsync(v => v.VehicleTypeId == vehicleTypeId);
            if (!vehicleTypeExists) return new ResponseDTO("Loại phương tiện không tồn tại", 400);

            return null;
        }

        private static SubscriptionPackageDTO MapToDTO(SubscriptionPackage package)
        {
            return new SubscriptionPackageDTO
            {
                PackageId = package.PackageId,
                PackageName = package.PackageName,
                VehicleTypeId = package.VehicleTypeId,
                VehicleTypeName = package.VehicleType?.TypeName,
                DurationMonths = package.DurationMonths,
                Price = package.Price,
                RequireFixedSlot = package.RequireFixedSlot ?? false,
                Description = package.Description,
                Status = package.Status
            };
        }
    }
}
