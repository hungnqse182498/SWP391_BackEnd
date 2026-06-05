using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Reservation;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

public class ReservationService : IReservationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPayOSService _payOSService;

    public ReservationService(IUnitOfWork unitOfWork, IPayOSService payOSService)
    {
        _unitOfWork = unitOfWork;
        _payOSService = payOSService;
    }

    public async Task<ResponseDTO> CreateReservationAsync(Guid userId, CreateReservationDTO dto)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (dto.ExpectedEntryTime <= DateTime.UtcNow)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("Thời gian vào phải lớn hơn thời gian hiện tại", 400);
            }

            var user = await _unitOfWork.UserRepo.GetByIdAsync(userId);
            if (user == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("Không tìm thấy người dùng", 404);
            }

            var carVehicleType = await _unitOfWork.VehicleTypeRepo.FindByNameAsync("Ô tô");
            if (carVehicleType == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("Không tìm thấy loại phương tiện", 500);
            }

            var pricing = await _unitOfWork.PricingPolicyRepo.GetActivePolicyAsync(carVehicleType.VehicleTypeId);
            if (pricing == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("Không tìm thấy chính sách giá", 404);
            }

            decimal depositAmount = pricing.BasePrice;

            var reservation = new Reservation
            {
                ReservationId = Guid.NewGuid(),
                UserId = userId,
                VehicleTypeId = carVehicleType.VehicleTypeId,
                ExpectedEntryTime = dto.ExpectedEntryTime,
                Status = ReservationStatus.Pending.ToString(),
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.ReservationRepo.AddAsync(reservation);

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                ReservationId = reservation.ReservationId,
                Amount = depositAmount,
                PaymentMethod = PaymentMethod.PayOS.ToString(),
                PaymentStatus = PaymentStatus.Pending.ToString(),
                PaymentType = PaymentType.Deposit.ToString(),
                PaymentTime = DateTime.UtcNow,
                TransactionReference = string.Empty
            };
            await _unitOfWork.PaymentRepo.AddAsync(payment);
            await _unitOfWork.SaveAsync();

            var paymentUrl = await _payOSService.CreatePaymentLinkAsync(payment);

            string paymentLinkId = "";
            if (!string.IsNullOrEmpty(paymentUrl))
            {
                paymentLinkId = paymentUrl.Substring(paymentUrl.LastIndexOf('/') + 1);
            }

            await _unitOfWork.PaymentRepo.UpdateAsync(payment);
            await _unitOfWork.SaveAsync();
            await _unitOfWork.CommitTransactionAsync();

            return new ResponseDTO("Reservation created successfully", 200, true,
                new CreateReservationResponseDTO
                {
                    ReservationId = reservation.ReservationId,
                    PaymentId = payment.PaymentId,
                    DepositAmount = depositAmount,
                    PaymentLinkId = paymentLinkId,
                    PaymentUrl = paymentUrl,
                    OrderCode = payment.TransactionReference
                });
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return new ResponseDTO(ex.Message, 500);
        }
    }

    public async Task<ResponseDTO> CheckPaymentStatusByOrderCodeAsync(string orderCode)
    {
        try
        {
            var payment = await _unitOfWork.PaymentRepo.GetByOrderCodeAsync(orderCode);

            if (payment == null)
            {
                return new ResponseDTO("Không tìm thấy thanh toán", 404);
            }

            var reservation = await _unitOfWork.ReservationRepo.GetByIdAsync(payment.ReservationId.Value);

            return new ResponseDTO("Kiểm tra trạng thái thành công", 200, true, new
            {
                PaymentStatus = payment.PaymentStatus,
                ReservationStatus = reservation?.Status,
                ReservationId = payment.ReservationId
            });
        }
        catch (Exception ex)
        {
            return new ResponseDTO(ex.Message, 500);
        }
    }

    public async Task<ResponseDTO> GetMyReservationsAsync(Guid userId)
    {
        try
        {
            var reservations = await _unitOfWork.ReservationRepo.GetByUserIdWithPaymentsAsync(userId);
            return new ResponseDTO("Lấy thông tin đặt chỗ của người dùng thành công", 200, true, reservations);
        }
        catch (Exception ex)
        {
            return new ResponseDTO(ex.Message, 500);
        }
    }

    public async Task<ResponseDTO> GetReservationByIdAsync(Guid reservationId, Guid userId, string userRole)
    {
        try
        {
            var reservation = await _unitOfWork.ReservationRepo.GetDetailWithRelationsAsync(reservationId);

            if (reservation == null)
            {
                return new ResponseDTO("Không tìm thấy thông tin đặt chỗ", 404);
            }

            if (userRole != "Manager" && userRole != "Staff" && reservation.UserId != userId)
            {
                return new ResponseDTO("Bạn không có quyền xem thông tin đặt chỗ này", 403);
            }

            return new ResponseDTO("Lấy thông tin đặt chỗ theo id thành công", 200, true, reservation);
        }
        catch (Exception ex)
        {
            return new ResponseDTO(ex.Message, 500);
        }
    }

    public async Task<ResponseDTO> CancelReservationAsync(Guid reservationId, Guid userId)
    {
        try
        {
            var reservation = await _unitOfWork.ReservationRepo.GetByIdAsync(reservationId);

            if (reservation == null)
                return new ResponseDTO("Không tìm thấy thông tin đặt chỗ", 404);

            if (reservation.UserId != userId)
                return new ResponseDTO("Bạn không phải là chủ sở hữu của thông tin đặt chỗ này", 403);

            if (reservation.Status == ReservationStatus.Completed.ToString())
                return new ResponseDTO("Đặt chỗ đã hoàn tất", 400);

            if (reservation.Status == ReservationStatus.Cancelled.ToString())
                return new ResponseDTO("Đặt chỗ đã bị hủy", 400);

            reservation.Status = ReservationStatus.Cancelled.ToString();

            var payment = await _unitOfWork.PaymentRepo.FirstOrDefaultAsync(p => p.ReservationId == reservationId);
            if (payment != null && payment.PaymentStatus == PaymentStatus.Pending.ToString())
            {
                payment.PaymentStatus = PaymentStatus.Failed.ToString();
                await _unitOfWork.PaymentRepo.UpdateAsync(payment);
            }

            await _unitOfWork.ReservationRepo.UpdateAsync(reservation);
            await _unitOfWork.SaveAsync();

            return new ResponseDTO("Đã hủy đặt chỗ thành công", 200, true);
        }
        catch (Exception ex)
        {
            return new ResponseDTO(ex.Message, 500);
        }
    }

    public async Task<ResponseDTO> GetAllReservationsAsync(string? status, DateTime? date)
    {
        try
        {
            var reservations = await _unitOfWork.ReservationRepo.GetByAdminFiltersAsync(status, date);
            return new ResponseDTO("Lấy danh sách thông tin đặt chỗ thành công", 200, true, reservations);
        }
        catch (Exception ex)
        {
            return new ResponseDTO(ex.Message, 500);
        }
    }

    public async Task<ResponseDTO> UpdateReservationStatusAsync(Guid reservationId, UpdateReservationStatusDTO dto)
    {
        try
        {
            var reservation = await _unitOfWork.ReservationRepo.GetByIdAsync(reservationId);
            if (reservation == null)
                return new ResponseDTO("Không tìm thấy thông tin đặt chỗ", 404);

            reservation.Status = dto.Status;
            await _unitOfWork.ReservationRepo.UpdateAsync(reservation);
            await _unitOfWork.SaveAsync();

            return new ResponseDTO($"Trạng thái đặt chỗ đã được cập nhật thành {dto.Status}", 200, true);
        }
        catch (Exception ex)
        {
            return new ResponseDTO(ex.Message, 500);
        }
    }
}