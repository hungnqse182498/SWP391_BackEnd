using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.ParkingOperation;
using Common.DTOs.ParkingSession;
using Common.Enums;
using Common.Utilities;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.RegularExpressions;
using ZXing;
using ZXing.Common;

namespace BLL.Implements
{
    public class ParkingOperationService : IParkingOperationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPayOSService _payOSService;
        private const string EntryGateType = "Entry";
        private const string ExitGateType = "Exit";
        private const string CustomerTypeGuest = "Guest";
        private const string CustomerTypeResident = "Resident";
        private const string CustomerTypeReservation = "Reservation";
        private const string QrCodeTypeSession = "Session";
        private const string QrCodeTypeReservation = "Reservation";
        private const string QrCodeTypeMixed = "SessionAndReservation";
        private const double GuestCapacityPercent = 0.80;
        private static readonly string[] ReservationFloorKeywords = { "Đặt trước", "Dat truoc" };
        private static readonly Regex GuidRegex = new(
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public ParkingOperationService(IUnitOfWork unitOfWork, IPayOSService payOSService)
        {
            _unitOfWork = unitOfWork;
            _payOSService = payOSService;
        }

        public async Task<ResponseDTO> CheckInAsync(ParkingCheckInDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu check-in không hợp lệ", 400, false);

            return await HandleUnifiedCheckInAsync(dto);
        }

        public async Task<ResponseDTO> CheckOutAsync(ParkingCheckOutDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu check-out không hợp lệ", 400, false);

            return await HandleUnifiedCheckOutAsync(dto);
        }

        public async Task<ResponseDTO> GetFeePreviewAsync(Guid sessionId)
        {
            var sessionResult = await FindActiveSessionAsync(sessionId, null);
            if (sessionResult.Error != null) return sessionResult.Error;

            var session = sessionResult.Session!;
            var pendingPayment = await _unitOfWork.PaymentRepo.GetPendingCheckoutPaymentAsync(session.SessionId);
            var calculatedAt = pendingPayment?.PaymentTime ?? DateTime.UtcNow;
            var totalHours = Math.Max(0, (calculatedAt - session.EntryTime).TotalHours);
            var billedHours = Math.Max(1, (int)Math.Ceiling(totalHours));
            var subscription = await _unitOfWork.MonthlySubscriptionRepo
                .GetActiveByPlateAndVehicleTypeAsync(
                    session.LicensePlateIn,
                    session.VehicleTypeId,
                    calculatedAt);

            if (!session.ReservationId.HasValue && subscription != null)
            {
                return new ResponseDTO(
                    "Phí gửi xe đã được bao gồm trong gói tháng",
                    200,
                    true,
                    new ParkingFeePreviewDTO
                    {
                        SessionId = session.SessionId,
                        LicensePlate = session.LicensePlateIn,
                        EntryTime = session.EntryTime,
                        ExitTime = calculatedAt,
                        TotalHours = Math.Round(totalHours, 2),
                        BilledHours = billedHours,
                        Amount = 0,
                        IsCoveredBySubscription = true
                    });
            }

            var feeResult = await CalculateFeeAsync(session, calculatedAt);
            if (feeResult.Error != null) return feeResult.Error;

            return new ResponseDTO(
                "Tính phí gửi xe tạm tính thành công",
                200,
                true,
                feeResult.Preview);
        }

        public async Task<ResponseDTO> GetMyFeePreviewAsync(Guid sessionId, Guid userId)
        {
            var ownershipError = await ValidateSessionOwnerAsync(sessionId, userId);
            return ownershipError ?? await GetFeePreviewAsync(sessionId);
        }

        public async Task<ResponseDTO> GetMyCheckoutPaymentAsync(Guid sessionId, Guid userId)
        {
            var ownershipError = await ValidateSessionOwnerAsync(sessionId, userId);
            if (ownershipError != null) return ownershipError;

            var payment = await _unitOfWork.PaymentRepo.GetPendingCheckoutPaymentAsync(sessionId);
            if (payment == null)
            {
                return new ResponseDTO(
                    "Chưa có yêu cầu thanh toán checkout",
                    200,
                    true,
                    new { Payment = (object?)null, OnlinePayment = (object?)null });
            }

            object? onlinePayment = null;
            if (IsSameStatus(payment.PaymentMethod, PaymentMethod.PayOS.ToString()))
            {
                try
                {
                    var link = await _payOSService.GetPaymentLinkDetailsAsync(payment);
                    onlinePayment = new
                    {
                        link.PaymentUrl,
                        PaymentQrCodeDataUrl = string.IsNullOrWhiteSpace(link.QrCode)
                            ? null
                            : CreateQrCodeDataUrl(link.QrCode),
                        link.PaymentLinkId,
                        OrderCode = payment.TransactionReference
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseDTO($"Không thể tải liên kết thanh toán: {ex.Message}", 502, false);
                }
            }

            return new ResponseDTO(
                "Lấy yêu cầu thanh toán checkout thành công",
                200,
                true,
                new { Payment = MapOperationPayment(payment), OnlinePayment = onlinePayment });
        }

        public async Task<ResponseDTO> GetCheckoutPaymentStatusAsync(Guid paymentId)
        {
            var paymentResult = await FindCheckoutPaymentAsync(paymentId);
            if (paymentResult.Error != null) return paymentResult.Error;

            var payment = paymentResult.Payment!;
            return new ResponseDTO(
                "Lấy trạng thái thanh toán checkout thành công",
                200,
                true,
                new
                {
                    Payment = MapOperationPayment(payment),
                    Session = payment.SessionId.HasValue
                        ? await GetSessionDTOAsync(payment.SessionId.Value)
                        : null
                });
        }

        public async Task<ResponseDTO> ConfirmCashCheckoutAsync(Guid paymentId)
        {
            var paymentResult = await FindCheckoutPaymentAsync(paymentId);
            if (paymentResult.Error != null) return paymentResult.Error;

            var payment = paymentResult.Payment!;
            if (!IsSameStatus(payment.PaymentMethod, PaymentMethod.Cash.ToString()))
            {
                return new ResponseDTO("Chỉ thanh toán tiền mặt mới được nhân viên xác nhận thủ công", 400, false);
            }

            if (IsSameStatus(payment.PaymentStatus, PaymentStatus.Success.ToString()))
            {
                return new ResponseDTO("Thanh toán đã được xác nhận trước đó", 200, true, new
                {
                    Payment = MapOperationPayment(payment),
                    Session = payment.SessionId.HasValue ? await GetSessionDTOAsync(payment.SessionId.Value) : null
                });
            }

            if (!IsSameStatus(payment.PaymentStatus, PaymentStatus.Pending.ToString()))
            {
                return new ResponseDTO("Thanh toán đã bị hủy hoặc thất bại, không thể xác nhận", 409, false);
            }

            var sessionResult = await FindActiveSessionAsync(payment.SessionId, null);
            if (sessionResult.Error != null) return sessionResult.Error;

            var session = sessionResult.Session!;
            if (!session.ExitGateId.HasValue || !session.ExitTime.HasValue)
            {
                return new ResponseDTO("Yêu cầu checkout chưa có đủ thông tin cổng ra và thời gian ra", 409, false);
            }

            payment.PaymentStatus = PaymentStatus.Success.ToString();
            payment.PaymentTime = DateTime.UtcNow;
            await CloseSessionAsync(
                session,
                session.ExitGateId.Value,
                session.LicensePlateOut,
                session.ExitImageUrl,
                session.ExitTime.Value);
            await _unitOfWork.PaymentRepo.UpdateAsync(payment);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO("Đã nhận tiền mặt và checkout thành công", 200, true, new
            {
                Payment = MapOperationPayment(payment),
                Session = await GetSessionDTOAsync(session.SessionId)
            });
        }

        public async Task<ResponseDTO> CancelCheckoutAsync(Guid paymentId)
        {
            var paymentResult = await FindCheckoutPaymentAsync(paymentId);
            if (paymentResult.Error != null) return paymentResult.Error;

            var payment = paymentResult.Payment!;
            if (IsSameStatus(payment.PaymentStatus, PaymentStatus.Success.ToString()))
            {
                return new ResponseDTO("Thanh toán đã thành công nên không thể hủy checkout", 409, false);
            }

            if (IsSameStatus(payment.PaymentStatus, PaymentStatus.Failed.ToString()))
            {
                return new ResponseDTO("Yêu cầu checkout đã được hủy trước đó", 200, true, new
                {
                    Payment = MapOperationPayment(payment)
                });
            }

            if (IsSameStatus(payment.PaymentMethod, PaymentMethod.PayOS.ToString()))
            {
                try
                {
                    await _payOSService.CancelPaymentLinkAsync(payment);
                }
                catch (Exception ex)
                {
                    return new ResponseDTO(
                        $"Không thể hủy link PayOS nên checkout chưa được hủy: {ex.Message}",
                        502,
                        false);
                }
            }

            payment.PaymentStatus = PaymentStatus.Failed.ToString();
            await _unitOfWork.PaymentRepo.UpdateAsync(payment);
            await RestoreSessionAfterCancelledCheckoutAsync(payment.SessionId);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO("Đã hủy checkout; phiên gửi xe vẫn đang hoạt động", 200, true, new
            {
                Payment = MapOperationPayment(payment),
                Session = payment.SessionId.HasValue ? await GetSessionDTOAsync(payment.SessionId.Value) : null
            });
        }

        public async Task<ResponseDTO> DecodeQrImageAsync(
            Stream imageStream,
            string fileName,
            string? imageUrl = null,
            CancellationToken cancellationToken = default)
        {
            if (imageStream == null || !imageStream.CanRead)
            {
                return new ResponseDTO("File ảnh QR không hợp lệ", 400, false);
            }

            string? qrPayload;
            try
            {
                qrPayload = await DecodeQrPayloadFromImageAsync(imageStream, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnknownImageFormatException)
            {
                return new ResponseDTO("File upload không phải ảnh hợp lệ", 400, false);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi đọc mã QR từ ảnh {fileName}: {ex.Message}", 500, false);
            }

            if (string.IsNullOrWhiteSpace(qrPayload))
            {
                return new ResponseDTO("Không tìm thấy mã QR trong ảnh", 422, false, new { ImageUrl = imageUrl });
            }

            var resolved = await ResolveQrPayloadCoreAsync(qrPayload);
            if (resolved.Error != null) return resolved.Error;

            return new ResponseDTO("Đọc mã QR thành công", 200, true, new ParkingQrDecodeResultDTO
            {
                QrPayload = qrPayload.Trim(),
                CodeType = resolved.CodeType!,
                ReservationId = resolved.ReservationId,
                SessionId = resolved.SessionId,
                ImageUrl = imageUrl
            });
        }

        public async Task<ResponseDTO> ResolveQrPayloadAsync(string? qrPayload)
        {
            if (string.IsNullOrWhiteSpace(qrPayload))
            {
                return new ResponseDTO("Vui lòng gửi QrPayload", 400, false);
            }

            var resolved = await ResolveQrPayloadCoreAsync(qrPayload);
            if (resolved.Error != null) return resolved.Error;

            return new ResponseDTO("Đọc mã QR thành công", 200, true, new ParkingQrDecodeResultDTO
            {
                QrPayload = qrPayload.Trim(),
                CodeType = resolved.CodeType!,
                ReservationId = resolved.ReservationId,
                SessionId = resolved.SessionId
            });
        }

        private async Task<ResponseDTO> ProcessGuestCheckInAsync(ParkingCheckInDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.LicensePlate)) return new ResponseDTO("Vui lòng nhập biển số", 400, false);
            var vehicleTypeId = dto.VehicleTypeId.GetValueOrDefault();
            if (vehicleTypeId == Guid.Empty) return new ResponseDTO("Vui lòng chọn loại phương tiện", 400, false);

            var vehicleType = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(vehicleTypeId);
            if (vehicleType == null) return new ResponseDTO("Loại phương tiện không tồn tại", 400, false);

            var gateResult = await ResolveGateByIdAsync(dto.GateId, EntryGateType);
            if (gateResult.Error != null) return gateResult.Error;
            var floorValidation = ValidateFloorForCheckIn(gateResult.Gate!, vehicleTypeId, false);
            if (floorValidation != null) return floorValidation;

            var licensePlate = NormalizePlate(dto.LicensePlate);
            if (!LicensePlateNormalizer.IsValid(licensePlate))
                return new ResponseDTO("Biển số phải gồm 4-15 chữ cái và chữ số", 400, false);

            var activeSubscription = await _unitOfWork.MonthlySubscriptionRepo
                .GetActiveByPlateAndVehicleTypeAsync(licensePlate, vehicleTypeId, DateTime.UtcNow);
            if (activeSubscription != null)
            {
                return new ResponseDTO(
                    "Biển số này đang có gói tháng hợp lệ. Vui lòng check-in tại tầng cư dân / khách tháng",
                    403,
                    false);
            }

            var activeValidation = await ValidateNoActiveSessionAsync(licensePlate);
            if (activeValidation != null) return activeValidation;

            if (string.Equals(vehicleType.TypeName, "Ô tô", StringComparison.OrdinalIgnoreCase))
            {
                var floorId = gateResult.Gate!.FloorId;
                var totalCarSlots = await _unitOfWork.ParkingSlotRepo.GetAll()
                    .CountAsync(s => s.FloorId == floorId && s.VehicleTypeId == vehicleTypeId);
                var maximumGuestSlots = (int)(totalCarSlots * GuestCapacityPercent);
                var activeGuestSessions = await _unitOfWork.ParkingSessionRepo.GetAll()
                    .CountAsync(s => s.Status == SessionStatus.Active.ToString()
                                  && !s.ReservationId.HasValue
                                  && !s.DriverUserId.HasValue
                                  && s.VehicleTypeId == vehicleTypeId
                                  && s.ActualSlot != null
                                  && s.ActualSlot.FloorId == floorId);

                if (activeGuestSessions >= maximumGuestSlots)
                {
                    return new ResponseDTO(
                        "Khách vãng lai đã sử dụng hết giới hạn 80% của tầng. 20% chỗ còn lại được dành cho xe đặt trước.",
                        409,
                        false);
                }
            }

            var slot = await FindGuestAvailableSlotAsync(vehicleTypeId, gateResult.Gate!.FloorId);
            if (slot == null) return new ResponseDTO("Không còn chỗ trống cho khách vãng lai", 409, false);

            var now = DateTime.UtcNow;
            var session = new ParkingSession
            {
                SessionId = Guid.NewGuid(),
                LicensePlateIn = licensePlate,
                EntryImageUrl = NormalizeOptional(dto.EntryImageUrl),
                VehicleTypeId = vehicleTypeId,
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

        private async Task<ResponseDTO> ProcessPaidCheckOutAsync(ParkingCheckOutDTO dto, Guid sessionId)
        {
            if (sessionId == Guid.Empty) return new ResponseDTO("Vui lòng quét mã vé gửi xe", 400, false);

            var paymentMethod = NormalizeCheckoutPaymentMethod(dto.PaymentMethod);
            if (paymentMethod == null) return new ResponseDTO("Phương thức thanh toán chỉ được là Cash hoặc PayOS", 400, false);

            var exitGateResult = await ResolveGateByIdAsync(dto.GateId, ExitGateType);
            if (exitGateResult.Error != null) return exitGateResult.Error;

            var sessionResult = await FindActiveSessionAsync(sessionId, null);
            if (sessionResult.Error != null) return sessionResult.Error;

            var session = sessionResult.Session!;
            var exitFloorValidation = ValidateExitGateForSession(exitGateResult.Gate!, session);
            if (exitFloorValidation != null) return exitFloorValidation;
            var checkoutPlate = NormalizeOptional(dto.LicensePlateOut ?? dto.LicensePlate);
            var plateValidation = await ValidatePlateOutAsync(session, checkoutPlate);
            if (plateValidation != null) return plateValidation;

            var pendingPayment = await _unitOfWork.PaymentRepo.GetPendingCheckoutPaymentAsync(session.SessionId);
            if (pendingPayment != null)
            {
                var pendingFeeResult = await CalculateFeeAsync(
                    session,
                    session.ExitTime ?? pendingPayment.PaymentTime);
                if (pendingFeeResult.Error != null) return pendingFeeResult.Error;
                return new ResponseDTO("Phiên gửi xe này đang có yêu cầu thanh toán checkout chờ xử lý", 200, true, new
                {
                    Session = await GetSessionDTOAsync(session.SessionId),
                    Payment = MapOperationPayment(pendingPayment),
                    Fee = pendingFeeResult.Preview
                });
            }

            var exitTime = DateTime.UtcNow;
            var feeResult = await CalculateFeeAsync(session, exitTime);
            if (feeResult.Error != null) return feeResult.Error;

            if (feeResult.Preview!.Amount <= 0)
            {
                var coveredPayment = new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    SessionId = session.SessionId,
                    ReservationId = session.ReservationId,
                    Amount = 0,
                    PaymentMethod = paymentMethod,
                    PaymentType = PaymentType.CheckoutFee.ToString(),
                    PaymentTime = exitTime,
                    PaymentStatus = PaymentStatus.Success.ToString(),
                    TransactionReference = string.Empty
                };

                await CloseSessionAsync(
                    session,
                    exitGateResult.Gate!.GateId,
                    checkoutPlate,
                    dto.ExitImageUrl,
                    exitTime);
                await _unitOfWork.PaymentRepo.AddAsync(coveredPayment);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO(
                    "Tiền cọc đã thanh toán đủ phí gửi xe; checkout thành công và không cần thanh toán thêm",
                    200,
                    true,
                    new
                    {
                        Session = await GetSessionDTOAsync(session.SessionId),
                        Payment = MapOperationPayment(coveredPayment),
                        Fee = feeResult.Preview
                    });
            }

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                SessionId = session.SessionId,
                ReservationId = session.ReservationId,
                Amount = feeResult.Preview!.Amount,
                PaymentMethod = paymentMethod,
                PaymentType = PaymentType.CheckoutFee.ToString(),
                PaymentTime = exitTime,
                PaymentStatus = PaymentStatus.Pending.ToString(),
                TransactionReference = string.Empty
            };

            if (paymentMethod == PaymentMethod.Cash.ToString())
            {
                PrepareSessionForPendingOnlineCheckout(session, exitGateResult.Gate!.GateId, checkoutPlate, dto.ExitImageUrl, exitTime);
                await _unitOfWork.ParkingSessionRepo.UpdateAsync(session);
                await _unitOfWork.PaymentRepo.AddAsync(payment);
                await _unitOfWork.SaveChangeAsync();

                var cashResult = new
                {
                    Session = await GetSessionDTOAsync(session.SessionId),
                    Payment = MapOperationPayment(payment),
                    Fee = feeResult.Preview
                };

                return new ResponseDTO("Đã tạo yêu cầu thanh toán tiền mặt; cần xác nhận đã nhận tiền", 200, true, cashResult);
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                PrepareSessionForPendingOnlineCheckout(session, exitGateResult.Gate!.GateId, checkoutPlate, dto.ExitImageUrl, exitTime);
                await _unitOfWork.ParkingSessionRepo.UpdateAsync(session);
                await _unitOfWork.PaymentRepo.AddAsync(payment);
                await _unitOfWork.SaveAsync();

                var payOSPayment = await _payOSService.CreatePaymentLinkDetailsAsync(payment);
                if (string.IsNullOrWhiteSpace(payOSPayment.PaymentUrl) ||
                    string.IsNullOrWhiteSpace(payOSPayment.QrCode))
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return new ResponseDTO("PayOS không trả về đủ link và mã QR thanh toán", 500, false);
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
                        PaymentUrl = payOSPayment.PaymentUrl,
                        PaymentQrCodeDataUrl = CreateQrCodeDataUrl(payOSPayment.QrCode),
                        PaymentLinkId = payOSPayment.PaymentLinkId,
                        OrderCode = payment.TransactionReference
                    }
                };

                return new ResponseDTO("Đã tạo mã thanh toán PayOS; checkout chỉ hoàn tất sau khi thanh toán thành công", 200, true, payOSResult);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO($"Lỗi tạo thanh toán PayOS: {ex.Message}", 500, false);
            }
        }

        private async Task<ResponseDTO> ProcessResidentCheckInAsync(ParkingCheckInDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.LicensePlate)) return new ResponseDTO("Vui lòng nhập biển số", 400, false);
            var vehicleTypeId = dto.VehicleTypeId.GetValueOrDefault();
            if (vehicleTypeId == Guid.Empty) return new ResponseDTO("Vui lòng chọn loại phương tiện", 400, false);

            var vehicleTypeExists = await _unitOfWork.VehicleTypeRepo.AnyAsync(v => v.VehicleTypeId == vehicleTypeId);
            if (!vehicleTypeExists) return new ResponseDTO("Loại phương tiện không tồn tại", 400, false);

            var gateResult = await ResolveGateByIdAsync(dto.GateId, EntryGateType);
            if (gateResult.Error != null) return gateResult.Error;
            var floorValidation = ValidateFloorForCheckIn(gateResult.Gate!, vehicleTypeId, true);
            if (floorValidation != null) return floorValidation;

            var licensePlate = NormalizePlate(dto.LicensePlate);
            if (!LicensePlateNormalizer.IsValid(licensePlate))
                return new ResponseDTO("Biển số phải gồm 4-15 chữ cái và chữ số", 400, false);

            var now = DateTime.UtcNow;
            var subscription = await _unitOfWork.MonthlySubscriptionRepo
                .GetActiveByPlateAndVehicleTypeAsync(licensePlate, vehicleTypeId, now);

            if (subscription == null) return new ResponseDTO("Không tìm thấy gói tháng hợp lệ cho biển số này", 403, false);

            var activeValidation = await ValidateNoActiveSessionAsync(licensePlate);
            if (activeValidation != null) return activeValidation;

            var isMotorbike = IsMotorbike(subscription.VehicleType?.TypeName);
            ParkingSlot? slot = null;

            if (!isMotorbike)
            {
                var newlyAssignedSlot = false;
                if (!subscription.FixedSlotId.HasValue)
                {
                    slot = await FindResidentAvailableSlotAsync(vehicleTypeId, gateResult.Gate!.FloorId);
                    if (slot == null) return new ResponseDTO("Không còn vị trí ô tô trống tại tầng cư dân", 409, false);

                    newlyAssignedSlot = true;
                    slot.AssignedUserId = subscription.UserId;
                    subscription.FixedSlotId = slot.SlotId;
                    await _unitOfWork.MonthlySubscriptionRepo.UpdateAsync(subscription);
                }
                else
                {
                    slot = await _unitOfWork.ParkingSlotRepo.GetDetailWithFloorAndTypeAsync(subscription.FixedSlotId.Value);
                }

                if (slot == null ||
                    slot.VehicleTypeId != vehicleTypeId ||
                    slot.FloorId != gateResult.Gate!.FloorId ||
                    slot.Floor?.IsResident != true ||
                    slot.AssignedUserId != subscription.UserId ||
                    (!newlyAssignedSlot &&
                     slot.Status != ParkingSlotStatus.Assigned.ToString()))
                {
                    return new ResponseDTO("Vị trí ô tô của gói không khả dụng", 409, false);
                }
            }

            var session = new ParkingSession
            {
                SessionId = Guid.NewGuid(),
                DriverUserId = subscription.UserId,
                LicensePlateIn = licensePlate,
                EntryImageUrl = NormalizeOptional(dto.EntryImageUrl),
                VehicleTypeId = vehicleTypeId,
                EntryTime = now,
                EntryGateId = gateResult.Gate!.GateId,
                AssignedSlotId = slot?.SlotId,
                ActualSlotId = slot?.SlotId,
                Status = SessionStatus.Active.ToString()
            };

            if (slot != null)
            {
                slot.Status = ParkingSlotStatus.Occupied.ToString();
                await _unitOfWork.ParkingSlotRepo.UpdateAsync(slot);
            }

            await _unitOfWork.ParkingSessionRepo.AddAsync(session);
            await _unitOfWork.SaveChangeAsync();

            var result = await GetSessionDTOWithTicketAsync(session.SessionId);
            return new ResponseDTO("Check-in cư dân thành công", 201, true, result);
        }

        private async Task<ResponseDTO> ProcessResidentCheckOutAsync(ParkingCheckOutDTO dto, Guid sessionId)
        {
            if (sessionId == Guid.Empty) return new ResponseDTO("Vui lòng quét mã vé gửi xe", 400, false);

            var exitGateResult = await ResolveGateByIdAsync(dto.GateId, ExitGateType);
            if (exitGateResult.Error != null) return exitGateResult.Error;

            var sessionResult = await FindActiveSessionAsync(sessionId, null);
            if (sessionResult.Error != null) return sessionResult.Error;

            var session = sessionResult.Session!;
            var exitFloorValidation = ValidateExitGateForSession(exitGateResult.Gate!, session);
            if (exitFloorValidation != null) return exitFloorValidation;
            var now = DateTime.UtcNow;
            var subscription = await _unitOfWork.MonthlySubscriptionRepo
                .GetActiveByPlateAndVehicleTypeAsync(session.LicensePlateIn, session.VehicleTypeId, now);

            if (subscription == null) return new ResponseDTO("Phiên gửi xe này không thuộc gói tháng hợp lệ", 403, false);

            var checkoutPlate = NormalizeOptional(dto.LicensePlateOut ?? dto.LicensePlate);
            var plateValidation = await ValidatePlateOutAsync(session, checkoutPlate);
            if (plateValidation != null) return plateValidation;

            var isMotorbike = IsMotorbike(subscription.VehicleType?.TypeName);

            await CloseSessionAsync(
                session,
                exitGateResult.Gate!.GateId,
                checkoutPlate,
                dto.ExitImageUrl,
                now,
                !isMotorbike ? subscription.FixedSlotId : null);
            await _unitOfWork.SaveChangeAsync();

            var result = await GetSessionDTOAsync(session.SessionId);
            return new ResponseDTO("Checkout cư dân thành công", 200, true, result);
        }

        private async Task<ResponseDTO> ProcessReservationCheckInAsync(ParkingCheckInDTO dto, Guid reservationId)
        {
            if (reservationId == Guid.Empty) return new ResponseDTO("Vui lòng nhập ReservationId", 400, false);
            if (string.IsNullOrWhiteSpace(dto.LicensePlate)) return new ResponseDTO("Vui lòng nhập biển số", 400, false);

            var gateResult = await ResolveGateByIdAsync(dto.GateId, EntryGateType);
            if (gateResult.Error != null) return gateResult.Error;

            var reservation = await _unitOfWork.ReservationRepo.GetAll()
                .Include(r => r.User)
                .Include(r => r.VehicleType)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

            if (reservation == null) return new ResponseDTO("Không tìm thấy đặt chỗ", 404, false);

            var floorValidation = ValidateFloorForCheckIn(gateResult.Gate!, reservation.VehicleTypeId, false);
            if (floorValidation != null) return floorValidation;

            if (!IsSameStatus(reservation.Status, ReservationStatus.Confirmed.ToString()) &&
                !IsSameStatus(reservation.Status, ReservationStatus.Modified.ToString()))
            {
                return new ResponseDTO("Đặt chỗ phải ở trạng thái Confirmed hoặc Modified để check-in", 400, false);
            }

            var now = DateTime.UtcNow;
            var minEntryTime = reservation.ExpectedEntryTime.AddMinutes(-30);
            var maxEntryTime = reservation.ExpectedEntryTime.AddMinutes(30); 

            if (now < minEntryTime || now > maxEntryTime)
            {
                return new ResponseDTO("Ngoài khung giờ cho phép check-in đặt trước (Chỉ áp dụng trong khoảng -30p đến +30p so với giờ hẹn)", 400, false);
            }

            var licensePlate = NormalizePlate(dto.LicensePlate);
            if (!LicensePlateNormalizer.IsValid(licensePlate))
                return new ResponseDTO("Biển số phải gồm 4-15 chữ cái và chữ số", 400, false);

            var activeSubscription = await _unitOfWork.MonthlySubscriptionRepo
                .GetActiveByPlateAndVehicleTypeAsync(
                    licensePlate,
                    reservation.VehicleTypeId,
                    now);
            if (activeSubscription != null)
            {
                return new ResponseDTO(
                    "Biển số này đang có gói tháng hợp lệ. Không thể check-in bằng vé đặt trước; vui lòng check-in tại tầng cư dân / khách tháng",
                    403,
                    false);
            }

            var activeValidation = await ValidateNoActiveSessionAsync(licensePlate);
            if (activeValidation != null) return activeValidation;

            var availableSlot = await FindReservationAvailableSlotAsync(
                reservation.VehicleTypeId,
                gateResult.Gate!.FloorId);

            if (availableSlot == null)
            {
                return new ResponseDTO("Hệ thống hết vị trí trống khả dụng cho xe đặt trước tại thời điểm này", 409, false);
            }
          
            var session = new ParkingSession
            {
                SessionId = Guid.NewGuid(),
                ReservationId = reservation.ReservationId,
                DriverUserId = reservation.UserId,
                LicensePlateIn = licensePlate,
                EntryImageUrl = NormalizeOptional(dto.EntryImageUrl),
                VehicleTypeId = reservation.VehicleTypeId,
                EntryTime = now,
                EntryGateId = gateResult.Gate!.GateId,
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
                    AssignedSlots = g.Count(s => s.Status == ParkingSlotStatus.Assigned.ToString())
                })
                .OrderBy(a => a.FloorName)
                .ToList();

            return new ResponseDTO("Lấy tình trạng chỗ trống thành công", 200, true, result);
        }

        private static string? NormalizeCheckoutPaymentMethod(string? paymentMethod)
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

        private async Task<(Payment? Payment, ResponseDTO? Error)> FindCheckoutPaymentAsync(Guid paymentId)
        {
            if (paymentId == Guid.Empty)
            {
                return (null, new ResponseDTO("PaymentId không hợp lệ", 400, false));
            }

            var payment = await _unitOfWork.PaymentRepo.GetByIdAsync(paymentId);
            if (payment == null ||
                !IsSameStatus(payment.PaymentType, PaymentType.CheckoutFee.ToString()) ||
                !payment.SessionId.HasValue)
            {
                return (null, new ResponseDTO("Không tìm thấy thanh toán checkout", 404, false));
            }

            return (payment, null);
        }

        private async Task<ResponseDTO?> ValidateSessionOwnerAsync(Guid sessionId, Guid userId)
        {
            if (sessionId == Guid.Empty || userId == Guid.Empty)
                return new ResponseDTO("Thông tin phiên gửi xe không hợp lệ", 400, false);

            var session = await _unitOfWork.ParkingSessionRepo.GetByIdAsync(sessionId);
            if (session == null)
                return new ResponseDTO("Không tìm thấy phiên gửi xe", 404, false);
            if (!session.DriverUserId.HasValue || session.DriverUserId.Value != userId)
                return new ResponseDTO("Bạn không có quyền xem phiên gửi xe này", 403, false);

            return null;
        }

        private async Task RestoreSessionAfterCancelledCheckoutAsync(Guid? sessionId)
        {
            if (!sessionId.HasValue) return;

            var session = await _unitOfWork.ParkingSessionRepo.GetByIdAsync(sessionId.Value);
            if (session == null || !IsSameStatus(session.Status, SessionStatus.Active.ToString())) return;

            session.ExitGateId = null;
            session.ExitTime = null;
            session.LicensePlateOut = null;
            session.ExitImageUrl = null;
            await _unitOfWork.ParkingSessionRepo.UpdateAsync(session);
        }

        private async Task<ResponseDTO> HandleUnifiedCheckInAsync(ParkingCheckInDTO dto)
        {
            var customerType = NormalizeCustomerType(dto.CustomerType);
            if (customerType == null)
            {
                return new ResponseDTO("Vui lòng chọn CustomerType: Guest, Resident hoặc Reservation", 400, false);
            }

            if (customerType == CustomerTypeGuest)
            {
                return await ProcessGuestCheckInAsync(dto);
            }

            if (customerType == CustomerTypeResident)
            {
                return await ProcessResidentCheckInAsync(dto);
            }

            var reservationIdResult = await ResolveReservationIdForOperationAsync(dto.ReservationId, dto.QrPayload);
            if (reservationIdResult.Error != null) return reservationIdResult.Error;

            return await ProcessReservationCheckInAsync(dto, reservationIdResult.ReservationId);
        }

        private async Task<ResponseDTO> HandleUnifiedCheckOutAsync(ParkingCheckOutDTO dto)
        {
            var sessionIdResult = await ResolveSessionIdForOperationAsync(dto.SessionId, dto.QrPayload);
            if (sessionIdResult.Error != null) return sessionIdResult.Error;

            string? requestedCustomerType = null;
            if (!string.IsNullOrWhiteSpace(dto.CustomerType))
            {
                requestedCustomerType = NormalizeCustomerType(dto.CustomerType);
                if (requestedCustomerType == null)
                {
                    return new ResponseDTO("CustomerType chỉ được là Guest, Resident hoặc Reservation", 400, false);
                }
            }

            var inferred = await InferCheckoutCustomerTypeAsync(sessionIdResult.SessionId);
            if (inferred.Error != null) return inferred.Error;
            var customerType = inferred.CustomerType!;

            if (requestedCustomerType != null &&
                !string.Equals(requestedCustomerType, customerType, StringComparison.OrdinalIgnoreCase))
            {
                var actualTypeLabel = customerType switch
                {
                    CustomerTypeReservation => "xe đặt trước",
                    CustomerTypeResident => "cư dân / khách tháng",
                    _ => "khách vãng lai"
                };
                return new ResponseDTO(
                    $"Loại checkout không khớp: phiên gửi xe này thuộc {actualTypeLabel}",
                    400,
                    false);
            }

            if (customerType == CustomerTypeResident)
            {
                return await ProcessResidentCheckOutAsync(dto, sessionIdResult.SessionId);
            }

            return await ProcessPaidCheckOutAsync(dto, sessionIdResult.SessionId);
        }

        private async Task<(Guid ReservationId, ResponseDTO? Error)> ResolveReservationIdForOperationAsync(
            Guid? reservationId,
            string? qrPayload)
        {
            if (reservationId.HasValue)
            {
                return reservationId.Value == Guid.Empty
                    ? (Guid.Empty, new ResponseDTO("ReservationId không hợp lệ", 400, false))
                    : (reservationId.Value, null);
            }

            if (string.IsNullOrWhiteSpace(qrPayload))
            {
                return (Guid.Empty, new ResponseDTO("Vui lòng quét mã đặt chỗ hoặc gửi QrPayload", 400, false));
            }

            var resolved = await ResolveQrPayloadCoreAsync(qrPayload);
            if (resolved.Error != null) return (Guid.Empty, resolved.Error);
            if (!resolved.ReservationId.HasValue)
            {
                return (Guid.Empty, new ResponseDTO("Mã QR không phải mã đặt chỗ", 400, false));
            }

            return (resolved.ReservationId.Value, null);
        }

        private async Task<(Guid SessionId, ResponseDTO? Error)> ResolveSessionIdForOperationAsync(
            Guid? sessionId,
            string? qrPayload)
        {
            if (sessionId.HasValue)
            {
                return sessionId.Value == Guid.Empty
                    ? (Guid.Empty, new ResponseDTO("SessionId không hợp lệ", 400, false))
                    : (sessionId.Value, null);
            }

            if (string.IsNullOrWhiteSpace(qrPayload))
            {
                return (Guid.Empty, new ResponseDTO("Vui lòng quét mã vé gửi xe hoặc gửi QrPayload", 400, false));
            }

            var resolved = await ResolveQrPayloadCoreAsync(qrPayload);
            if (resolved.Error != null) return (Guid.Empty, resolved.Error);
            if (!resolved.SessionId.HasValue)
            {
                return (Guid.Empty, new ResponseDTO("Mã QR không phải mã vé gửi xe", 400, false));
            }

            return (resolved.SessionId.Value, null);
        }

        private async Task<(string? CustomerType, ResponseDTO? Error)> InferCheckoutCustomerTypeAsync(Guid sessionId)
        {
            var sessionResult = await FindActiveSessionAsync(sessionId, null);
            if (sessionResult.Error != null) return (null, sessionResult.Error);

            var session = sessionResult.Session!;
            if (session.ReservationId.HasValue)
            {
                return (CustomerTypeReservation, null);
            }

            var subscription = await _unitOfWork.MonthlySubscriptionRepo
                .GetActiveByPlateAndVehicleTypeAsync(session.LicensePlateIn, session.VehicleTypeId, DateTime.UtcNow);

            return (subscription == null ? CustomerTypeGuest : CustomerTypeResident, null);
        }

        private async Task<(Guid? ReservationId, Guid? SessionId, string? CodeType, ResponseDTO? Error)> ResolveQrPayloadCoreAsync(string? qrPayload)
        {
            var id = ExtractGuidFromQrPayload(qrPayload);
            if (!id.HasValue)
            {
                return (null, null, null, new ResponseDTO("Mã QR không chứa GUID hợp lệ", 400, false));
            }

            var reservationExists = await _unitOfWork.ReservationRepo.AnyAsync(r => r.ReservationId == id.Value);
            var sessionExists = await _unitOfWork.ParkingSessionRepo.AnyAsync(s => s.SessionId == id.Value);

            if (!reservationExists && !sessionExists)
            {
                return (null, null, null, new ResponseDTO("Mã QR không khớp mã đặt chỗ hoặc vé gửi xe", 404, false, new
                {
                    QrPayload = qrPayload?.Trim()
                }));
            }

            var codeType = reservationExists && sessionExists
                ? QrCodeTypeMixed
                : sessionExists ? QrCodeTypeSession : QrCodeTypeReservation;

            return (
                reservationExists ? id.Value : null,
                sessionExists ? id.Value : null,
                codeType,
                null);
        }

        private static async Task<string?> DecodeQrPayloadFromImageAsync(
            Stream imageStream,
            CancellationToken cancellationToken)
        {
            using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);
            var pixelBytes = new byte[image.Width * image.Height * 3];

            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++)
                    {
                        var offset = ((y * image.Width) + x) * 3;
                        pixelBytes[offset] = row[x].R;
                        pixelBytes[offset + 1] = row[x].G;
                        pixelBytes[offset + 2] = row[x].B;
                    }
                }
            });

            var luminanceSource = new RGBLuminanceSource(
                pixelBytes,
                image.Width,
                image.Height,
                RGBLuminanceSource.BitmapFormat.RGB24);

            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }
                }
            };

            return reader.Decode(luminanceSource)?.Text;
        }

        private static void PrepareSessionForPendingOnlineCheckout(ParkingSession session, Guid exitGateId, string? licensePlateOut, string? exitImageUrl, DateTime exitTime)
        {
            session.ExitGateId = exitGateId;
            session.ExitTime = exitTime;
            session.LicensePlateOut = string.IsNullOrWhiteSpace(licensePlateOut) ? session.LicensePlateIn : NormalizePlate(licensePlateOut);
            session.ExitImageUrl = NormalizeOptional(exitImageUrl);
        }

        private async Task<ResponseDTO?> ValidateGateTypeAsync(Guid gateId, string expectedGateType)
        {
            if (gateId == Guid.Empty) return new ResponseDTO("Vui lòng chọn cổng", 400, false);

            var gate = await _unitOfWork.GateRepo.GetByIdWithFloorAsync(gateId);
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

            var gate = await _unitOfWork.GateRepo.GetByIdWithFloorAsync(gateId);

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

        private static ResponseDTO? ValidateFloorForCheckIn(Gate gate, Guid vehicleTypeId, bool requiresResidentFloor)
        {
            if (gate.Floor == null)
            {
                return new ResponseDTO("Cổng chưa được gắn với tầng hợp lệ", 400, false);
            }

            if (gate.Floor.IsResident != requiresResidentFloor)
            {
                return new ResponseDTO(
                    requiresResidentFloor
                        ? "Cổng đã chọn không thuộc tầng cư dân"
                        : "Cổng đã chọn thuộc tầng cư dân, không dành cho khách vãng lai hoặc đặt trước",
                    400,
                    false);
            }

            if (gate.Floor.DedicatedVehicleTypeId.HasValue &&
                gate.Floor.DedicatedVehicleTypeId.Value != vehicleTypeId)
            {
                return new ResponseDTO("Loại phương tiện không đúng với tầng của cổng đã chọn", 400, false);
            }

            return null;
        }

        private static ResponseDTO? ValidateExitGateForSession(Gate gate, ParkingSession session)
        {
            var sessionFloorId = session.ActualSlot?.FloorId
                ?? session.AssignedSlot?.FloorId
                ?? session.EntryGate?.FloorId;

            if (!sessionFloorId.HasValue || sessionFloorId.Value != gate.FloorId)
            {
                return new ResponseDTO("Xe phải checkout tại cổng ra thuộc đúng tầng đang gửi", 400, false);
            }

            if (gate.Floor?.DedicatedVehicleTypeId is Guid dedicatedTypeId &&
                dedicatedTypeId != session.VehicleTypeId)
            {
                return new ResponseDTO("Loại phương tiện không đúng với tầng của cổng ra", 400, false);
            }

            return null;
        }

        private async Task<ParkingSlot?> FindGuestAvailableSlotAsync(Guid vehicleTypeId, Guid floorId)
        {
            var availableSlots = await _unitOfWork.ParkingSlotRepo.GetAvailableByVehicleTypeAndResidentFlagAsync(vehicleTypeId, false);
            var floorSlots = availableSlots.Where(slot => slot.FloorId == floorId).ToList();
            if (floorSlots.Count == 0) return null;
            return floorSlots[Random.Shared.Next(floorSlots.Count)];
        }

        private async Task<ParkingSlot?> FindResidentAvailableSlotAsync(Guid vehicleTypeId, Guid floorId)
        {
            var availableSlots = await _unitOfWork.ParkingSlotRepo.GetAvailableByVehicleTypeAndResidentFlagAsync(vehicleTypeId, true);
            var floorSlots = availableSlots.Where(slot => slot.FloorId == floorId).ToList();
            if (floorSlots.Count == 0) return null;
            return floorSlots[Random.Shared.Next(floorSlots.Count)];
        }

        private async Task<ParkingSlot?> FindReservationAvailableSlotAsync(Guid vehicleTypeId, Guid floorId)
        {
            var availableSlots = await _unitOfWork.ParkingSlotRepo.GetAvailableByVehicleTypeAndResidentFlagAsync(vehicleTypeId, false);
            return availableSlots.FirstOrDefault(slot => slot.FloorId == floorId);
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
            var nightSurchargeCount = (policy.NightSurcharge ?? 0) > 0
                ? CountNightSurcharges(session.EntryTime, exitTime)
                : 0;
            var hasNightSurcharge = nightSurchargeCount > 0;

            if (billedHours > policy.BaseHours)
            {
                amount += (billedHours - policy.BaseHours) * policy.ExtraHourPrice;
            }

            if (hasNightSurcharge)
            {
                amount += nightSurchargeCount * (policy.NightSurcharge ?? 0);
            }

            var grossAmount = amount;
            var depositAmount = 0m;
            if (session.ReservationId.HasValue)
            {
                var successfulDepositAmount = await _unitOfWork.PaymentRepo.GetAll()
                    .Where(payment =>
                        payment.ReservationId == session.ReservationId.Value &&
                        payment.PaymentType == PaymentType.Deposit.ToString() &&
                        payment.PaymentStatus == PaymentStatus.Success.ToString())
                    .SumAsync(payment => payment.Amount);

                depositAmount = Math.Min(grossAmount, successfulDepositAmount);
                amount = Math.Max(0, grossAmount - depositAmount);
            }

            return (new ParkingFeePreviewDTO
            {
                SessionId = session.SessionId,
                LicensePlate = session.LicensePlateIn,
                EntryTime = session.EntryTime,
                ExitTime = exitTime,
                TotalHours = Math.Round(totalHours, 2),
                BilledHours = billedHours,
                GrossAmount = grossAmount,
                DepositAmount = depositAmount,
                Amount = amount,
                PricingPolicyId = policy.PolicyId,
                BasePrice = policy.BasePrice,
                BaseHours = policy.BaseHours,
                ExtraHourPrice = policy.ExtraHourPrice,
                NightSurcharge = policy.NightSurcharge,
                NightSurchargeCount = nightSurchargeCount,
                HasNightSurcharge = hasNightSurcharge
            }, null);
        }

        private async Task CloseSessionAsync(
            ParkingSession session,
            Guid exitGateId,
            string? licensePlateOut,
            string? exitImageUrl,
            DateTime exitTime,
            Guid? assignedFixedSlotId = null)
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
                    actualSlot.Status = assignedFixedSlotId == actualSlot.SlotId
                        ? ParkingSlotStatus.Assigned.ToString()
                        : ParkingSlotStatus.Available.ToString();
                    await _unitOfWork.ParkingSlotRepo.UpdateAsync(actualSlot);
                }
            }

            if (session.AssignedSlotId.HasValue && session.AssignedSlotId != session.ActualSlotId)
            {
                var assignedSlot = await _unitOfWork.ParkingSlotRepo.GetByIdAsync(session.AssignedSlotId.Value);
                if (assignedSlot != null)
                {
                    assignedSlot.Status = assignedFixedSlotId == assignedSlot.SlotId
                        ? ParkingSlotStatus.Assigned.ToString()
                        : ParkingSlotStatus.Available.ToString();
                    await _unitOfWork.ParkingSlotRepo.UpdateAsync(assignedSlot);
                }
            }

            await CompleteReservationIfNeededAsync(session);
            await _unitOfWork.ParkingSessionRepo.UpdateAsync(session);
        }

        private static bool IsMotorbike(string? vehicleTypeName)
        {
            if (string.IsNullOrWhiteSpace(vehicleTypeName)) return false;

            var normalized = vehicleTypeName.Trim().ToLowerInvariant();
            return normalized.Contains("motor") ||
                   normalized.Contains("bike") ||
                   normalized.Contains("xe máy") ||
                   normalized.Contains("xe may");
        }

        private async Task CompleteReservationIfNeededAsync(ParkingSession session)
        {
            if (!session.ReservationId.HasValue) return;

            var reservation = await _unitOfWork.ReservationRepo.GetByIdAsync(session.ReservationId.Value);
            if (reservation == null) return;
            if (IsSameStatus(reservation.Status, ReservationStatus.Completed.ToString()) ||
                IsSameStatus(reservation.Status, ReservationStatus.Cancelled.ToString()) ||
                IsSameStatus(reservation.Status, ReservationStatus.NoShow.ToString()))
            {
                return;
            }

            reservation.Status = ReservationStatus.Completed.ToString();
            await _unitOfWork.ReservationRepo.UpdateAsync(reservation);
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

        internal static ParkingSessionTicketDTO CreateSessionTicket(Guid sessionId)
        {
            var qrPayload = sessionId.ToString();
            return new ParkingSessionTicketDTO
            {
                QrPayload = qrPayload,
                QrCodeDataUrl = CreateQrCodeDataUrl(qrPayload)
            };
        }

        private static string CreateQrCodeDataUrl(string payload)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var pngQrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = pngQrCode.GetGraphic(20);
            var qrCodeBase64 = Convert.ToBase64String(qrCodeBytes);
            return $"data:image/png;base64,{qrCodeBase64}";
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
            return LicensePlateNormalizer.Normalize(plate);
        }

        private static string? NormalizeCustomerType(string? customerType)
        {
            if (string.IsNullOrWhiteSpace(customerType)) return null;

            var normalized = customerType.Trim()
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .Replace(" ", string.Empty);

            if (string.Equals(normalized, CustomerTypeGuest, StringComparison.OrdinalIgnoreCase))
            {
                return CustomerTypeGuest;
            }

            if (string.Equals(normalized, CustomerTypeResident, StringComparison.OrdinalIgnoreCase))
            {
                return CustomerTypeResident;
            }

            if (string.Equals(normalized, CustomerTypeReservation, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Booking", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Reserved", StringComparison.OrdinalIgnoreCase))
            {
                return CustomerTypeReservation;
            }

            return null;
        }

        private static Guid? ExtractGuidFromQrPayload(string? qrPayload)
        {
            if (string.IsNullOrWhiteSpace(qrPayload)) return null;

            var trimmed = qrPayload.Trim();
            if (Guid.TryParse(trimmed, out var directGuid))
            {
                return directGuid;
            }

            var match = GuidRegex.Match(trimmed);
            return match.Success && Guid.TryParse(match.Value, out var parsedGuid)
                ? parsedGuid
                : null;
        }

        private static bool IsSameStatus(string? currentStatus, string expectedStatus)
        {
            return string.Equals(currentStatus, expectedStatus, StringComparison.OrdinalIgnoreCase);
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

        private static int CountNightSurcharges(DateTime entryTime, DateTime exitTime)
        {
            if (exitTime <= entryTime) return 0;

            var nightCount = 0;

            // Việt Nam là UTC+7, nên 22:00-06:00 giờ Việt Nam
            // tương ứng 15:00-23:00 UTC trong cùng một ngày UTC.
            for (
                var utcDate = entryTime.Date;
                utcDate <= exitTime.Date;
                utcDate = utcDate.AddDays(1))
            {
                var nightStartUtc = utcDate.AddHours(15);
                var nightEndUtc = utcDate.AddHours(23);
                if (entryTime < nightEndUtc && exitTime > nightStartUtc)
                {
                    nightCount++;
                }
            }

            return nightCount;
        }
    }
}
