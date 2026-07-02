using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Payment;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Success",
            "Pending",
            "Failed"
        };

    public PaymentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }


    public async Task PayOSWebhookAsync(PayOSWebhookDTO dto)
    {
        if (dto?.Data == null) return;

        var orderCode = dto.Data.OrderCode.ToString();

        var payment = await _unitOfWork.PaymentRepo
            .FirstOrDefaultAsync(x => x.TransactionReference == orderCode);

        if (payment == null) return;

        if (payment.PaymentStatus == PaymentStatus.Success.ToString())
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
        if (payment.PaymentStatus != PaymentStatus.Success.ToString())
            return;

        switch (payment.PaymentType)
        {
            case var t when t == PaymentType.Deposit.ToString():
                await HandleReservationAsync(payment);
                break;

            case var t when t == PaymentType.SubscriptionFee.ToString():
                await ActivateSubscriptionAsync(payment.SubscriptionId!.Value);
                break;

            case var t when t == PaymentType.SubscriptionRenewal.ToString():
                await CompleteRenewalAsync(payment);
                break;
        }
    }

    //3
    private async Task HandleReservationAsync(Payment payment)
    {
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
        var subscription = await _unitOfWork.MonthlySubscriptionRepo.GetAll()
            .Include(s => s.Package)
            .Include(s => s.User)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);
        
        if (subscription == null) return;

        subscription.Status = "Active";

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
            var slot = await _unitOfWork.ParkingSlotRepo.GetAll()
                .Where(x => x.VehicleTypeId == subscription.VehicleTypeId && x.Status == "Available")
                .OrderBy(x => x.SlotCode)
                .FirstOrDefaultAsync();

            if (slot != null)
            {
                slot.Status = "Reserved";
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

        var months = Math.Max(1, (int)Math.Round(payment.Amount / subscription.Price, MidpointRounding.AwayFromZero));
        var oldEnd = subscription.EndDate;
        var start = subscription.EndDate < DateTime.Now ? DateTime.Now : subscription.EndDate;
        var newEnd = start.AddMonths(months);

        subscription.EndDate = newEnd;
        subscription.Status = "Active";

        var renewal = new SubscriptionRenewal
        {
            RenewalId = Guid.NewGuid(),
            SubscriptionId = subscription.SubscriptionId,
            OldEndDate = oldEnd,
            NewEndDate = newEnd,
            Amount = payment.Amount,
            RenewalDate = DateTime.Now
        };

        await _unitOfWork.SubscriptionRenewalRepo.AddAsync(renewal);
        await _unitOfWork.MonthlySubscriptionRepo.UpdateAsync(subscription);
    }

    public async Task<ResponseDTO> GetAllAsync()
    {
        var payments = await _unitOfWork.PaymentRepo.GetAll()
            .OrderByDescending(p => p.PaymentTime)
            .ToListAsync();

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

        var validation = await ValidatePaymentAsync(dto.SessionId, dto.ReservationId, dto.Amount, dto.PaymentMethod, dto.PaymentStatus ?? "Success");
        if (validation.Error != null) return validation.Error;

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            SessionId = dto.SessionId,
            ReservationId = dto.ReservationId,
            Amount = dto.Amount,
            PaymentMethod = dto.PaymentMethod.Trim(),
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

        var validation = await ValidatePaymentAsync(dto.SessionId, dto.ReservationId, dto.Amount, dto.PaymentMethod, dto.PaymentStatus);
        if (validation.Error != null) return validation.Error;

        payment.SessionId = dto.SessionId;
        payment.ReservationId = dto.ReservationId;
        payment.Amount = dto.Amount;
        payment.PaymentMethod = dto.PaymentMethod.Trim();
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

    private async Task<(string? Status, ResponseDTO? Error)> ValidatePaymentAsync(Guid sessionId, Guid? reservationId, decimal amount, string? paymentMethod, string? status)
    {
        if (sessionId == Guid.Empty) return (null, new ResponseDTO("Vui lòng chọn phiên gửi xe", 400, false));
        if (amount < 0) return (null, new ResponseDTO("Số tiền thanh toán không được âm", 400, false));
        if (string.IsNullOrWhiteSpace(paymentMethod)) return (null, new ResponseDTO("Vui lòng nhập phương thức thanh toán", 400, false));

        var normalizedStatus = NormalizeStatus(status);
        if (normalizedStatus == null) return (null, new ResponseDTO("Trạng thái thanh toán chỉ được là Pending, Success hoặc Failed", 400, false));

        var sessionExists = await _unitOfWork.ParkingSessionRepo.AnyAsync(s => s.SessionId == sessionId);
        if (!sessionExists) return (null, new ResponseDTO("Phiên gửi xe không tồn tại", 400, false));

        if (reservationId.HasValue)
        {
            var reservationExists = await _unitOfWork.ReservationRepo.AnyAsync(r => r.ReservationId == reservationId.Value);
            if (!reservationExists) return (null, new ResponseDTO("Đặt chỗ không tồn tại", 400, false));
        }

        return (normalizedStatus, null);
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        return ValidStatuses.FirstOrDefault(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task CompleteCheckoutSessionIfNeededAsync(Payment payment)
    {
        if (!payment.SessionId.HasValue) return;
        if (!string.Equals(payment.PaymentType, PaymentType.CheckoutFee.ToString(), StringComparison.OrdinalIgnoreCase)) return;

        var session = await _unitOfWork.ParkingSessionRepo.GetByIdAsync(payment.SessionId.Value);
        if (session == null || session.Status == "Completed") return;

        session.ExitTime ??= payment.PaymentTime;
        session.LicensePlateOut = string.IsNullOrWhiteSpace(session.LicensePlateOut) ? session.LicensePlateIn : session.LicensePlateOut;
        session.Status = "Completed";

        if (session.ActualSlotId.HasValue)
        {
            var actualSlot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(session.ActualSlotId.Value);
            if (actualSlot != null)
            {
                actualSlot.Status = "Available";
                await _unitOfWork.ParkingSlotRepo.UpdateAsync(actualSlot);
            }
        }

        if (session.AssignedSlotId.HasValue && session.AssignedSlotId != session.ActualSlotId)
        {
            var assignedSlot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(session.AssignedSlotId.Value);
            if (assignedSlot != null)
            {
                assignedSlot.Status = "Available";
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
            SessionId = payment.SessionId,
            ReservationId = payment.ReservationId,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            PaymentTime = payment.PaymentTime,
            PaymentStatus = payment.PaymentStatus,
            TransactionReference = payment.TransactionReference
        };
    }
}
