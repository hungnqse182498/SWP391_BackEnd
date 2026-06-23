using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Subscription;
using Common.DTOs.Payment;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using PayOS.Models.V2.PaymentRequests;

namespace BLL.Implements
{
    public class MonthlySubscriptionService : IMonthlySubscriptionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPayOSService _payOSService;

        public MonthlySubscriptionService(IUnitOfWork unitOfWork, IPayOSService payOSService)
        {
            _unitOfWork = unitOfWork;
            _payOSService = payOSService;
        }

        public async Task<ResponseDTO> RegisterAsync(Guid userId, RegisterMonthlySubscriptionDTO dto)
        {
            if (userId == Guid.Empty) return new ResponseDTO("Vui lòng đăng nhập để đăng ký gói", 401);
            if (dto == null) return new ResponseDTO("Dữ liệu đăng ký không hợp lệ", 400);
            if (dto.PackageId == Guid.Empty) return new ResponseDTO("Vui lòng chọn gói", 400);
            if (string.IsNullOrWhiteSpace(dto.LicensePlate)) return new ResponseDTO("Vui lòng nhập biển số xe", 400);

            var package = await _unitOfWork.SubscriptionPackageRepo.GetAll()
                .Include(p => p.VehicleType)
                .FirstOrDefaultAsync(p => p.PackageId == dto.PackageId && (p.Status == null || p.Status == "Active"));
            if (package == null) return new ResponseDTO("Gói không tồn tại hoặc đã ngừng bán", 404);

            var normalizedPlate = string.IsNullOrWhiteSpace(dto.LicensePlate) ? string.Empty : dto.LicensePlate.Trim().ToUpperInvariant();

            var plateExists = await HasUsablePlateAsync(normalizedPlate);
            if (plateExists) return new ResponseDTO("Biển số này đã có gói đang hiệu lực hoặc đang chờ thanh toán", 400);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var subscription = new MonthlySubscription
                {
                    SubscriptionId = Guid.NewGuid(),
                    UserId = userId,
                    VehicleTypeId = package.VehicleTypeId,
                    LicensePlate = normalizedPlate,
                    PackageId = package.PackageId,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddMonths(package.DurationMonths),
                    Price = package.Price,
                    Status = "PendingPayment"
                };

                var payment = new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    SubscriptionId = subscription.SubscriptionId,
                    Amount = package.Price,
                    PaymentMethod = PaymentMethod.PayOS.ToString(),
                    PaymentStatus = PaymentStatus.Pending.ToString(),
                    PaymentType = PaymentType.SubscriptionFee.ToString(),
                    PaymentTime = DateTime.UtcNow,
                    TransactionReference = string.Empty
                };

                await _unitOfWork.MonthlySubscriptionRepo.AddAsync(subscription);
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

