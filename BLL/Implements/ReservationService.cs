using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Reservation;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using Hangfire; 
using QRCoder;

public class ReservationService : IReservationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPayOSService _payOSService;
    private readonly IBackgroundJobClient _backgroundJobClient; 

    private const double RESERVATION_QUOTA_PERCENT = 0.20;

    public ReservationService(IUnitOfWork unitOfWork, IPayOSService payOSService, IBackgroundJobClient backgroundJobClient)
    {
        _unitOfWork = unitOfWork;
        _payOSService = payOSService;
        _backgroundJobClient = backgroundJobClient;
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

            if (dto.ExpectedEntryTime > DateTime.UtcNow.AddHours(5))
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("Chỉ được phép đặt trước tối đa 5 tiếng", 400);
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

            int totalCarCapacity = await _unitOfWork.FloorRepo.GetTotalCapacityByVehicleTypeAsync(carVehicleType.VehicleTypeId, isResident: false);
            if (totalCarCapacity == 0)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("Hệ thống hiện tại không có tầng nào hỗ trợ đỗ xe Ô tô vãng lai", 400);
            }

            int maxReservationSlots = (int)(totalCarCapacity * RESERVATION_QUOTA_PERCENT);
            int currentActiveReservations = await _unitOfWork.ReservationRepo
                .CountActiveReservationsAsync(
                    carVehicleType.VehicleTypeId,
                    ReservationStatus.Confirmed.ToString(),
                    ReservationStatus.Modified.ToString(),
                    Guid.Empty
                );
            if (currentActiveReservations >= maxReservationSlots)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("Hệ thống đã hết chỗ nhận đặt trước. Vui lòng vào trực tiếp hoặc chọn khung giờ khác.", 400);
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

            var delay = reservation.ExpectedEntryTime.AddMinutes(30) - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                _backgroundJobClient.Schedule<IReservationService>(
                    service => service.ProcessNoShowTimeoutAsync(reservation.ReservationId),
                    delay
                );
            }

            return new ResponseDTO("Reservation created successfully", 200, true,
                new CreateReservationPaymentDTO
                {
                    ReservationId = reservation.ReservationId,
                    PaymentId = payment.PaymentId,
                    DepositAmount = depositAmount,
                    PaymentLinkId = paymentLinkId,
                    PaymentUrl = paymentUrl,
                    OrderCode = payment.TransactionReference,
                    Ticket = CreateReservationTicket(reservation.ReservationId)
                });
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return new ResponseDTO(ex.Message, 500);
        }
    }

    public async Task<ResponseDTO> ChangeReservationTimeAsync(Guid reservationId, DateTime newExpectedTime)
    {
        try
        {
            var reservation = await _unitOfWork.ReservationRepo.GetByIdAsync(reservationId);
            if (reservation == null) return new ResponseDTO("Không tìm thấy thông tin đặt chỗ", 404);

            if (string.Equals(reservation.Status, ReservationStatus.Modified.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return new ResponseDTO("Bạn đã hết lượt đổi giờ cho mã đặt chỗ này (Tối đa 1 lần)", 400);
            }

            if (!string.Equals(reservation.Status, ReservationStatus.Confirmed.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return new ResponseDTO("Chỉ đơn đặt chỗ đã thanh toán thành công mới được đổi giờ", 400);
            }

            if (DateTime.UtcNow > reservation.ExpectedEntryTime.AddHours(0.5)) 
            {
                reservation.Status = ReservationStatus.NoShow.ToString();
                await _unitOfWork.ReservationRepo.UpdateAsync(reservation);
                await _unitOfWork.SaveAsync();
                return new ResponseDTO("Đơn đặt chỗ đã bị hủy (NoShow) do quá hạn 30 phút không check-in. Không thể đổi giờ.", 400);
            }

            if (newExpectedTime <= DateTime.UtcNow)
            {
                return new ResponseDTO("Giờ hẹn mới phải lớn hơn thời gian hiện tại", 400);
            }

            if (newExpectedTime > DateTime.UtcNow.AddHours(5))
            {
                return new ResponseDTO("Giờ hẹn mới không được vượt quá 5 tiếng tính từ thời điểm hiện tại", 400);
            }

            if (DateTime.UtcNow >= reservation.ExpectedEntryTime.AddMinutes(-15))
            {
                return new ResponseDTO("Phải thực hiện đổi lịch trước giờ hẹn cũ ít nhất 15 phút.", 400);
            }

            int totalCarCapacity = await _unitOfWork.FloorRepo.GetTotalCapacityByVehicleTypeAsync(reservation.VehicleTypeId, isResident: false);
            int maxReservationSlots = (int)(totalCarCapacity * RESERVATION_QUOTA_PERCENT);

            int currentActiveReservations = await _unitOfWork.ReservationRepo
                            .CountActiveReservationsAsync(
                                reservation.VehicleTypeId,
                                ReservationStatus.Confirmed.ToString(),
                                ReservationStatus.Modified.ToString(),
                                reservationId
                            );
            if (currentActiveReservations >= maxReservationSlots)
            {
                return new ResponseDTO("Khung giờ mới đã hết hạn ngạch đặt trước. Vui lòng giữ nguyên giờ cũ.", 400);
            }

            reservation.ExpectedEntryTime = newExpectedTime;
            reservation.Status = ReservationStatus.Modified.ToString();
            await _unitOfWork.ReservationRepo.UpdateAsync(reservation);
            await _unitOfWork.SaveAsync();

            var delay = newExpectedTime.AddMinutes(30) - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                _backgroundJobClient.Schedule<IReservationService>(
                    service => service.ProcessNoShowTimeoutAsync(reservation.ReservationId),
                    delay
                );
            }

            return new ResponseDTO($"Thay đổi giờ hẹn thành công sang: {newExpectedTime:HH:mm dd/MM/yyyy}", 200, true);
        }
        catch (Exception ex)
        {
            return new ResponseDTO(ex.Message, 500);
        }
    }

    public async Task<ResponseDTO> CreatePaymentLinkForReservationAsync(Guid reservationId, Guid userId)
    {
        try
        {
            if (reservationId == Guid.Empty)
                return new ResponseDTO("Vui lòng cung cấp ReservationId", 400, false);

            var reservation = await _unitOfWork.ReservationRepo.GetByIdAsync(reservationId);
            if (reservation == null)
                return new ResponseDTO("Không tìm thấy thông tin đặt chỗ", 404, false);

            if (userId != Guid.Empty && reservation.UserId != userId)
                return new ResponseDTO("Bạn không có quyền thanh toán cho đặt chỗ này", 403, false);

            if (string.Equals(reservation.Status, ReservationStatus.Confirmed.ToString(), StringComparison.OrdinalIgnoreCase))
                return new ResponseDTO("Đặt chỗ này đã được thanh toán và xác nhận thành công", 400, false);

            if (string.Equals(reservation.Status, ReservationStatus.CheckedIn.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(reservation.Status, ReservationStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase))
                return new ResponseDTO("Lượt đặt chỗ này đã hoặc đang được sử dụng", 400, false);

            if (string.Equals(reservation.Status, ReservationStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(reservation.Status, ReservationStatus.NoShow.ToString(), StringComparison.OrdinalIgnoreCase))
                return new ResponseDTO("Đặt chỗ đã bị hủy hoặc quá hạn, không thể thanh toán", 400, false);

            var pricing = await _unitOfWork.PricingPolicyRepo.GetActivePolicyAsync(reservation.VehicleTypeId);
            if (pricing == null)
            {
                return new ResponseDTO("Không tìm thấy chính sách giá cho loại phương tiện này", 404, false);
            }
            decimal depositAmount = pricing.BasePrice;

            var payment = await _unitOfWork.PaymentRepo.FirstOrDefaultAsync(p =>
                p.ReservationId == reservationId &&
                p.PaymentType == PaymentType.Deposit.ToString() &&
                p.PaymentStatus == PaymentStatus.Pending.ToString());

            if (payment == null)
            {
                payment = new Payment
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
            }
            else
            {
                payment.PaymentTime = DateTime.UtcNow;
                await _unitOfWork.PaymentRepo.UpdateAsync(payment);
            }

            await _unitOfWork.SaveAsync();

            string paymentUrl = string.Empty;
            string paymentLinkId = string.Empty;
            try
            {
                paymentUrl = await _payOSService.CreatePaymentLinkAsync(payment);
                if (!string.IsNullOrEmpty(paymentUrl))
                {
                    paymentLinkId = paymentUrl.Substring(paymentUrl.LastIndexOf('/') + 1);
                }
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi khi kết nối cổng thanh toán PayOS: {ex.Message}", 500, false);
            }

            return new ResponseDTO("Tạo lại link thanh toán thành công", 200, true, new CreateReservationPaymentDTO
            {
                ReservationId = reservation.ReservationId,
                PaymentId = payment.PaymentId,
                DepositAmount = payment.Amount,
                PaymentLinkId = paymentLinkId,
                PaymentUrl = paymentUrl,
                OrderCode = payment.TransactionReference,
                Ticket = CreateReservationTicket(reservation.ReservationId)
            });
        }
        catch (Exception ex)
        {
            return new ResponseDTO(ex.Message, 500, false);
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

            if (!payment.ReservationId.HasValue)
            {
                return new ResponseDTO("Thanh toán này không thuộc đặt chỗ", 400, false);
            }

            var reservation = await _unitOfWork.ReservationRepo.GetByIdAsync(payment.ReservationId.Value);

            return new ResponseDTO("Kiểm tra trạng thái thành công", 200, true, new
            {
                PaymentStatus = payment.PaymentStatus,
                ReservationStatus = reservation?.Status,
                ReservationId = payment.ReservationId,
                Ticket = payment.ReservationId.HasValue ? CreateReservationTicket(payment.ReservationId.Value) : null
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
            if (userId == Guid.Empty)
                return new ResponseDTO("Vui lòng đăng nhập hệ thống", 400, false);

            var reservations = await _unitOfWork.ReservationRepo.GetByUserIdWithPaymentsAsync(userId);
            var reservationList = reservations.ToList();

            if (reservationList.Count == 0)
            {
                return new ResponseDTO("Bạn không có lịch sử đặt chỗ nào", 404, false);
            }

            var dtos = reservationList.Select(MapToDTO).ToList();
            return new ResponseDTO("Lấy thông tin đặt chỗ của người dùng thành công", 200, true, dtos);
        }
        catch (Exception ex)
        {
            return new ResponseDTO(ex.Message, 500, false);
        }
    }

    public async Task<ResponseDTO> GetReservationByIdAsync(Guid reservationId, Guid userId, string userRole)
    {
        try
        {
            if (reservationId == Guid.Empty)
                return new ResponseDTO("Vui lòng nhập ReservationId", 400, false);

            var reservation = await _unitOfWork.ReservationRepo.GetDetailWithRelationsAsync(reservationId);
            if (reservation == null)
            {
                return new ResponseDTO("Không tìm thấy thông tin đặt chỗ", 404, false);
            }

            if (userRole != "Manager" && userRole != "Staff" && reservation.UserId != userId)
            {
                return new ResponseDTO("Bạn không có quyền xem thông tin đặt chỗ này", 403, false);
            }

            return new ResponseDTO("Lấy thông tin đặt chỗ theo id thành công", 200, true, MapToDTO(reservation));
        }
        catch (Exception ex)
        {
            return new ResponseDTO(ex.Message, 500, false);
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
            var dtos = reservations.Select(MapToDTO).ToList();
            return new ResponseDTO("Lấy danh sách thông tin đặt chỗ thành công", 200, true, dtos);
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

    public async Task ProcessNoShowTimeoutAsync(Guid reservationId)
    {
        try
        {
            var reservation = await _unitOfWork.ReservationRepo.GetByIdAsync(reservationId);

            if (reservation == null ||
                reservation.Status == ReservationStatus.CheckedIn.ToString() ||
                reservation.Status == ReservationStatus.Cancelled.ToString() ||
                reservation.Status == ReservationStatus.Completed.ToString() ||
                reservation.Status == ReservationStatus.NoShow.ToString())
            {
                return;
            }

            if (DateTime.UtcNow >= reservation.ExpectedEntryTime.AddMinutes(30))
            {
                reservation.Status = ReservationStatus.NoShow.ToString();
                await _unitOfWork.ReservationRepo.UpdateAsync(reservation);
                await _unitOfWork.SaveAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Hangfire Error] Lỗi tự động xử lý NoShow cho đơn {reservationId}: {ex.Message}");
            throw;
        }
    }

    public async Task ProcessOverdueReservationsAsync()
    {
        var overdueReservations = await _unitOfWork.ReservationRepo.GetOverdueAsync(
            DateTime.UtcNow.AddMinutes(-30),
            ReservationStatus.Pending.ToString(),
            ReservationStatus.Confirmed.ToString(),
            ReservationStatus.Modified.ToString());

        if (overdueReservations.Count == 0)
        {
            return;
        }

        foreach (var reservation in overdueReservations)
        {
            reservation.Status = ReservationStatus.NoShow.ToString();
            await _unitOfWork.ReservationRepo.UpdateAsync(reservation);
        }

        await _unitOfWork.SaveAsync();
        Console.WriteLine($"[Hangfire] Đã chuyển {overdueReservations.Count} đơn quá hạn sang NoShow.");
    }

    private static ReservationDTO MapToDTO(Reservation reservation)
    {
        return new ReservationDTO
        {
            ReservationId = reservation.ReservationId,
            UserId = reservation.UserId,
            UserFullName = reservation.User?.FullName ?? "N/A",
            VehicleTypeId = reservation.VehicleTypeId,
            VehicleTypeName = reservation.VehicleType?.TypeName ?? "N/A",
            ExpectedEntryTime = reservation.ExpectedEntryTime,
            Status = reservation.Status,
            CreatedAt = reservation.CreatedAt,
            Ticket = CreateReservationTicket(reservation.ReservationId)
        };
    }

    private static ReservationTicketDTO CreateReservationTicket(Guid reservationId)
    {
        var qrPayload = reservationId.ToString();
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = pngQrCode.GetGraphic(20);
        var qrCodeBase64 = Convert.ToBase64String(qrCodeBytes);

        return new ReservationTicketDTO
        {
            QrPayload = qrPayload,
            QrCodeDataUrl = $"data:image/png;base64,{qrCodeBase64}"
        };
    }
}
