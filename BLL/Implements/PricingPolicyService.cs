using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.PricingPolicy;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements
{
    public class PricingPolicyService : IPricingPolicyService
    {
        private readonly IUnitOfWork _unitOfWork;
        private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Active",
            "Inactive"
        };

        public PricingPolicyService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var policies = await _unitOfWork.PricingPolicyRepo.GetAll()
                .Include(p => p.VehicleType)
                .OrderByDescending(p => p.EffectiveDate)
                .ToListAsync();

            return new ResponseDTO("Lấy danh sách chính sách giá thành công", 200, true, policies.Select(MapToDTO).ToList());
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập PolicyId", 400, false);

            var policy = await _unitOfWork.PricingPolicyRepo.GetAll()
                .Include(p => p.VehicleType)
                .FirstOrDefaultAsync(p => p.PolicyId == id);

            if (policy == null) return new ResponseDTO("Không tìm thấy chính sách giá", 404, false);
            return new ResponseDTO("Lấy thông tin chính sách giá thành công", 200, true, MapToDTO(policy));
        }

        public async Task<ResponseDTO> CreateAsync(CreatePricingPolicyDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu tạo chính sách giá không hợp lệ", 400, false);

            var validation = await ValidatePolicyAsync(dto.VehicleTypeId, dto.BasePrice, dto.BaseHours, dto.ExtraHourPrice, dto.NightSurcharge, dto.Status ?? "Active");
            if (validation.Error != null) return validation.Error;

            var policy = new PricingPolicy
            {
                PolicyId = Guid.NewGuid(),
                VehicleTypeId = dto.VehicleTypeId,
                BasePrice = dto.BasePrice,
                BaseHours = dto.BaseHours,
                ExtraHourPrice = dto.ExtraHourPrice,
                NightSurcharge = dto.NightSurcharge ?? 0,
                EffectiveDate = dto.EffectiveDate,
                Status = validation.Status!
            };

            await _unitOfWork.PricingPolicyRepo.AddAsync(policy);
            await _unitOfWork.SaveChangeAsync();
            policy.VehicleType = validation.VehicleType;

            return new ResponseDTO("Tạo chính sách giá thành công", 201, true, MapToDTO(policy));
        }

        public async Task<ResponseDTO> UpdateAsync(UpdatePricingPolicyDTO dto)
        {
            if (dto == null || dto.PolicyId == Guid.Empty) return new ResponseDTO("Dữ liệu cập nhật chính sách giá không hợp lệ", 400, false);

            var policy = await _unitOfWork.PricingPolicyRepo.GetByIdAsync(dto.PolicyId);
            if (policy == null) return new ResponseDTO("Không tìm thấy chính sách giá", 404, false);

            var validation = await ValidatePolicyAsync(dto.VehicleTypeId, dto.BasePrice, dto.BaseHours, dto.ExtraHourPrice, dto.NightSurcharge, dto.Status);
            if (validation.Error != null) return validation.Error;

            policy.VehicleTypeId = dto.VehicleTypeId;
            policy.BasePrice = dto.BasePrice;
            policy.BaseHours = dto.BaseHours;
            policy.ExtraHourPrice = dto.ExtraHourPrice;
            policy.NightSurcharge = dto.NightSurcharge ?? 0;
            policy.EffectiveDate = dto.EffectiveDate;
            policy.Status = validation.Status!;

            await _unitOfWork.PricingPolicyRepo.UpdateAsync(policy);
            await _unitOfWork.SaveChangeAsync();
            policy.VehicleType = validation.VehicleType;

            return new ResponseDTO("Cập nhật chính sách giá thành công", 200, true, MapToDTO(policy));
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập PolicyId", 400, false);

            var policy = await _unitOfWork.PricingPolicyRepo.GetByIdAsync(id);
            if (policy == null) return new ResponseDTO("Không tìm thấy chính sách giá", 404, false);

            try
            {
                _unitOfWork.PricingPolicyRepo.Delete(policy);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Xóa chính sách giá thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi xóa chính sách giá: {ex.Message}", 500, false);
            }
        }

        private async Task<(VehicleType? VehicleType, string? Status, ResponseDTO? Error)> ValidatePolicyAsync(
            Guid vehicleTypeId,
            decimal basePrice,
            int baseHours,
            decimal extraHourPrice,
            decimal? nightSurcharge,
            string? status)
        {
            if (vehicleTypeId == Guid.Empty) return (null, null, new ResponseDTO("Vui lòng chọn loại phương tiện", 400, false));
            if (basePrice < 0) return (null, null, new ResponseDTO("Giá cơ bản không được âm", 400, false));
            if (baseHours <= 0) return (null, null, new ResponseDTO("Số giờ cơ bản phải lớn hơn 0", 400, false));
            if (extraHourPrice < 0) return (null, null, new ResponseDTO("Giá giờ thêm không được âm", 400, false));
            if (nightSurcharge < 0) return (null, null, new ResponseDTO("Phụ thu đêm không được âm", 400, false));

            var normalizedStatus = NormalizeStatus(status);
            if (normalizedStatus == null) return (null, null, new ResponseDTO("Trạng thái chính sách giá chỉ được là Active hoặc Inactive", 400, false));

            var vehicleType = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(vehicleTypeId);
            if (vehicleType == null) return (null, null, new ResponseDTO("Loại phương tiện không tồn tại", 400, false));

            return (vehicleType, normalizedStatus, null);
        }

        private static string? NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            return ValidStatuses.FirstOrDefault(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static PricingPolicyDTO MapToDTO(PricingPolicy policy)
        {
            return new PricingPolicyDTO
            {
                PolicyId = policy.PolicyId,
                VehicleTypeId = policy.VehicleTypeId,
                VehicleTypeName = policy.VehicleType?.TypeName,
                BasePrice = policy.BasePrice,
                BaseHours = policy.BaseHours,
                ExtraHourPrice = policy.ExtraHourPrice,
                NightSurcharge = policy.NightSurcharge,
                EffectiveDate = policy.EffectiveDate,
                Status = policy.Status
            };
        }
    }
}