                return new ResponseDTO("Tạo đăng ký gói thành công", 201, true, new RegisterMonthlySubscriptionPaymentDTO
                {
                    SubscriptionId = subscription.SubscriptionId,
                    PaymentId = payment.PaymentId,
                    Amount = payment.Amount,
                    PaymentLinkId = paymentLinkId,
                    PaymentUrl = paymentUrl,
                    OrderCode = payment.TransactionReference
                });
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO($"Lỗi đăng ký gói: {ex.Message}", 500);
            }
        }

        public async Task<ResponseDTO> CreatePaymentAsync(Guid subscriptionId, Guid userId)
        {
            var subscription = await _unitOfWork.MonthlySubscriptionRepo.GetDetailAsync(subscriptionId);
            if (subscription == null) return new ResponseDTO("Không tìm thấy gói tháng", 404);
            if (userId != Guid.Empty && subscription.UserId != userId) return new ResponseDTO("Bạn không có quyền thanh toán gói này", 403);
            if (subscription.Status == "Active") return new ResponseDTO("Gói đã được kích hoạt", 400);

            var payment = await _unitOfWork.PaymentRepo.GetAll()
                .Where(p => p.SubscriptionId == subscriptionId && p.PaymentType == PaymentType.SubscriptionFee.ToString() && p.PaymentStatus == PaymentStatus.Pending.ToString())
                .OrderByDescending(p => p.PaymentTime)
                .FirstOrDefaultAsync();

            if (payment == null)
            {
                payment = new Payment
                    {
                        PaymentId = Guid.NewGuid(),
                        SubscriptionId = subscription.SubscriptionId,
                        Amount = subscription.Price,
                        PaymentMethod = PaymentMethod.PayOS.ToString(),
                        PaymentStatus = PaymentStatus.Pending.ToString(),
                        PaymentType = PaymentType.SubscriptionFee.ToString(),
                        PaymentTime = DateTime.UtcNow,
                        TransactionReference = string.Empty
                    };
                await _unitOfWork.PaymentRepo.AddAsync(payment);
                await _unitOfWork.SaveAsync();
            }

            var paymentUrl = await _payOSService.CreatePaymentLinkAsync(payment);
            string paymentLinkId = "";
            if (!string.IsNullOrEmpty(paymentUrl))
            {
                paymentLinkId = paymentUrl.Substring(paymentUrl.LastIndexOf('/') + 1);
            }
            await _unitOfWork.SaveAsync();

            return new ResponseDTO("Tạo link thanh toán thành công", 200, true, new RegisterMonthlySubscriptionPaymentDTO
            {
                SubscriptionId = subscription.SubscriptionId,
                PaymentId = payment.PaymentId,
                Amount = payment.Amount,
                PaymentLinkId = paymentLinkId,
                PaymentUrl = paymentUrl,
                OrderCode = payment.TransactionReference
            });
        }

        public async Task<ResponseDTO> GetMyAsync(Guid userId)
        {
            if (userId == Guid.Empty) return new ResponseDTO("Vui lòng đăng nhập", 401);
            var list = await _unitOfWork.MonthlySubscriptionRepo.GetByUserAsync(userId);
            return new ResponseDTO("OK", 200, true, list.Select(MapToDTO).ToList());
        }

        public async Task<ResponseDTO> GetByUserAsync(Guid userId)
        {
            var list = await _unitOfWork.MonthlySubscriptionRepo.GetByUserAsync(userId);
            return new ResponseDTO("OK", 200, true, list.Select(MapToDTO).ToList());
        }

        public async Task<ResponseDTO> GetDetailAsync(Guid id)
        {
            var data = await _unitOfWork.MonthlySubscriptionRepo.GetDetailAsync(id);
            if (data == null) return new ResponseDTO("Không tồn tại", 404);
            return new ResponseDTO("OK", 200, true, MapToDTO(data));
        }

        public async Task<ResponseDTO> CancelAsync(Guid id)
        {
            var sub = await _unitOfWork.MonthlySubscriptionRepo.GetDetailAsync(id);
            if (sub == null) return new ResponseDTO("Không tìm thấy", 404);

            sub.Status = "Cancelled";
            if (sub.FixedSlotId.HasValue)
            {
                var slot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(sub.FixedSlotId.Value);
                if (slot != null)
                {
                    slot.AssignedUserId = null;
                    slot.Status = "Available";
                    await _unitOfWork.ParkingSlotRepo.UpdateAsync(slot);
                }

                sub.FixedSlotId = null;
            }

            await _unitOfWork.MonthlySubscriptionRepo.UpdateAsync(sub);
            await _unitOfWork.SaveAsync();

            return new ResponseDTO("Hủy thành công", 200, true);
        }

        private async Task<bool> HasUsablePlateAsync(string plate, Guid? ignoredSubscriptionId = null)
        {
            return await _unitOfWork.MonthlySubscriptionRepo.GetAll()
                .AnyAsync(s =>
                    s.LicensePlate == plate &&
                    s.Status != "Cancelled" &&
                    (!ignoredSubscriptionId.HasValue || s.SubscriptionId != ignoredSubscriptionId.Value) &&
                    (s.Status == "PendingPayment" || s.EndDate >= DateTime.Now));
        }

        private static MonthlySubscriptionDTO MapToDTO(MonthlySubscription sub)
        {
            return new MonthlySubscriptionDTO
            {
                SubscriptionId = sub.SubscriptionId,
                FullName = sub.User?.FullName,
                LicensePlate = sub.LicensePlate,
                VehicleType = sub.VehicleType?.TypeName,
                PackageName = sub.Package?.PackageName,
                StartDate = sub.StartDate,
                EndDate = sub.EndDate,
                Price = sub.Price,
                Status = sub.Status,
                FixedSlot = sub.FixedSlot?.SlotCode
            };
        }
    }
}
