using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.ParkingOperation;
using Common.DTOs.ParkingSession;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace BLL.Implements
{
    public class ParkingOperationService : IParkingOperationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPayOSService _payOSService;
        private const string EntryGateType = "Entry";
        private const string ExitGateType = "Exit";
        private static readonly string[] ReservationFloorKeywords = { "Đặt trước", "Dat truoc" };

        public ParkingOperationService(IUnitOfWork unitOfWork, IPayOSService payOSService)
        {
            _unitOfWork = unitOfWork;
            _payOSService = payOSService;
        }

        public async Task<ResponseDTO> GuestCheckInAsync(GuestCheckInDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu check-in khách vãng lai không hợp lệ", 400, false);
            if (string.IsNullOrWhiteSpace(dto.LicensePlate)) return new ResponseDTO("Vui lòng nhập biển số", 400, false);
            if (dto.VehicleTypeId == Guid.Empty) return new ResponseDTO("Vui lòng chọn loại phương tiện", 400, false);

            var vehicleType = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(dto.VehicleTypeId);
            if (vehicleType == null) return new ResponseDTO("Loại phương tiện không tồn tại", 400, false);

            var gateResult = await ResolveGateByIdAsync(dto.GateId, EntryGateType);
            if (gateResult.Error != null) return gateResult.Error;

            var licensePlate = NormalizePlate(dto.LicensePlate);

            var activeValidation = await ValidateNoActiveSessionAsync(licensePlate);
            if (activeValidation != null) return activeValidation;

            var slot = await FindGuestAvailableSlotAsync(dto.VehicleTypeId);
            if (slot == null) return new ResponseDTO("Không còn chỗ trống cho khách vãng lai", 409, false);

            var now = DateTime.UtcNow;
            var session = new ParkingSession
            {
                SessionId = Guid.NewGuid(),
                LicensePlateIn = licensePlate,
                EntryImageUrl = NormalizeOptional(dto.EntryImageUrl),
                VehicleTypeId = dto.VehicleTypeId,
                EntryTime = now,
                EntryGateId = gateResult.Gate!.GateId,
                ActualSlotId = slot.SlotId,
                Status = SessionStatus.Active.ToString()
            };

            slot.Status = ParkingSlotStatus.Occupied.ToString();

            await _unitOfWork.ParkingSessionRepo.AddAsync(session);
            await _unitOfWork.ParkingSlotRepo.UpdateAsync(slot);
            await _unitOfWork.SaveChangeAsync();

            var ticket = CreateSessionTicket(session.SessionId);
            var result = new
            {
                session.SessionId,
                LicensePlate = session.LicensePlateIn,
                session.VehicleTypeId,
                VehicleTypeName = vehicleType.TypeName,
                GateName = gateResult.Gate.GateName,
                session.EntryTime,
                session.Status,
                Ticket = ticket
            };

            return new ResponseDTO("Check-in khách vãng lai thành công", 201, true, result);
        }

        public async Task<ResponseDTO> GuestCheckOutPreviewAsync(GuestCheckOutPreviewDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu xem trước checkout không hợp lệ", 400, false);

            var sessionResult = await FindActiveSessionAsync(dto.SessionId, dto.LicensePlate);
            if (sessionResult.Error != null) return sessionResult.Error;

            var feeResult = await CalculateFeeAsync(sessionResult.Session!, DateTime.UtcNow);
            if (feeResult.Error != null) return feeResult.Error;

            return new ResponseDTO("Tính phí gửi xe thành công", 200, true, feeResult.Preview);
        }

        public async Task<ResponseDTO> GuestCheckOutAsync(GuestCheckOutDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu checkout khách vãng lai không hợp lệ", 400, false);
            if (dto.SessionId == Guid.Empty) return new ResponseDTO("Vui lòng quét mã vé gửi xe", 400, false);

            var paymentMethod = NormalizeGuestCheckoutPaymentMethod(dto.PaymentMethod);
            if (paymentMethod == null) return new ResponseDTO("Phương thức thanh toán chỉ được là Cash hoặc PayOS", 400, false);

            var exitGateResult = await ResolveGateByIdAsync(dto.GateId, ExitGateType);
            if (exitGateResult.Error != null) return exitGateResult.Error;

            var sessionResult = await FindActiveSessionAsync(dto.SessionId, null);
            if (sessionResult.Error != null) return sessionResult.Error;

            var session = sessionResult.Session!;
            var checkoutPlate = NormalizeOptional(dto.LicensePlateOut);
            var plateValidation = await ValidatePlateOutAsync(session, checkoutPlate);
            if (plateValidation != null) return plateValidation;

            var exitTime = DateTime.UtcNow;
            var feeResult = await CalculateFeeAsync(session, exitTime);
            if (feeResult.Error != null) return feeResult.Error;

            var pendingPayment = await _unitOfWork.PaymentRepo.GetPendingCheckoutPaymentAsync(session.SessionId);
            if (pendingPayment != null)
            {
                return new ResponseDTO("Phiên gửi xe này đang có thanh toán online chờ xử lý", 409, false, new
                {
                    Payment = MapOperationPayment(pendingPayment)
                });
            }

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                SessionId = session.SessionId,
                Amount = feeResult.Preview!.Amount,
                PaymentMethod = paymentMethod,
                PaymentType = PaymentType.CheckoutFee.ToString(),
                PaymentTime = exitTime,
                PaymentStatus = paymentMethod == PaymentMethod.Cash.ToString()
                    ? PaymentStatus.Success.ToString()
                    : PaymentStatus.Pending.ToString(),
                TransactionReference = string.Empty
            };

            if (paymentMethod == PaymentMethod.Cash.ToString())
            {
                await CloseSessionAsync(session, exitGateResult.Gate!.GateId, checkoutPlate, dto.ExitImageUrl, exitTime);
                await _unitOfWork.PaymentRepo.AddAsync(payment);
                await _unitOfWork.SaveChangeAsync();

                var cashResult = new
                {
                    Session = await GetSessionDTOAsync(session.SessionId),
                    Payment = MapOperationPayment(payment),
                    Fee = feeResult.Preview
                };

                return new ResponseDTO("Checkout khách vãng lai bằng tiền mặt thành công", 200, true, cashResult);
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                PrepareSessionForPendingOnlineCheckout(session, exitGateResult.Gate!.GateId, checkoutPlate, dto.ExitImageUrl, exitTime);
                await _unitOfWork.ParkingSessionRepo.UpdateAsync(session);
                await _unitOfWork.PaymentRepo.AddAsync(payment);
                await _unitOfWork.SaveAsync();

                var paymentUrl = await _payOSService.CreatePaymentLinkAsync(payment);
                if (string.IsNullOrWhiteSpace(paymentUrl))
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return new ResponseDTO("Không tạo được link thanh toán PayOS", 500, false);
                }

                await _unitOfWork.PaymentRepo.UpdateAsync(payment);
                await _unitOfWork.SaveAsync();
                await _unitOfWork.CommitTransactionAsync();

                var payOSResult = new
                {
                    Session = await GetSessionDTOAsync(session.SessionId),
                    Payment = MapOperationPayment(payment),
                    Fee = feeResult.Preview,
                    OnlinePayment = new
                    {
                        PaymentUrl = paymentUrl,
                        PaymentLinkId = GetPaymentLinkId(paymentUrl),
                        OrderCode = payment.TransactionReference
                    }
                };

                return new ResponseDTO("Tạo thanh toán PayOS cho checkout khách vãng lai thành công", 200, true, payOSResult);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO($"Lỗi tạo thanh toán PayOS: {ex.Message}", 500, false);
            }
        }

        private async Task<ResponseDTO> GuestCheckOutLegacyAsync(GuestCheckOutLegacyDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu checkout khách vãng lai không hợp lệ", 400, false);
            if (string.IsNullOrWhiteSpace(dto.PaymentMethod)) return new ResponseDTO("Vui lòng nhập phương thức thanh toán", 400, false);

            var exitGateValidation = await ValidateGateTypeAsync(dto.GateId, ExitGateType);
            if (exitGateValidation != null) return exitGateValidation;

            var sessionResult = await FindActiveSessionAsync(dto.SessionId, null);
            if (sessionResult.Error != null) return sessionResult.Error;

            var session = sessionResult.Session!;
            var plateValidation = await ValidatePlateOutAsync(session, dto.LicensePlateOut);
            if (plateValidation != null) return plateValidation;

            var exitTime = DateTime.UtcNow;
            var feeResult = await CalculateFeeAsync(session, exitTime);
            if (feeResult.Error != null) return feeResult.Error;

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                SessionId = session.SessionId,
                Amount = feeResult.Preview!.Amount,
                PaymentMethod = dto.PaymentMethod.Trim(),
                PaymentType = PaymentType.CheckoutFee.ToString(),
                PaymentTime = exitTime,
                PaymentStatus = PaymentStatus.Success.ToString(),
                TransactionReference = NormalizeOptional(dto.TransactionReference)
            };

            await CloseSessionAsync(session, dto.GateId, dto.LicensePlateOut, dto.ExitImageUrl, exitTime);
            await _unitOfWork.PaymentRepo.AddAsync(payment);
            await _unitOfWork.SaveChangeAsync();

            var result = new
            {
                Session = await GetSessionDTOAsync(session.SessionId),
                Payment = new
                {
                    payment.PaymentId,
                    payment.SessionId,
                    payment.Amount,
                    payment.PaymentMethod,
                    payment.PaymentTime,
                    payment.PaymentStatus,
                    payment.TransactionReference
                },
                Fee = feeResult.Preview
            };

            return new ResponseDTO("Checkout khách vãng lai thành công", 200, true, result);
        }

        public async Task<ResponseDTO> ResidentCheckInAsync(ResidentCheckInDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu check-in cư dân không hợp lệ", 400, false);
            if (string.IsNullOrWhiteSpace(dto.LicensePlate)) return new ResponseDTO("Vui lòng nhập biển số", 400, false);
            if (dto.VehicleTypeId == Guid.Empty) return new ResponseDTO("Vui lòng chọn loại phương tiện", 400, false);

            var vehicleTypeExists = await _unitOfWork.VehicleTypeRepo.AnyAsync(v => v.VehicleTypeId == dto.VehicleTypeId);
            if (!vehicleTypeExists) return new ResponseDTO("Loại phương tiện không tồn tại", 400, false);

            var gateResult = await ResolveGateByIdAsync(dto.GateId, EntryGateType);
            if (gateResult.Error != null) return gateResult.Error;

            var licensePlate = NormalizePlate(dto.LicensePlate);

            var now = DateTime.UtcNow;
            var subscription = await _unitOfWork.MonthlySubscriptionRepo
                .GetActiveByPlateAndVehicleTypeAsync(licensePlate, dto.VehicleTypeId, now);

            if (subscription == null) return new ResponseDTO("Không tìm thấy gói tháng hợp lệ cho biển số này", 403, false);

            var activeValidation = await ValidateNoActiveSessionAsync(licensePlate);
            if (activeValidation != null) return activeValidation;

            var slot = await FindResidentAvailableSlotAsync(dto.VehicleTypeId);
            if (slot == null) return new ResponseDTO("Không còn chỗ trống cho cư dân", 409, false);

            var session = new ParkingSession
            {
                SessionId = Guid.NewGuid(),
                DriverUserId = subscription.UserId,
                LicensePlateIn = licensePlate,
                EntryImageUrl = NormalizeOptional(dto.EntryImageUrl),
                VehicleTypeId = dto.VehicleTypeId,
                EntryTime = now,
                EntryGateId = gateResult.Gate!.GateId,
                AssignedSlotId = slot.SlotId,
                ActualSlotId = slot.SlotId,
                Status = SessionStatus.Active.ToString()
            };

            slot.Status = ParkingSlotStatus.Occupied.ToString();

            await _unitOfWork.ParkingSessionRepo.AddAsync(session);
            await _unitOfWork.ParkingSlotRepo.UpdateAsync(slot);
            await _unitOfWork.SaveChangeAsync();

            var result = await GetSessionDTOWithTicketAsync(session.SessionId);
            return new ResponseDTO("Check-in cư dân thành công", 201, true, result);
        }

        public async Task<ResponseDTO> ResidentCheckOutAsync(ResidentCheckOutDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu checkout cư dân không hợp lệ", 400, false);
            if (dto.SessionId == Guid.Empty) return new ResponseDTO("Vui lòng quét mã vé gửi xe", 400, false);

            var exitGateResult = await ResolveGateByIdAsync(dto.GateId, ExitGateType);
            if (exitGateResult.Error != null) return exitGateResult.Error;

            var sessionResult = await FindActiveSessionAsync(dto.SessionId, null);
            if (sessionResult.Error != null) return sessionResult.Error;

            var session = sessionResult.Session!;
            var now = DateTime.UtcNow;
            var subscription = await _unitOfWork.MonthlySubscriptionRepo
                .GetActiveByPlateAndVehicleTypeAsync(session.LicensePlateIn, session.VehicleTypeId, now);

            if (subscription == null) return new ResponseDTO("Phiên gửi xe này không thuộc gói tháng hợp lệ", 403, false);

            var checkoutPlate = NormalizeOptional(dto.LicensePlateOut);
            var plateValidation = await ValidatePlateOutAsync(session, checkoutPlate);
            if (plateValidation != null) return plateValidation;

            await CloseSessionAsync(session, exitGateResult.Gate!.GateId, checkoutPlate, dto.ExitImageUrl, now);
            await _unitOfWork.SaveChangeAsync();

            var result = await GetSessionDTOAsync(session.SessionId);
            return new ResponseDTO("Checkout cư dân thành công", 200, true, result);
        }

        public async Task<ResponseDTO> ReservationCheckInAsync(ReservationCheckInDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu check-in đặt trước không hợp lệ", 400, false);
            if (dto.ReservationId == Guid.Empty) return new ResponseDTO("Vui lòng nhập ReservationId", 400, false);
            if (string.IsNullOrWhiteSpace(dto.LicensePlate)) return new ResponseDTO("Vui lòng nhập biển số", 400, false);

            var entryGateValidation = await ValidateGateTypeAsync(dto.GateId, EntryGateType);
            if (entryGateValidation != null) return entryGateValidation;

            var reservation = await _unitOfWork.ReservationRepo.GetAll()
                .Include(r => r.User)
                .Include(r => r.VehicleType)
                .FirstOrDefaultAsync(r => r.ReservationId == dto.ReservationId);

            if (reservation == null) return new ResponseDTO("Không tìm thấy đặt chỗ", 404, false);

            if (!string.Equals(reservation.Status, ReservationStatus.Confirmed.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return new ResponseDTO("Đặt chỗ phải ở trạng thái Confirmed để check-in", 400, false);
            }

            var now = DateTime.UtcNow;
            var minEntryTime = reservation.ExpectedEntryTime.AddMinutes(-30);
            var maxEntryTime = reservation.ExpectedEntryTime.AddMinutes(30); 

            if (now < minEntryTime || now > maxEntryTime)
            {
                return new ResponseDTO("Ngoài khung giờ cho phép check-in đặt trước (Chỉ áp dụng trong khoảng -30p đến +30p so với giờ hẹn)", 400, false);
            }

            var licensePlate = NormalizePlate(dto.LicensePlate);
            var activeValidation = await ValidateNoActiveSessionAsync(licensePlate);
            if (activeValidation != null) return activeValidation;

            var availableReservationSlots = await _unitOfWork.ParkingSlotRepo.GetAll()
                .Include(s => s.Floor)
                .Where(s => s.Status == ParkingSlotStatus.Available.ToString() &&
                            s.VehicleTypeId == reservation.VehicleTypeId &&
                            s.Floor != null)
                .OrderBy(s => s.SlotCode)
                .ToListAsync();

            var availableSlot = availableReservationSlots
                .FirstOrDefault(s => FloorMatches(s.Floor?.FloorName, ReservationFloorKeywords));

            if (availableSlot == null)
            {
                return new ResponseDTO("Hệ thống hết vị trí trống khả dụng cho ô tô đặt trước tại thời điểm này", 409, false);
            }
          
            var session = new ParkingSession
            {
                SessionId = Guid.NewGuid(),
                DriverUserId = reservation.UserId,
                LicensePlateIn = licensePlate,
                EntryImageUrl = NormalizeOptional(dto.EntryImageUrl),
                VehicleTypeId = reservation.VehicleTypeId,
                EntryTime = now,
                EntryGateId = dto.GateId,
                AssignedSlotId = availableSlot.SlotId,
                ActualSlotId = availableSlot.SlotId,
                Status = SessionStatus.Active.ToString()
            };

            reservation.Status = ReservationStatus.CheckedIn.ToString();
            availableSlot.Status = ParkingSlotStatus.Occupied.ToString();

            await _unitOfWork.ParkingSessionRepo.AddAsync(session);
            await _unitOfWork.ReservationRepo.UpdateAsync(reservation);
            await _unitOfWork.ParkingSlotRepo.UpdateAsync(availableSlot);
            await _unitOfWork.SaveChangeAsync();

            var result = await GetSessionDTOWithTicketAsync(session.SessionId);
            return new ResponseDTO("Check-in xe đặt trước thành công", 201, true, result);
        }

        public async Task<ResponseDTO> GetAvailabilityAsync(Guid? vehicleTypeId, string? floorKeyword)
        {
            var slots = await _unitOfWork.ParkingSlotRepo.GetAll()
                .Include(s => s.Floor)
                .Include(s => s.VehicleType)
                .Where(s => !vehicleTypeId.HasValue || s.VehicleTypeId == vehicleTypeId.Value)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(floorKeyword))
            {
                slots = slots
                    .Where(s => s.Floor != null && s.Floor.FloorName.Contains(floorKeyword.Trim(), StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var result = slots
                .GroupBy(s => new { s.FloorId, s.Floor.FloorName, VehicleTypeId = (Guid?)s.VehicleTypeId, VehicleTypeName = s.VehicleType.TypeName })
                .Select(g => new ParkingAvailabilityDTO
                {
                    FloorId = g.Key.FloorId,
                    FloorName = g.Key.FloorName,
                    VehicleTypeId = g.Key.VehicleTypeId,
                    VehicleTypeName = g.Key.VehicleTypeName,
                    TotalSlots = g.Count(),
                    AvailableSlots = g.Count(s => s.Status == ParkingSlotStatus.Available.ToString()),
                    OccupiedSlots = g.Count(s => s.Status == ParkingSlotStatus.Occupied.ToString()),
                    ReservedSlots = g.Count(s => s.Status == ParkingSlotStatus.Reserved.ToString())
                })
                .OrderBy(a => a.FloorName)
                .ToList();

            return new ResponseDTO("Lấy tình trạng chỗ trống thành công", 200, true, result);
        }

        private static string? NormalizeGuestCheckoutPaymentMethod(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod)) return null;

            var normalized = paymentMethod.Trim();
            if (string.Equals(normalized, PaymentMethod.Cash.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return PaymentMethod.Cash.ToString();
            }

            if (string.Equals(normalized, PaymentMethod.PayOS.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return PaymentMethod.PayOS.ToString();
            }

            return null;
        }

        private static object MapOperationPayment(Payment payment)
        {
            return new
            {
                payment.PaymentId,
                payment.SessionId,
                payment.ReservationId,
                payment.Amount,
                payment.PaymentMethod,
                payment.PaymentType,
                payment.PaymentTime,
                payment.PaymentStatus,
                payment.TransactionReference
            };
        }

        private static string GetPaymentLinkId(string paymentUrl)
        {
            if (string.IsNullOrWhiteSpace(paymentUrl)) return string.Empty;
            return paymentUrl[(paymentUrl.LastIndexOf('/') + 1)..];
        }

        private static void PrepareSessionForPendingOnlineCheckout(ParkingSession session, Guid exitGateId, string? licensePlateOut, string? exitImageUrl, DateTime exitTime)
        {
            session.ExitGateId = exitGateId;
            session.ExitTime = exitTime;
            session.LicensePlateOut = string.IsNullOrWhiteSpace(licensePlateOut) ? session.LicensePlateIn : NormalizePlate(licensePlateOut);
            session.ExitImageUrl = NormalizeOptional(exitImageUrl);
        }

        private async Task<(string? LicensePlate, ResponseDTO? Error)> ValidateCheckInBaseAsync(
            string? licensePlate,
            Guid vehicleTypeId,
            Guid gateId)
        {
            if (string.IsNullOrWhiteSpace(licensePlate)) return (null, new ResponseDTO("Vui lòng nhập biển số", 400, false));
            if (vehicleTypeId == Guid.Empty) return (null, new ResponseDTO("Vui lòng chọn loại phương tiện", 400, false));

            var vehicleTypeExists = await _unitOfWork.VehicleTypeRepo.AnyAsync(v => v.VehicleTypeId == vehicleTypeId);
            if (!vehicleTypeExists) return (null, new ResponseDTO("Loại phương tiện không tồn tại", 400, false));

            var gateValidation = await ValidateGateTypeAsync(gateId, EntryGateType);
            if (gateValidation != null) return (null, gateValidation);

            return (NormalizePlate(licensePlate), null);
        }

        private async Task<ResponseDTO?> ValidateGateTypeAsync(Guid gateId, string expectedGateType)
        {
            if (gateId == Guid.Empty) return new ResponseDTO("Vui lòng chọn cổng", 400, false);

            var gate = await _unitOfWork.GateRepo.GetByIdAsync(gateId);
            if (gate == null) return new ResponseDTO("Cổng không tồn tại", 400, false);
            if (!string.Equals(gate.GateType, expectedGateType, StringComparison.OrdinalIgnoreCase))
            {
                return new ResponseDTO($"Cổng phải là loại {expectedGateType}", 400, false);
            }

            return null;
        }

        private async Task<(Gate? Gate, ResponseDTO? Error)> ResolveGateByIdAsync(Guid gateId, string expectedGateType)
        {
            if (gateId == Guid.Empty) return (null, new ResponseDTO("Vui lòng chọn cổng", 400, false));

            var gate = await _unitOfWork.GateRepo.GetByIdAsync(gateId);

            if (gate == null) return (null, new ResponseDTO("Cổng không tồn tại", 400, false));
            if (!string.Equals(gate.GateType, expectedGateType, StringComparison.OrdinalIgnoreCase))
            {
                return (null, new ResponseDTO($"Cổng phải là loại {expectedGateType}", 400, false));
            }

            return (gate, null);
        }

        private async Task<ResponseDTO?> ValidateNoActiveSessionAsync(string licensePlate)
        {
            var hasActivePlate = await _unitOfWork.ParkingSessionRepo.HasActiveSessionByLicensePlateAsync(licensePlate);
            if (hasActivePlate) return new ResponseDTO("Biển số đang có phiên gửi xe active", 409, false);

            return null;
        }

        private async Task<ParkingSlot?> FindGuestAvailableSlotAsync(Guid vehicleTypeId)
        {
            var availableSlots = await _unitOfWork.ParkingSlotRepo.GetAvailableByVehicleTypeAndResidentFlagAsync(vehicleTypeId, false);
            if (availableSlots.Count == 0) return null;
            return availableSlots[Random.Shared.Next(availableSlots.Count)];
        }

        private async Task<ParkingSlot?> FindResidentAvailableSlotAsync(Guid vehicleTypeId)
        {
            var availableSlots = await _unitOfWork.ParkingSlotRepo.GetAvailableByVehicleTypeAndResidentFlagAsync(vehicleTypeId, true);
            return availableSlots.FirstOrDefault();
        }

        private async Task<(ParkingSession? Session, ResponseDTO? Error)> FindActiveSessionAsync(Guid? sessionId, string? licensePlate)
        {
            if (!sessionId.HasValue && string.IsNullOrWhiteSpace(licensePlate))
            {
                return (null, new ResponseDTO("Vui lòng nhập SessionId hoặc biển số xe", 400, false));
            }

            if (sessionId.HasValue && sessionId.Value == Guid.Empty)
            {
                return (null, new ResponseDTO("SessionId không hợp lệ", 400, false));
            }

            var session = await _unitOfWork.ParkingSessionRepo.GetActiveSessionWithDetailsAsync(sessionId, licensePlate);

            if (session == null) return (null, new ResponseDTO("Không tìm thấy phiên gửi xe active", 404, false));

            return (session, null);
        }

        private async Task<ResponseDTO?> ValidatePlateOutAsync(ParkingSession session, string? licensePlateOut)
        {
            if (string.IsNullOrWhiteSpace(licensePlateOut))
            {
                return new ResponseDTO("Vui lòng quét hoặc nhập biển số xe ra", 400, false);
            }

            var normalizedPlateOut = NormalizePlate(licensePlateOut);
            if (normalizedPlateOut == session.LicensePlateIn) return null;

            await CreateIncidentIfPossibleAsync(session, "PlateMismatch", $"Biển số ra {normalizedPlateOut} không khớp biển số vào {session.LicensePlateIn}.");
            await _unitOfWork.SaveChangeAsync();
            return new ResponseDTO("Biển số ra không khớp biển số vào", 409, false);
        }

        private async Task<(ParkingFeePreviewDTO? Preview, ResponseDTO? Error)> CalculateFeeAsync(ParkingSession session, DateTime exitTime)
        {
            var policy = await _unitOfWork.PricingPolicyRepo.GetActivePolicyAtAsync(session.VehicleTypeId, exitTime);

            if (policy == null) return (null, new ResponseDTO("Chưa cấu hình chính sách giá active cho loại phương tiện này", 400, false));

            var totalHours = Math.Max(0, (exitTime - session.EntryTime).TotalHours);
            var billedHours = Math.Max(1, (int)Math.Ceiling(totalHours));
            var amount = policy.BasePrice;

            if (billedHours > policy.BaseHours)
            {
                amount += (billedHours - policy.BaseHours) * policy.ExtraHourPrice;
            }

            if ((policy.NightSurcharge ?? 0) > 0 && HasNightOverlap(session.EntryTime, exitTime))
            {
                amount += policy.NightSurcharge ?? 0;
            }

            return (new ParkingFeePreviewDTO
            {
                SessionId = session.SessionId,
                LicensePlate = session.LicensePlateIn,
                EntryTime = session.EntryTime,
                ExitTime = exitTime,
                TotalHours = Math.Round(totalHours, 2),
                Amount = amount,
                PricingPolicyId = policy.PolicyId
            }, null);
        }

        private async Task CloseSessionAsync(ParkingSession session, Guid exitGateId, string? licensePlateOut, string? exitImageUrl, DateTime exitTime)
        {
            session.ExitGateId = exitGateId;
            session.ExitTime = exitTime;
            session.LicensePlateOut = string.IsNullOrWhiteSpace(licensePlateOut) ? session.LicensePlateIn : NormalizePlate(licensePlateOut);
            session.ExitImageUrl = NormalizeOptional(exitImageUrl);
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

        private IQueryable<ParkingSession> QuerySessionsWithIncludes()
        {
            return _unitOfWork.ParkingSessionRepo.GetAll()
                .Include(s => s.DriverUser)
                .Include(s => s.VehicleType)
                .Include(s => s.EntryGate)
                .Include(s => s.ExitGate)
                .Include(s => s.AssignedSlot)
                .Include(s => s.ActualSlot);
        }

        private async Task<ParkingSessionDTO?> GetSessionDTOAsync(Guid sessionId)
        {
            var session = await QuerySessionsWithIncludes().FirstOrDefaultAsync(s => s.SessionId == sessionId);
            return session == null ? null : ParkingSessionService.MapToDTO(session);
        }

        private async Task<ParkingSessionDTO?> GetSessionDTOWithTicketAsync(Guid sessionId)
        {
            var result = await GetSessionDTOAsync(sessionId);
            if (result != null)
            {
                result.Ticket = CreateSessionTicket(sessionId);
            }

            return result;
        }

        private static ParkingSessionTicketDTO CreateSessionTicket(Guid sessionId)
        {
            var qrPayload = sessionId.ToString();
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.Q);
            var pngQrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = pngQrCode.GetGraphic(20);
            var qrCodeBase64 = Convert.ToBase64String(qrCodeBytes);

            return new ParkingSessionTicketDTO
            {
                QrPayload = qrPayload,
                QrCodeDataUrl = $"data:image/png;base64,{qrCodeBase64}"
            };
        }

        private async Task CreateIncidentIfPossibleAsync(ParkingSession session, string issueType, string description)
        {
            if (!session.DriverUserId.HasValue) return;

            var incident = new IncidentReport
            {
                IncidentId = Guid.NewGuid(),
                SessionId = session.SessionId,
                ReportedByUserId = session.DriverUserId.Value,
                IssueType = issueType,
                Description = description,
                Status = IncidentStatus.Open.ToString()
            };

            await _unitOfWork.IncidentReportRepo.AddAsync(incident);
        }

        private static string NormalizePlate(string plate)
        {
            return plate.Trim().ToUpper();
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static bool FloorMatches(string? floorName, string[] keywords)
        {
            return !string.IsNullOrWhiteSpace(floorName)
                && keywords.Any(k => floorName.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasNightOverlap(DateTime entryTime, DateTime exitTime)
        {
            if (exitTime <= entryTime) return false;

            var cursor = entryTime;
            while (cursor <= exitTime)
            {
                if (cursor.Hour >= 22 || cursor.Hour < 6) return true;
                cursor = cursor.AddHours(1);
            }

            return exitTime.Hour >= 22 || exitTime.Hour < 6;
        }
    }
}
