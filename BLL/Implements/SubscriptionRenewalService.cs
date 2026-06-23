using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Subscription;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;


public class SubscriptionRenewalService : ISubscriptionRenewalService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPayOSService _payOSService;

    public SubscriptionRenewalService(IUnitOfWork unitOfWork, IPayOSService payOSService)
    {
        _unitOfWork = unitOfWork;
        _payOSService = payOSService;
    }

    public async Task<ResponseDTO> RenewAsync(Guid subscriptionId, Guid userId, RenewSubscriptionDTO dto)
    {
        if (dto == null || dto.Months <= 0)
            return new ResponseDTO("Số tháng gia hạn phải lớn hơn 0", 400, false);

        var subscription = await _unitOfWork.MonthlySubscriptionRepo.GetDetailAsync(subscriptionId);
        if (subscription == null)
            return new ResponseDTO("Không tìm thấy gói tháng", 404, false);

        if (userId != Guid.Empty && subscription.UserId != userId)
            return new ResponseDTO("Bạn không có quyền gia hạn gói này", 403, false);

        if (subscription.Status == "Cancelled")
            return new ResponseDTO("Không thể gia hạn gói đã hủy", 400, false);

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            SubscriptionId = subscription.SubscriptionId,
            Amount = subscription.Price * dto.Months,
            PaymentMethod = PaymentMethod.PayOS.ToString(),
            PaymentStatus = PaymentStatus.Pending.ToString(),
            PaymentType = PaymentType.SubscriptionRenewal.ToString(),
            PaymentTime = DateTime.UtcNow,
            TransactionReference = string.Empty
        };

        try
        {
            await _unitOfWork.PaymentRepo.AddAsync(payment);

            var paymentUrl = await _payOSService.CreatePaymentLinkAsync(payment);
            string paymentLinkId = "";
            if (!string.IsNullOrEmpty(paymentUrl))
            {
                paymentLinkId = paymentUrl.Substring(paymentUrl.LastIndexOf('/') + 1);
            }

            await _unitOfWork.SaveAsync();

            var resultData = new RegisterMonthlySubscriptionPaymentDTO
            {
                SubscriptionId = subscription.SubscriptionId,
                PaymentId = payment.PaymentId,
                Amount = payment.Amount,
                PaymentLinkId = paymentLinkId,
                PaymentUrl = paymentUrl,
                OrderCode = payment.TransactionReference
            };

            return new ResponseDTO("Tạo thanh toán gia hạn thành công", 201, true, resultData);
        }
        catch (Exception ex)
        {
            return new ResponseDTO($"Lỗi tạo thanh toán gia hạn: {ex.Message}", 500, false);
        }
    }

    public async Task<ResponseDTO> GetRenewalsAsync(Guid subscriptionId)
    {
        if (subscriptionId == Guid.Empty)
            return new ResponseDTO("Vui lòng nhập SubscriptionId", 400, false);
        var list = await _unitOfWork.SubscriptionRenewalRepo.GetBySubscriptionIdAsync(subscriptionId);
        var dtos = list.Select(MapToDTO).ToList();
        return new ResponseDTO("Lịch sử gia hạn", 200, true, dtos);
    }

    public async Task<ResponseDTO> GetAllAsync()
    {
        var renewals = await _unitOfWork.SubscriptionRenewalRepo.GetAllWithSubscriptionAsync();
        var renewalList = renewals.ToList();

        if (renewalList.Count == 0)
        {
            return new ResponseDTO("Không tìm thấy lịch sử gia hạn nào", 404, false);
        }

        var dtos = renewalList.Select(MapToDTO).ToList();
        return new ResponseDTO("Lấy danh sách lịch sử gia hạn thành công", 200, true, dtos);
    }

    public async Task<ResponseDTO> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty)
            return new ResponseDTO("Vui lòng nhập RenewalId", 400, false);

        var renewal = await _unitOfWork.SubscriptionRenewalRepo.GetByIdWithSubscriptionAsync(id);

        if (renewal == null)
            return new ResponseDTO("Không tìm thấy lịch sử gia hạn", 404, false);

        return new ResponseDTO("Lấy thông tin lịch sử gia hạn thành công", 200, true, MapToDTO(renewal));
    }

    public async Task<ResponseDTO> CreateDirectAsync(CreateDirectRenewalDTO dto)
    {
        if (dto == null)
            return new ResponseDTO("Dữ liệu tạo lịch sử gia hạn không hợp lệ", 400, false);

        var validation = await ValidateRenewalAsync(dto.SubscriptionId, dto.Months, dto.Amount);
        if (validation.Error != null)
            return validation.Error;

        var sub = validation.Subscription!;
        DateTime oldEndDate = sub.EndDate;

        DateTime baseDate = sub.EndDate > DateTime.UtcNow ? sub.EndDate : DateTime.UtcNow;
        DateTime newEndDate = baseDate.AddMonths(dto.Months);

        var renewal = new SubscriptionRenewal
        {
            RenewalId = Guid.NewGuid(),
            SubscriptionId = dto.SubscriptionId,
            OldEndDate = oldEndDate,
            NewEndDate = newEndDate,
            Amount = dto.Amount,
            RenewalDate = DateTime.UtcNow
        };

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _unitOfWork.SubscriptionRenewalRepo.AddAsync(renewal);

            sub.EndDate = newEndDate;
            sub.Status = "Active";
            await _unitOfWork.MonthlySubscriptionRepo.UpdateAsync(sub);

            await _unitOfWork.SaveChangeAsync();
            await _unitOfWork.CommitTransactionAsync();

            renewal.Subscription = sub;
            return new ResponseDTO("Tạo gia hạn thành công", 201, true, MapToDTO(renewal));
        }
        catch (DbUpdateException)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return new ResponseDTO("Dữ liệu gia hạn bị trùng lặp hoặc không hợp lệ", 400, false);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return new ResponseDTO($"Lỗi tạo gia hạn: {ex.Message}", 500, false);
        }
    }

    public async Task<ResponseDTO> UpdateAsync(UpdateRenewalDTO dto)
    {
        if (dto == null || dto.RenewalId == Guid.Empty)
            return new ResponseDTO("Dữ liệu cập nhật không hợp lệ", 400, false);

        var renewal = await _unitOfWork.SubscriptionRenewalRepo.GetByIdWithSubscriptionAsync(dto.RenewalId);

        if (renewal == null)
            return new ResponseDTO("Không tìm thấy thông tin lịch sử gia hạn", 404, false);

        if (dto.Amount < 0)
            return new ResponseDTO("Số tiền chỉnh sửa không được âm", 400, false);

        renewal.Amount = dto.Amount;
        if (dto.RenewalDate.HasValue)
        {
            renewal.RenewalDate = dto.RenewalDate.Value;
        }

        try
        {
            await _unitOfWork.SubscriptionRenewalRepo.UpdateAsync(renewal);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO("Cập nhật lịch sử gia hạn thành công", 200, true, MapToDTO(renewal));
        }
        catch (DbUpdateException)
        {
            return new ResponseDTO("Dữ liệu cập nhật bị trùng hoặc không hợp lệ", 400, false);
        }
        catch (Exception ex)
        {
            return new ResponseDTO($"Lỗi cập nhật lịch sử: {ex.Message}", 500, false);
        }
    }

    public async Task<ResponseDTO> DeleteAsync(Guid id)
    {
        if (id == Guid.Empty)
            return new ResponseDTO("Vui lòng nhập RenewalId", 400, false);

        var renewal = await _unitOfWork.SubscriptionRenewalRepo.GetByIdAsync(id);
        if (renewal == null)
            return new ResponseDTO("Không tìm thấy lịch sử gia hạn", 404, false);

        var sub = await _unitOfWork.MonthlySubscriptionRepo.GetByIdAsync(renewal.SubscriptionId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (sub != null)
            {
                if (sub.EndDate == renewal.NewEndDate)
                {
                    sub.EndDate = renewal.OldEndDate;
                    if (sub.EndDate < DateTime.UtcNow) sub.Status = "Expired";
                }
                else
                {
                    var diffMonths = ((renewal.NewEndDate.Year - renewal.OldEndDate.Year) * 12) + renewal.NewEndDate.Month - renewal.OldEndDate.Month;
                    sub.EndDate = sub.EndDate.AddMonths(-diffMonths);
                }
                await _unitOfWork.MonthlySubscriptionRepo.UpdateAsync(sub);
            }

            _unitOfWork.SubscriptionRenewalRepo.Delete(renewal);
            await _unitOfWork.SaveChangeAsync();
            await _unitOfWork.CommitTransactionAsync();

            return new ResponseDTO("Xóa lịch sử gia hạn và phục hồi lại hạn dùng cũ thành công", 200, true);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return new ResponseDTO($"Lỗi xóa lịch sử gia hạn: {ex.Message}", 500, false);
        }
    }

    private async Task<(MonthlySubscription? Subscription, ResponseDTO? Error)> ValidateRenewalAsync(Guid subscriptionId, int months, decimal amount)
    {
        if (subscriptionId == Guid.Empty)
            return (null, new ResponseDTO("Vui lòng nhập SubscriptionId", 400, false));

        if (months <= 0)
            return (null, new ResponseDTO("Số tháng gia hạn phải lớn hơn 0", 400, false));

        if (amount < 0)
            return (null, new ResponseDTO("Số tiền thanh toán không được nhỏ hơn 0", 400, false));

        var subscription = await _unitOfWork.MonthlySubscriptionRepo.GetByIdAsync(subscriptionId);
        if (subscription == null)
            return (null, new ResponseDTO("Gói tháng của cư dân không tồn tại", 400, false));

        if (subscription.Status == "Cancelled")
            return (null, new ResponseDTO("Không thể gia hạn gói tháng đã hủy", 400, false));

        return (subscription, null);
    }

    private static SubscriptionRenewalDTO MapToDTO(SubscriptionRenewal renewal)
    {
        return new SubscriptionRenewalDTO
        {
            RenewalId = renewal.RenewalId,
            SubscriptionId = renewal.SubscriptionId,
            OldEndDate = renewal.OldEndDate,
            NewEndDate = renewal.NewEndDate,
            Amount = renewal.Amount,
            RenewalDate = renewal.RenewalDate
        };
    }
}