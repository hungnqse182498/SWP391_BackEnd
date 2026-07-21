using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Subscription;
using Common.Enums;
using DAL.Interfaces;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BLL.Implements
{
    public class VehicleChangeRequestService : IVehicleChangeRequestService
    {
        private readonly IUnitOfWork _unitOfWork;

        public VehicleChangeRequestService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> CreateVehicleChangeAsync(Guid userId, CreateVehicleChangeDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu không hợp lệ", 400);

            if (dto.Reason?.Trim().Length > 500)
                return new ResponseDTO("Lý do không được vượt quá 500 ký tự", 400, false);

            var validation = await ValidateRequestAsync(userId, dto.SubscriptionId, dto.NewLicensePlate, null);
            if (validation.Error != null) return validation.Error;

            bool hasPending = await _unitOfWork.VehicleChangeRequestRepo.AnyAsync(x =>
                x.SubscriptionId == dto.SubscriptionId &&
                x.Status == VehicleChangeStatusEnum.Pending.ToString());

            if (hasPending)
                return new ResponseDTO("Gói vé tháng này hiện đang có một yêu cầu đổi biển số chờ xử lý", 409, false);

            if (validation.Subscription!.LicensePlate == validation.NormalizedPlate)
                return new ResponseDTO("Biển số mới không được trùng với biển số hiện tại", 400);

            var request = new VehicleChangeRequest
            {
                RequestId = Guid.NewGuid(),
                SubscriptionId = validation.Subscription!.SubscriptionId,
                OldLicensePlate = validation.Subscription.LicensePlate,
                NewLicensePlate = validation.NormalizedPlate!,
                Reason = dto.Reason?.Trim(),
                Status = VehicleChangeStatusEnum.Pending.ToString(),
                CreatedAt = DateTime.UtcNow
            };  
            try
            {
                await _unitOfWork.VehicleChangeRequestRepo.AddAsync(request);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Đã gửi yêu cầu đổi xe thành công", 201, true, MapToDTO(request, validation.Subscription));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi gửi yêu cầu: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> GetVehicleChangeRequestsAsync()
        {
            var list = await _unitOfWork.VehicleChangeRequestRepo.GetRequestsWithDetailsAsync();

            var dtos = list.Select(x => MapToDTO(x, x.Subscription)).ToList();
            return new ResponseDTO("Lấy danh sách yêu cầu đổi xe thành công", 200, true, dtos);
        }

        public async Task<ResponseDTO> GetVehicleChangeRequestByIdAsync(Guid id, Guid requesterId, bool canManage)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập RequestId", 400, false);

            var request = await _unitOfWork.VehicleChangeRequestRepo.GetByIdWithDetailsAsync(id);
            if (request == null) return new ResponseDTO("Không tồn tại yêu cầu này", 404, false);

            if (!canManage && request.Subscription?.UserId != requesterId)
                return new ResponseDTO("Bạn không có quyền xem yêu cầu này", 403, false);

            return new ResponseDTO("Lấy thông tin chi tiết yêu cầu thành công", 200, true, MapToDTO(request, request.Subscription));
        }

        public async Task<ResponseDTO> GetRequestsByUserIdAsync(Guid userId)
        {
            if (userId == Guid.Empty) return new ResponseDTO("UserId không hợp lệ", 400, false);

            var list = await _unitOfWork.VehicleChangeRequestRepo.GetRequestsByUserIdAsync(userId);

            var dtos = list.Select(x => MapToDTO(x, x.Subscription)).ToList();
            return new ResponseDTO("Lấy danh sách yêu cầu của user thành công", 200, true, dtos);
        }

        public async Task<ResponseDTO> UpdateVehicleChangeRequestAsync(Guid id, Guid userId, UpdateVehicleChangeDTO dto)
        {
            if (id == Guid.Empty || dto == null) return new ResponseDTO("Dữ liệu không hợp lệ", 400, false);

            var request = await _unitOfWork.VehicleChangeRequestRepo.GetByIdAsync(id);
            if (request == null) return new ResponseDTO("Không tồn tại yêu cầu này", 404, false);

            if (request.Status != VehicleChangeStatusEnum.Pending.ToString())
                return new ResponseDTO("Chỉ có thể chỉnh sửa yêu cầu đang ở trạng thái chờ duyệt", 400, false);

            if (dto.Reason?.Trim().Length > 500)
                return new ResponseDTO("Lý do không được vượt quá 500 ký tự", 400, false);

            var validation = await ValidateRequestAsync(userId, request.SubscriptionId, dto.NewLicensePlate, id);
            if (validation.Error != null) return validation.Error;

            if (validation.Subscription!.LicensePlate == validation.NormalizedPlate)
                return new ResponseDTO("Biển số mới không được trùng với biển số hiện tại", 400, false);

            request.NewLicensePlate = validation.NormalizedPlate!;
            request.Reason = dto.Reason?.Trim();

            try
            {
                await _unitOfWork.VehicleChangeRequestRepo.UpdateAsync(request);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Cập nhật thông tin yêu cầu đổi xe thành công", 200, true, MapToDTO(request, validation.Subscription));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi khi cập nhật yêu cầu: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> ApproveVehicleChangeAsync(Guid id, Guid handlerId)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập RequestId", 400, false);
            if (handlerId == Guid.Empty) return new ResponseDTO("Không xác định được người xử lý", 401, false);
            var request = await _unitOfWork.VehicleChangeRequestRepo.GetByIdAsync(id);
            if (request == null) return new ResponseDTO("Không tồn tại", 404);
            if (request.Status != VehicleChangeStatusEnum.Pending.ToString()) return new ResponseDTO("Yêu cầu đã được xử lý", 400);

            var sub = await _unitOfWork.MonthlySubscriptionRepo.GetByIdAsync(request.SubscriptionId);
            if (sub == null) return new ResponseDTO("Không tìm thấy gói", 404);

            if (!string.Equals(NormalizePlate(sub.LicensePlate), NormalizePlate(request.OldLicensePlate), StringComparison.Ordinal))
                return new ResponseDTO("Biển số của gói đã thay đổi sau khi yêu cầu được tạo. Vui lòng từ chối yêu cầu cũ và tạo yêu cầu mới", 409, false);

            if (await HasUsablePlateAsync(request.NewLicensePlate, sub.SubscriptionId))
                return new ResponseDTO("Biển số xe mới này hiện đang được sử dụng ở một gói khác", 400);

            sub.LicensePlate = NormalizePlate(request.NewLicensePlate);
            request.Status = VehicleChangeStatusEnum.Approved.ToString();
            request.ProcessedAt = DateTime.UtcNow;
            request.HandledByStaffId = handlerId;
            request.RejectionReason = null;

            try
            {
                await _unitOfWork.MonthlySubscriptionRepo.UpdateAsync(sub);
                await _unitOfWork.VehicleChangeRequestRepo.UpdateAsync(request);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Duyệt đổi biển số xe thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi khi duyệt yêu cầu: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> RejectVehicleChangeAsync(Guid id, Guid handlerId, RejectVehicleChangeDTO dto)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập RequestId", 400, false);
            if (handlerId == Guid.Empty) return new ResponseDTO("Không xác định được người xử lý", 401, false);
            var request = await _unitOfWork.VehicleChangeRequestRepo.GetByIdAsync(id);
            if (request == null) return new ResponseDTO("Không tồn tại", 404);
            if (request.Status != VehicleChangeStatusEnum.Pending.ToString()) 
                return new ResponseDTO("Yêu cầu đã được xử lý", 400);
            if (dto == null || string.IsNullOrWhiteSpace(dto.Reason))
                return new ResponseDTO("Vui lòng nhập lý do từ chối đơn", 400, false);
            if (dto.Reason.Trim().Length > 500)
                return new ResponseDTO("Lý do từ chối không được vượt quá 500 ký tự", 400, false);

            request.Status = VehicleChangeStatusEnum.Rejected.ToString();
            request.RejectionReason = dto.Reason.Trim();
            request.ProcessedAt = DateTime.UtcNow;
            request.HandledByStaffId = handlerId;

            try
            {
                await _unitOfWork.VehicleChangeRequestRepo.UpdateAsync(request);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Từ chối yêu cầu thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi khi từ chối yêu cầu: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> DeleteVehicleChangeRequestAsync(Guid id, Guid userId)
        {
            if (id == Guid.Empty) return new ResponseDTO("Mã yêu cầu không hợp lệ", 400, false);

            var request = await _unitOfWork.VehicleChangeRequestRepo.GetByIdAsync(id);
            if (request == null) return new ResponseDTO("Không tìm thấy yêu cầu này", 404, false);

            var sub = await _unitOfWork.MonthlySubscriptionRepo.GetByIdAsync(request.SubscriptionId);
            if (sub != null && userId != Guid.Empty && sub.UserId != userId)
            {
                return new ResponseDTO("Bạn không có quyền hủy yêu cầu này", 403, false);
            }

            if (request.Status != VehicleChangeStatusEnum.Pending.ToString())
            {
                return new ResponseDTO("Không thể xóa yêu cầu đã qua xử lý", 400, false);
            }

            try
            {
                await _unitOfWork.VehicleChangeRequestRepo.DeleteAsync(id);
                return new ResponseDTO("Hủy và xóa đơn yêu cầu đổi xe thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi khi hủy yêu cầu: {ex.Message}", 500, false);
            }
        }

        private async Task<(MonthlySubscription? Subscription, string? NormalizedPlate, ResponseDTO? Error)> ValidateRequestAsync(
            Guid userId, Guid subscriptionId, string? rawPlate, Guid? currentRequestId)
        {
            if (subscriptionId == Guid.Empty)
                return (null, null, new ResponseDTO("Vui lòng chọn gói vé tháng cần đổi", 400, false));

            var sub = await _unitOfWork.MonthlySubscriptionRepo.GetByIdAsync(subscriptionId);
            if (sub == null)
                return (null, null, new ResponseDTO("Gói vé tháng không tồn tại trên hệ thống", 404, false));

            if (userId != Guid.Empty && sub.UserId != userId)
                return (null, null, new ResponseDTO("Bạn không có quyền gửi yêu cầu cho gói này", 403, false));

            var now = DateTime.UtcNow;
            if (sub.Status != MonthlySubscriptionStatus.Active.ToString() || sub.StartDate > now || sub.EndDate < now)
                return (null, null, new ResponseDTO("Chỉ có thể đổi biển số cho gói đang hoạt động và còn hiệu lực", 400, false));

            var normalizedPlate = NormalizePlate(rawPlate);
            if (string.IsNullOrWhiteSpace(normalizedPlate))
                return (null, null, new ResponseDTO("Vui lòng nhập biển số xe mới hợp lệ", 400, false));
            if (normalizedPlate.Length > 15 || !Regex.IsMatch(normalizedPlate, "^[A-Z0-9.-]{4,15}$"))
                return (null, null, new ResponseDTO("Biển số chỉ gồm 4-15 chữ cái, chữ số, dấu chấm hoặc dấu gạch ngang", 400, false));

            if (await HasUsablePlateAsync(normalizedPlate, sub.SubscriptionId))
                return (null, null, new ResponseDTO("Biển số xe mới này hiện đang được sử dụng ở một gói khác", 400, false));

            return (sub, normalizedPlate, null);
        }

        private async Task<bool> HasUsablePlateAsync(string plate, Guid? ignoredSubscriptionId = null)
        {
            return await _unitOfWork.MonthlySubscriptionRepo.HasUsablePlateAsync(plate, ignoredSubscriptionId);
        }

        private static string NormalizePlate(string? plate)
        {
            return string.IsNullOrWhiteSpace(plate) ? string.Empty : plate.Trim().ToUpperInvariant();
        }

        private static VehicleChangeRequestDTO MapToDTO(VehicleChangeRequest request, MonthlySubscription? sub)
        {
            return new VehicleChangeRequestDTO
            {
                RequestId = request.RequestId,
                SubscriptionId = request.SubscriptionId,
                OldLicensePlate = request.OldLicensePlate,
                NewLicensePlate = request.NewLicensePlate,
                Reason = request.Reason,
                RejectionReason = request.RejectionReason,
                Status = request.Status,
                CreatedAt = request.CreatedAt,
                ProcessedAt = request.ProcessedAt,
                UserFullName = sub?.User?.FullName,
                PackageName = sub?.Package?.PackageName,
                HandledByStaffId = request.HandledByStaffId,
                HandledByFullName = request.HandledByStaff?.FullName
            };
        }
    }
}
