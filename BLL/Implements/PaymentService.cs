using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Payment;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;

namespace BLL.Implements;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;

    public PaymentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }


    public async Task PayOSWebhookAsync(PayOSWebhookDTO dto)
    {
        if (dto?.Data == null) return;

        var orderCode = dto.Data.OrderCode.ToString();

        var payment = await _unitOfWork.PaymentRepo.GetByOrderCodeAsync(orderCode);

        if (payment == null) return;

        if (string.Equals(payment.PaymentStatus, PaymentStatus.Success.ToString(), StringComparison.OrdinalIgnoreCase))
            return;

        payment.PaymentStatus = dto.Code == "00"
            ? PaymentStatus.Success.ToString()
            : PaymentStatus.Failed.ToString();

        payment.PaymentTime = DateTime.UtcNow;

        await _unitOfWork.PaymentRepo.UpdateAsync(payment);
        await DispatchPaymentAsync(payment);
        await _unitOfWork.SaveAsync();
    }

    //2
    private async Task DispatchPaymentAsync(Payment payment)
    {
        if (!Enum.TryParse<PaymentStatus>(payment.PaymentStatus, true, out var status) ||
            status != PaymentStatus.Success)
            return;

        if (!Enum.TryParse<PaymentType>(payment.PaymentType, true, out var paymentType))
            return;

        switch (paymentType)
        {
            case PaymentType.Deposit:
                await HandleReservationAsync(payment);
                break;

            case PaymentType.SubscriptionFee when payment.SubscriptionId.HasValue:
                await ActivateSubscriptionAsync(payment.SubscriptionId.Value);
                break;

            case PaymentType.SubscriptionRenewal when payment.SubscriptionId.HasValue:
                await CompleteRenewalAsync(payment);
                break;

            case PaymentType.CheckoutFee:
                await CompleteCheckoutSessionIfNeededAsync(payment);
                break;
        }
    }

    //3
    private async Task HandleReservationAsync(Payment payment)
    {
        if (!payment.ReservationId.HasValue) return;

        var reservation = await _unitOfWork.ReservationRepo
            .GetByIdAsync(payment.ReservationId.Value);

        if (reservation != null)
        {
            reservation.Status = ReservationStatus.Confirmed.ToString();
            await _unitOfWork.ReservationRepo.UpdateAsync(reservation);
        }
    }

    //4
    private async Task ActivateSubscriptionAsync(Guid subscriptionId)
    {
        var subscription = await _unitOfWork.MonthlySubscriptionRepo.GetActivationDetailAsync(subscriptionId);
        
        if (subscription == null) return;

        subscription.Status = MonthlySubscriptionStatus.Active.ToString();
        subscription.StartDate = DateTime.Now;
        subscription.EndDate = DateTime.Now.AddMonths(subscription.Package.DurationMonths);

        if (subscription.User != null && subscription.User.Role?.RoleName == "User")
        {
            var customerRole = await _unitOfWork.UserRepo.GetRoleByNameAsync("Customer");

            if (customerRole != null)
            {
                subscription.User.RoleId = customerRole.RoleId;
            }
        }

        if (subscription.Package.RequireFixedSlot == true && !subscription.FixedSlotId.HasValue)
        {
            var slot = await _unitOfWork.ParkingSlotRepo.GetFirstAvailableByVehicleTypeAsync(subscription.VehicleTypeId);

            if (slot != null)
            {
                slot.Status = ParkingSlotStatus.Reserved.ToString();
                slot.AssignedUserId = subscription.UserId;
                subscription.FixedSlotId = slot.SlotId;
                await _unitOfWork.ParkingSlotRepo.UpdateAsync(slot);
            }
        }

        await _unitOfWork.MonthlySubscriptionRepo.UpdateAsync(subscription);
    }

    //5
    private async Task CompleteRenewalAsync(Payment payment)
    {
        var subscription = await _unitOfWork.MonthlySubscriptionRepo.GetByIdAsync(payment.SubscriptionId!.Value);
        if (subscription == null || subscription.Price <= 0) return;

        var package = await _unitOfWork.SubscriptionPackageRepo.GetByIdAsync(subscription.PackageId);
        if (package == null) return;
        
        var oldEnd = subscription.EndDate;
        var start = subscription.EndDate < DateTime.UtcNow ? DateTime.UtcNow : subscription.EndDate;
        var newEnd = start.AddMonths(package.DurationMonths);

        subscription.EndDate = newEnd;
        subscription.Status = MonthlySubscriptionStatus.Active.ToString();

        var renewal = new SubscriptionRenewal
        {
            RenewalId = Guid.NewGuid(),
            SubscriptionId = subscription.SubscriptionId,
            OldEndDate = oldEnd,
            NewEndDate = newEnd,
            Amount = payment.Amount,
            RenewalDate = DateTime.UtcNow
        };

        await _unitOfWork.SubscriptionRenewalRepo.AddAsync(renewal);
        await _unitOfWork.MonthlySubscriptionRepo.UpdateAsync(subscription);
    }

    public async Task<ResponseDTO> GetAllAsync()
    {
        var payments = await _unitOfWork.PaymentRepo.GetAllOrderedByPaymentTimeAsync();

        return new ResponseDTO("Lấy danh sách thanh toán thành công", 200, true, payments.Select(MapToDTO).ToList());
    }

    public async Task<ResponseDTO> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập PaymentId", 400, false);

        var payment = await _unitOfWork.PaymentRepo.GetByIdAsync(id);
        if (payment == null) return new ResponseDTO("Không tìm thấy thanh toán", 404, false);
        return new ResponseDTO("Lấy thông tin thanh toán thành công", 200, true, MapToDTO(payment));
    }

    public async Task<ResponseDTO> CreateAsync(CreatePaymentDTO dto)
    {
        if (dto == null) return new ResponseDTO("Dữ liệu tạo thanh toán không hợp lệ", 400, false);

        var validation = await ValidatePaymentAsync(
            dto.UserId,
            dto.SessionId,
            dto.ReservationId,
            dto.SubscriptionId,
            dto.Amount,
            dto.PaymentMethod,
            dto.PaymentStatus ?? PaymentStatus.Success.ToString(),
            dto.PaymentType);
        if (validation.Error != null) return validation.Error;

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            UserId = dto.UserId,
            SessionId = dto.SessionId,
            ReservationId = dto.ReservationId,
            SubscriptionId = dto.SubscriptionId,
            Amount = dto.Amount,
            PaymentMethod = validation.Method!,
            PaymentType = validation.Type!,
            PaymentTime = dto.PaymentTime ?? DateTime.UtcNow,
            PaymentStatus = validation.Status!,
            TransactionReference = string.IsNullOrWhiteSpace(dto.TransactionReference) ? null : dto.TransactionReference.Trim()
        };

        await _unitOfWork.PaymentRepo.AddAsync(payment);
        await _unitOfWork.SaveChangeAsync();

        return new ResponseDTO("Tạo thanh toán thành công", 201, true, MapToDTO(payment));
    }

    public async Task<ResponseDTO> UpdateAsync(UpdatePaymentDTO dto)
    {
        if (dto == null || dto.PaymentId == Guid.Empty) return new ResponseDTO("Dữ liệu cập nhật thanh toán không hợp lệ", 400, false);

        var payment = await _unitOfWork.PaymentRepo.GetByIdAsync(dto.PaymentId);
        if (payment == null) return new ResponseDTO("Không tìm thấy thanh toán", 404, false);

        var validation = await ValidatePaymentAsync(
            dto.UserId,
            dto.SessionId,
            dto.ReservationId,
            dto.SubscriptionId,
            dto.Amount,
            dto.PaymentMethod,
            dto.PaymentStatus,
            dto.PaymentType ?? payment.PaymentType);
        if (validation.Error != null) return validation.Error;

        payment.UserId = dto.UserId;
        payment.SessionId = dto.SessionId;
        payment.ReservationId = dto.ReservationId;
        payment.SubscriptionId = dto.SubscriptionId;
        payment.Amount = dto.Amount;
        payment.PaymentMethod = validation.Method!;
        payment.PaymentType = validation.Type!;
        payment.PaymentTime = dto.PaymentTime;
        payment.PaymentStatus = validation.Status!;
        payment.TransactionReference = string.IsNullOrWhiteSpace(dto.TransactionReference) ? null : dto.TransactionReference.Trim();

        await _unitOfWork.PaymentRepo.UpdateAsync(payment);
        await _unitOfWork.SaveChangeAsync();

        return new ResponseDTO("Cập nhật thanh toán thành công", 200, true, MapToDTO(payment));
    }

    public async Task<ResponseDTO> DeleteAsync(Guid id)
    {
        if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập PaymentId", 400, false);

        var payment = await _unitOfWork.PaymentRepo.GetByIdAsync(id);
        if (payment == null) return new ResponseDTO("Không tìm thấy thanh toán", 404, false);

        try
        {
            _unitOfWork.PaymentRepo.Delete(payment);
            await _unitOfWork.SaveChangeAsync();
            return new ResponseDTO("Xóa thanh toán thành công", 200, true);
        }
        catch (Exception ex)
        {
            return new ResponseDTO($"Lỗi xóa thanh toán: {ex.Message}", 500, false);
        }
    }

    private async Task<(string? Method, string? Status, string? Type, ResponseDTO? Error)> ValidatePaymentAsync(
        Guid? userId,
        Guid? sessionId,
        Guid? reservationId,
        Guid? subscriptionId,
        decimal amount,
        string? paymentMethod,
        string? status,
        string? type)
    {
        if (amount < 0) return (null, null, null, new ResponseDTO("Số tiền thanh toán không được âm", 400, false));

        var normalizedMethod = NormalizeEnum<PaymentMethod>(paymentMethod);
        if (normalizedMethod == null) return (null, null, null, new ResponseDTO("Phương thức thanh toán chỉ được là PayOS hoặc Cash", 400, false));

        var normalizedStatus = NormalizeEnum<PaymentStatus>(status);
        if (normalizedStatus == null) return (null, null, null, new ResponseDTO("Trạng thái thanh toán chỉ được là Pending, Success hoặc Failed", 400, false));

        var normalizedType = NormalizeEnum<PaymentType>(string.IsNullOrWhiteSpace(type) ? PaymentType.CheckoutFee.ToString() : type);
        if (normalizedType == null) return (null, null, null, new ResponseDTO("Loại thanh toán không hợp lệ", 400, false));

        var paymentType = Enum.Parse<PaymentType>(normalizedType);
        var relationError = ValidateRequiredRelation(paymentType, sessionId, reservationId, subscriptionId);
        if (relationError != null) return (null, null, null, relationError);

        if (userId.HasValue)
        {
            if (userId.Value == Guid.Empty) return (null, null, null, new ResponseDTO("UserId không hợp lệ", 400, false));
            var userExists = await _unitOfWork.UserRepo.AnyAsync(u => u.UserId == userId.Value);
            if (!userExists) return (null, null, null, new ResponseDTO("Người dùng không tồn tại", 400, false));
        }

        if (sessionId.HasValue)
        {
            if (sessionId.Value == Guid.Empty) return (null, null, null, new ResponseDTO("SessionId không hợp lệ", 400, false));
            var sessionExists = await _unitOfWork.ParkingSessionRepo.AnyAsync(s => s.SessionId == sessionId.Value);
            if (!sessionExists) return (null, null, null, new ResponseDTO("Phiên gửi xe không tồn tại", 400, false));
        }

        if (reservationId.HasValue)
        {
            if (reservationId.Value == Guid.Empty) return (null, null, null, new ResponseDTO("ReservationId không hợp lệ", 400, false));
            var reservationExists = await _unitOfWork.ReservationRepo.AnyAsync(r => r.ReservationId == reservationId.Value);
            if (!reservationExists) return (null, null, null, new ResponseDTO("Đặt chỗ không tồn tại", 400, false));
        }

        if (subscriptionId.HasValue)
        {
            if (subscriptionId.Value == Guid.Empty) return (null, null, null, new ResponseDTO("SubscriptionId không hợp lệ", 400, false));
            var subscriptionExists = await _unitOfWork.MonthlySubscriptionRepo.AnyAsync(s => s.SubscriptionId == subscriptionId.Value);
            if (!subscriptionExists) return (null, null, null, new ResponseDTO("Gói tháng không tồn tại", 400, false));
        }

        return (normalizedMethod, normalizedStatus, normalizedType, null);
    }

    private static ResponseDTO? ValidateRequiredRelation(PaymentType paymentType, Guid? sessionId, Guid? reservationId, Guid? subscriptionId)
    {
        return paymentType switch
        {
            PaymentType.CheckoutFee when !sessionId.HasValue || sessionId.Value == Guid.Empty
                => new ResponseDTO("Vui lòng chọn phiên gửi xe", 400, false),
            PaymentType.Deposit when !reservationId.HasValue || reservationId.Value == Guid.Empty
                => new ResponseDTO("Vui lòng chọn đặt chỗ", 400, false),
            PaymentType.SubscriptionFee when !subscriptionId.HasValue || subscriptionId.Value == Guid.Empty
                => new ResponseDTO("Vui lòng chọn gói tháng", 400, false),
            PaymentType.SubscriptionRenewal when !subscriptionId.HasValue || subscriptionId.Value == Guid.Empty
                => new ResponseDTO("Vui lòng chọn gói tháng cần gia hạn", 400, false),
            _ => null
        };
    }

    private static string? NormalizeEnum<TEnum>(string? value) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Enum.TryParse<TEnum>(value.Trim(), true, out var parsed) ? parsed.ToString() : null;
    }

    private async Task CompleteCheckoutSessionIfNeededAsync(Payment payment)
    {
        if (!payment.SessionId.HasValue) return;
        if (!string.Equals(payment.PaymentType, PaymentType.CheckoutFee.ToString(), StringComparison.OrdinalIgnoreCase)) return;

        var session = await _unitOfWork.ParkingSessionRepo.GetByIdAsync(payment.SessionId.Value);
        if (session == null ||
            string.Equals(session.Status, SessionStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        session.ExitTime ??= payment.PaymentTime;
        session.LicensePlateOut = string.IsNullOrWhiteSpace(session.LicensePlateOut) ? session.LicensePlateIn : session.LicensePlateOut;
        session.Status = SessionStatus.Completed.ToString();

        if (session.ActualSlotId.HasValue)
        {
            var actualSlot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(session.ActualSlotId.Value);
            if (actualSlot != null)
            {
                actualSlot.Status = ParkingSlotStatus.Available.ToString();
                await _unitOfWork.ParkingSlotRepo.UpdateAsync(actualSlot);
            }
        }

        if (session.AssignedSlotId.HasValue && session.AssignedSlotId != session.ActualSlotId)
        {
            var assignedSlot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(session.AssignedSlotId.Value);
            if (assignedSlot != null)
            {
                assignedSlot.Status = ParkingSlotStatus.Available.ToString();
                await _unitOfWork.ParkingSlotRepo.UpdateAsync(assignedSlot);
            }
        }

        await _unitOfWork.ParkingSessionRepo.UpdateAsync(session);
    }

    private static PaymentDTO MapToDTO(Payment payment)
    {
        return new PaymentDTO
        {
            PaymentId = payment.PaymentId,
            UserId = payment.UserId,
            SessionId = payment.SessionId,
            ReservationId = payment.ReservationId,
            SubscriptionId = payment.SubscriptionId,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            PaymentType = payment.PaymentType,
            PaymentTime = payment.PaymentTime,
            PaymentStatus = payment.PaymentStatus,
            TransactionReference = payment.TransactionReference
        };
    }
}
