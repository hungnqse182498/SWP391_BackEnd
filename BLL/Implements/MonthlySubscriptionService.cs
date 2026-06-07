using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.MonthlySubscription;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements
{
    public class MonthlySubscriptionService : IMonthlySubscriptionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Active",
            "Inactive",
            "Expired",
            "Cancelled"
        };

        public MonthlySubscriptionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var subscriptions = await _unitOfWork.MonthlySubscriptionRepo.GetAll()
                .Include(s => s.User)
                .Include(s => s.VehicleType)
                .OrderBy(s => s.LicensePlate)
                .ToListAsync();

            return new ResponseDTO("Lấy danh sách gói tháng thành công", 200, true, subscriptions.Select(MapToDTO).ToList());
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập SubscriptionId", 400, false);

            var subscription = await _unitOfWork.MonthlySubscriptionRepo.GetAll()
                .Include(s => s.User)
                .Include(s => s.VehicleType)
                .FirstOrDefaultAsync(s => s.SubscriptionId == id);

            if (subscription == null) return new ResponseDTO("Không tìm thấy gói tháng", 404, false);
            return new ResponseDTO("Lấy thông tin gói tháng thành công", 200, true, MapToDTO(subscription));
        }

        public async Task<ResponseDTO> CreateAsync(CreateMonthlySubscriptionDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu tạo gói tháng không hợp lệ", 400, false);

            var validation = await ValidateSubscriptionAsync(dto.UserId, dto.VehicleTypeId, dto.LicensePlate, dto.StartDate, dto.EndDate, dto.Price, dto.Status ?? "Active", null);
            if (validation.Error != null) return validation.Error;

            var subscription = new MonthlySubscription
            {
                SubscriptionId = Guid.NewGuid(),
                UserId = dto.UserId,
                VehicleTypeId = dto.VehicleTypeId,
                LicensePlate = NormalizePlate(dto.LicensePlate),
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Price = dto.Price,
                Status = validation.Status!
            };

            await _unitOfWork.MonthlySubscriptionRepo.AddAsync(subscription);
            await _unitOfWork.SaveChangeAsync();
            subscription.User = validation.User;
            subscription.VehicleType = validation.VehicleType;

            return new ResponseDTO("Tạo gói tháng thành công", 201, true, MapToDTO(subscription));
        }

        public async Task<ResponseDTO> UpdateAsync(UpdateMonthlySubscriptionDTO dto)
        {
            if (dto == null || dto.SubscriptionId == Guid.Empty) return new ResponseDTO("Dữ liệu cập nhật gói tháng không hợp lệ", 400, false);

            var subscription = await _unitOfWork.MonthlySubscriptionRepo.GetByIdAsync(dto.SubscriptionId);
            if (subscription == null) return new ResponseDTO("Không tìm thấy gói tháng", 404, false);

            var validation = await ValidateSubscriptionAsync(dto.UserId, dto.VehicleTypeId, dto.LicensePlate, dto.StartDate, dto.EndDate, dto.Price, dto.Status, dto.SubscriptionId);
            if (validation.Error != null) return validation.Error;

            subscription.UserId = dto.UserId;
            subscription.VehicleTypeId = dto.VehicleTypeId;
            subscription.LicensePlate = NormalizePlate(dto.LicensePlate);
            subscription.StartDate = dto.StartDate;
            subscription.EndDate = dto.EndDate;
            subscription.Price = dto.Price;
            subscription.Status = validation.Status!;

            await _unitOfWork.MonthlySubscriptionRepo.UpdateAsync(subscription);
            await _unitOfWork.SaveChangeAsync();
            subscription.User = validation.User;
            subscription.VehicleType = validation.VehicleType;

            return new ResponseDTO("Cập nhật gói tháng thành công", 200, true, MapToDTO(subscription));
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập SubscriptionId", 400, false);

            var subscription = await _unitOfWork.MonthlySubscriptionRepo.GetByIdAsync(id);
            if (subscription == null) return new ResponseDTO("Không tìm thấy gói tháng", 404, false);

            try
            {
                _unitOfWork.MonthlySubscriptionRepo.Delete(subscription);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Xóa gói tháng thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi xóa gói tháng: {ex.Message}", 500, false);
            }
        }

        private async Task<(User? User, VehicleType? VehicleType, string? Status, ResponseDTO? Error)> ValidateSubscriptionAsync(
            Guid userId,
            Guid vehicleTypeId,
            string? licensePlate,
            DateTime startDate,
            DateTime endDate,
            decimal price,
            string? status,
            Guid? currentSubscriptionId)
        {
            if (userId == Guid.Empty) return (null, null, null, new ResponseDTO("Vui lòng chọn cư dân", 400, false));
            if (vehicleTypeId == Guid.Empty) return (null, null, null, new ResponseDTO("Vui lòng chọn loại phương tiện", 400, false));
            if (string.IsNullOrWhiteSpace(licensePlate)) return (null, null, null, new ResponseDTO("Vui lòng nhập biển số", 400, false));
            if (endDate <= startDate) return (null, null, null, new ResponseDTO("Ngày kết thúc phải sau ngày bắt đầu", 400, false));
            if (price < 0) return (null, null, null, new ResponseDTO("Giá gói tháng không được âm", 400, false));

            var normalizedStatus = NormalizeStatus(status);
            if (normalizedStatus == null) return (null, null, null, new ResponseDTO("Trạng thái gói tháng chỉ được là Active, Inactive, Expired hoặc Cancelled", 400, false));

            var user = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(userId);
            if (user == null) return (null, null, null, new ResponseDTO("Người dùng không tồn tại", 400, false));

            var vehicleType = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(vehicleTypeId);
            if (vehicleType == null) return (null, null, null, new ResponseDTO("Loại phương tiện không tồn tại", 400, false));

            var plate = NormalizePlate(licensePlate);
            var duplicate = await _unitOfWork.MonthlySubscriptionRepo.GetAll()
                .AnyAsync(s => s.LicensePlate.ToLower() == plate.ToLower() && (!currentSubscriptionId.HasValue || s.SubscriptionId != currentSubscriptionId.Value));
            if (duplicate) return (null, null, null, new ResponseDTO("Biển số đã có gói tháng", 400, false));

            return (user, vehicleType, normalizedStatus, null);
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

        private static MonthlySubscriptionDTO MapToDTO(MonthlySubscription subscription)
        {
            return new MonthlySubscriptionDTO
            {
                SubscriptionId = subscription.SubscriptionId,
                UserId = subscription.UserId,
                UserFullName = subscription.User?.FullName,
                VehicleTypeId = subscription.VehicleTypeId,
                VehicleTypeName = subscription.VehicleType?.TypeName,
                LicensePlate = subscription.LicensePlate,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                Price = subscription.Price,
                Status = subscription.Status
            };
        }
    }
}
