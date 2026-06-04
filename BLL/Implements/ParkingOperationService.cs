using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.ParkingOperation;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements
{
    public class ParkingOperationService : IParkingOperationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private static readonly string[] GuestFloorKeywords = { "Vãng lai", "Vang lai" };
        private static readonly string[] ResidentFloorKeywords = { "Tháng", "Thang", "Cư dân", "Cu dan" };
        private static readonly string[] ReservationFloorKeywords = { "Đặt trước", "Dat truoc" };

        public ParkingOperationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GuestCheckInAsync(GuestCheckInDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu check-in khách vãng lai không hợp lệ", 400, false);

            var validation = await ValidateCheckInBaseAsync(dto.LicensePlate, dto.VehicleTypeId, dto.GateId, dto.CardId, dto.CardCode, true);
            if (validation.Error != null) return validation.Error;

            var activeValidation = await ValidateNoActiveSessionAsync(validation.LicensePlate!, validation.Card);
            if (activeValidation != null) return activeValidation;

            var slot = await FindAvailableSlotAsync(dto.VehicleTypeId, GuestFloorKeywords);
            if (slot == null) return new ResponseDTO("Không còn chỗ trống cho khách vãng lai", 409, false);

            var now = DateTime.Now;
            var session = new ParkingSession
            {
                SessionId = Guid.NewGuid(),
                CardId = validation.Card!.CardId,
                LicensePlateIn = validation.LicensePlate!,
                EntryImageUrl = NormalizeOptional(dto.EntryImageUrl),
                VehicleTypeId = dto.VehicleTypeId,
                EntryTime = now,
                EntryGateId = dto.GateId,
                AssignedSlotId = slot.SlotId,
                ActualSlotId = slot.SlotId,
                Status = "Active"
            };

            slot.Status = "Occupied";
            validation.Card.Status = "InUse";

            await _unitOfWork.ParkingSessionRepo.AddAsync(session);
            await _unitOfWork.ParkingSlotRepo.UpdateAsync(slot);
            await _unitOfWork.ParkingCardRepo.UpdateAsync(validation.Card);
            await _unitOfWork.SaveChangeAsync();

            var result = await GetSessionDTOAsync(session.SessionId);
            return new ResponseDTO("Check-in khách vãng lai thành công", 201, true, result);
        }

        public async Task<ResponseDTO> GuestCheckOutPreviewAsync(GuestCheckOutPreviewDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu xem trước checkout không hợp lệ", 400, false);

            var sessionResult = await FindActiveSessionAsync(dto.SessionId, dto.LicensePlate, dto.CardId, dto.CardCode, true);
            if (sessionResult.Error != null) return sessionResult.Error;

            var feeResult = await CalculateFeeAsync(sessionResult.Session!, DateTime.Now);
            if (feeResult.Error != null) return feeResult.Error;

            return new ResponseDTO("Tính phí gửi xe thành công", 200, true, feeResult.Preview);
        }

        public async Task<ResponseDTO> GuestCheckOutAsync(GuestCheckOutDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu checkout khách vãng lai không hợp lệ", 400, false);
            if (string.IsNullOrWhiteSpace(dto.PaymentMethod)) return new ResponseDTO("Vui lòng nhập phương thức thanh toán", 400, false);

            var exitGateValidation = await ValidateGateTypeAsync(dto.GateId, "Exit");
            if (exitGateValidation != null) return exitGateValidation;

            var sessionResult = await FindActiveSessionAsync(dto.SessionId, dto.LicensePlate, dto.CardId, dto.CardCode, true);
            if (sessionResult.Error != null) return sessionResult.Error;

            var session = sessionResult.Session!;
            var plateValidation = await ValidatePlateOutAsync(session, dto.LicensePlateOut);
            if (plateValidation != null) return plateValidation;

            var exitTime = DateTime.Now;
            var feeResult = await CalculateFeeAsync(session, exitTime);
            if (feeResult.Error != null) return feeResult.Error;

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                SessionId = session.SessionId,
                Amount = feeResult.Preview!.Amount,
                PaymentMethod = dto.PaymentMethod.Trim(),
                PaymentTime = exitTime,
                PaymentStatus = "Success",
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

            var validation = await ValidateCheckInBaseAsync(dto.LicensePlate, dto.VehicleTypeId, dto.GateId, dto.CardId, dto.CardCode, false);
            if (validation.Error != null) return validation.Error;

            var now = DateTime.Now;
            var subscription = await _unitOfWork.MonthlySubscriptionRepo.GetAll()
                .Include(s => s.User)
                .FirstOrDefaultAsync(s =>
                    s.LicensePlate.ToLower() == validation.LicensePlate!.ToLower()
                    && s.VehicleTypeId == dto.VehicleTypeId
                    && s.Status == "Active"
                    && s.StartDate <= now
                    && s.EndDate >= now);

            if (subscription == null) return new ResponseDTO("Không tìm thấy gói tháng hợp lệ cho biển số này", 403, false);

            var activeValidation = await ValidateNoActiveSessionAsync(validation.LicensePlate!, validation.Card);
            if (activeValidation != null) return activeValidation;

            var slot = await FindAvailableSlotAsync(dto.VehicleTypeId, ResidentFloorKeywords);
            if (slot == null) return new ResponseDTO("Không còn chỗ trống cho cư dân", 409, false);

            var session = new ParkingSession
            {
                SessionId = Guid.NewGuid(),
                CardId = validation.Card?.CardId,
                DriverUserId = subscription.UserId,
                LicensePlateIn = validation.LicensePlate!,
                EntryImageUrl = NormalizeOptional(dto.EntryImageUrl),
                VehicleTypeId = dto.VehicleTypeId,
                EntryTime = now,
                EntryGateId = dto.GateId,
                AssignedSlotId = slot.SlotId,
                ActualSlotId = slot.SlotId,
                Status = "Active"
            };

            slot.Status = "Occupied";
            if (validation.Card != null) validation.Card.Status = "InUse";

            await _unitOfWork.ParkingSessionRepo.AddAsync(session);
            await _unitOfWork.ParkingSlotRepo.UpdateAsync(slot);
            if (validation.Card != null) await _unitOfWork.ParkingCardRepo.UpdateAsync(validation.Card);
            await _unitOfWork.SaveChangeAsync();

            var result = await GetSessionDTOAsync(session.SessionId);
            return new ResponseDTO("Check-in cư dân thành công", 201, true, result);
        }

        public async Task<ResponseDTO> ResidentCheckOutAsync(ResidentCheckOutDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu checkout cư dân không hợp lệ", 400, false);

            var exitGateValidation = await ValidateGateTypeAsync(dto.GateId, "Exit");
            if (exitGateValidation != null) return exitGateValidation;

            var sessionResult = await FindActiveSessionAsync(dto.SessionId, dto.LicensePlate, dto.CardId, dto.CardCode, false);
            if (sessionResult.Error != null) return sessionResult.Error;

            var session = sessionResult.Session!;
            var subscription = await _unitOfWork.MonthlySubscriptionRepo.GetAll()
                .FirstOrDefaultAsync(s =>
                    s.LicensePlate.ToLower() == session.LicensePlateIn.ToLower()
                    && s.VehicleTypeId == session.VehicleTypeId
                    && s.Status == "Active");

            if (subscription == null) return new ResponseDTO("Phiên gửi xe này không thuộc gói tháng hợp lệ", 403, false);

            var plateValidation = await ValidatePlateOutAsync(session, dto.LicensePlateOut);
            if (plateValidation != null) return plateValidation;

            await CloseSessionAsync(session, dto.GateId, dto.LicensePlateOut, dto.ExitImageUrl, DateTime.Now);
            await _unitOfWork.SaveChangeAsync();

            var result = await GetSessionDTOAsync(session.SessionId);
            return new ResponseDTO("Checkout cư dân thành công", 200, true, result);
        }

        public async Task<ResponseDTO> ReservationCheckInAsync(ReservationCheckInDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu check-in đặt trước không hợp lệ", 400, false);
            if (dto.ReservationId == Guid.Empty) return new ResponseDTO("Vui lòng nhập ReservationId", 400, false);
            if (string.IsNullOrWhiteSpace(dto.LicensePlate)) return new ResponseDTO("Vui lòng nhập biển số", 400, false);

            var entryGateValidation = await ValidateGateTypeAsync(dto.GateId, "Entry");
            if (entryGateValidation != null) return entryGateValidation;

            var reservation = await _unitOfWork.ReservationRepo.GetAll()
                .Include(r => r.User)
                .Include(r => r.VehicleType)
                .Include(r => r.AssignedSlot)
                    .ThenInclude(s => s.Floor)
                .FirstOrDefaultAsync(r => r.ReservationId == dto.ReservationId);

            if (reservation == null) return new ResponseDTO("Không tìm thấy đặt chỗ", 404, false);
            if (!string.Equals(reservation.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                return new ResponseDTO("Đặt chỗ phải ở trạng thái Confirmed để check-in", 400, false);
            }

            var now = DateTime.Now;
            if (now < reservation.ExpectedEntryTime.AddMinutes(-30) || now > reservation.ExpectedExitTime)
            {
                return new ResponseDTO("Chưa đến hoặc đã quá thời gian check-in đặt trước", 400, false);
            }

            if (reservation.AssignedSlot.Status != "Reserved")
            {
                return new ResponseDTO("Vị trí đặt trước không còn ở trạng thái Reserved", 409, false);
            }

            if (!FloorMatches(reservation.AssignedSlot.Floor?.FloorName, ReservationFloorKeywords))
            {
                return new ResponseDTO("Vị trí đặt trước phải thuộc tầng ô tô đặt trước", 400, false);
            }

            var cardResult = await ResolveCardAsync(dto.CardId, dto.CardCode, false, true);
            if (cardResult.Error != null) return cardResult.Error;

            var licensePlate = NormalizePlate(dto.LicensePlate);
            var activeValidation = await ValidateNoActiveSessionAsync(licensePlate, cardResult.Card);
            if (activeValidation != null) return activeValidation;

            var session = new ParkingSession
            {
                SessionId = Guid.NewGuid(),
                CardId = cardResult.Card?.CardId,
                DriverUserId = reservation.UserId,
                LicensePlateIn = licensePlate,
                EntryImageUrl = NormalizeOptional(dto.EntryImageUrl),
                VehicleTypeId = reservation.VehicleTypeId,
                EntryTime = now,
                EntryGateId = dto.GateId,
                AssignedSlotId = reservation.AssignedSlotId,
                ActualSlotId = reservation.AssignedSlotId,
                Status = "Active"
            };

            reservation.Status = "CheckedIn";
            reservation.AssignedSlot.Status = "Occupied";
            if (cardResult.Card != null) cardResult.Card.Status = "InUse";

            await _unitOfWork.ParkingSessionRepo.AddAsync(session);
            await _unitOfWork.ReservationRepo.UpdateAsync(reservation);
            await _unitOfWork.ParkingSlotRepo.UpdateAsync(reservation.AssignedSlot);
            if (cardResult.Card != null) await _unitOfWork.ParkingCardRepo.UpdateAsync(cardResult.Card);
            await _unitOfWork.SaveChangeAsync();

            var result = await GetSessionDTOAsync(session.SessionId);
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
                    AvailableSlots = g.Count(s => s.Status == "Available"),
                    OccupiedSlots = g.Count(s => s.Status == "Occupied"),
                    ReservedSlots = g.Count(s => s.Status == "Reserved")
                })
                .OrderBy(a => a.FloorName)
                .ToList();

            return new ResponseDTO("Lấy tình trạng chỗ trống thành công", 200, true, result);
        }

        private async Task<(string? LicensePlate, ParkingCard? Card, ResponseDTO? Error)> ValidateCheckInBaseAsync(
            string? licensePlate,
            Guid vehicleTypeId,
            Guid gateId,
            Guid? cardId,
            string? cardCode,
            bool requireCard)
        {
            if (string.IsNullOrWhiteSpace(licensePlate)) return (null, null, new ResponseDTO("Vui lòng nhập biển số", 400, false));
            if (vehicleTypeId == Guid.Empty) return (null, null, new ResponseDTO("Vui lòng chọn loại phương tiện", 400, false));

            var vehicleTypeExists = await _unitOfWork.VehicleTypeRepo.AnyAsync(v => v.VehicleTypeId == vehicleTypeId);
            if (!vehicleTypeExists) return (null, null, new ResponseDTO("Loại phương tiện không tồn tại", 400, false));

            var gateValidation = await ValidateGateTypeAsync(gateId, "Entry");
            if (gateValidation != null) return (null, null, gateValidation);

            var cardResult = await ResolveCardAsync(cardId, cardCode, requireCard, true);
            if (cardResult.Error != null) return (null, null, cardResult.Error);

            return (NormalizePlate(licensePlate), cardResult.Card, null);
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

        private async Task<(ParkingCard? Card, ResponseDTO? Error)> ResolveCardAsync(Guid? cardId, string? cardCode, bool requireCard, bool requireActive)
        {
            if (!cardId.HasValue && string.IsNullOrWhiteSpace(cardCode))
            {
                return requireCard ? (null, new ResponseDTO("Vui lòng nhập thẻ xe", 400, false)) : (null, null);
            }

            ParkingCard? card = null;
            if (cardId.HasValue)
            {
                card = await _unitOfWork.ParkingCardRepo.GetByIdAsync(cardId.Value);
            }

            if (!string.IsNullOrWhiteSpace(cardCode))
            {
                var cardByCode = await _unitOfWork.ParkingCardRepo.FindByCodeAsync(cardCode);
                if (cardByCode == null) return (null, new ResponseDTO("Không tìm thấy mã thẻ xe", 404, false));
                if (card != null && card.CardId != cardByCode.CardId)
                {
                    return (null, new ResponseDTO("CardId và CardCode không khớp", 400, false));
                }
                card = cardByCode;
            }

            if (card == null) return (null, new ResponseDTO("Không tìm thấy thẻ xe", 404, false));
            if (requireActive && card.Status != "Active")
            {
                return (null, new ResponseDTO("Thẻ xe không ở trạng thái Active", 409, false));
            }

            return (card, null);
        }

        private async Task<ResponseDTO?> ValidateNoActiveSessionAsync(string licensePlate, ParkingCard? card)
        {
            var hasActivePlate = await _unitOfWork.ParkingSessionRepo.AnyAsync(s => s.Status == "Active" && s.LicensePlateIn.ToLower() == licensePlate.ToLower());
            if (hasActivePlate) return new ResponseDTO("Biển số đang có phiên gửi xe active", 409, false);

            if (card != null)
            {
                var hasActiveCard = await _unitOfWork.ParkingSessionRepo.AnyAsync(s => s.Status == "Active" && s.CardId == card.CardId);
                if (hasActiveCard) return new ResponseDTO("Thẻ xe đang có phiên gửi xe active", 409, false);
            }

            return null;
        }

        private async Task<ParkingSlot?> FindAvailableSlotAsync(Guid vehicleTypeId, string[] floorKeywords)
        {
            var slots = await _unitOfWork.ParkingSlotRepo.GetAll()
                .Include(s => s.Floor)
                .Where(s => s.VehicleTypeId == vehicleTypeId && s.Status == "Available")
                .OrderBy(s => s.SlotCode)
                .ToListAsync();

            return slots.FirstOrDefault(s => FloorMatches(s.Floor?.FloorName, floorKeywords));
        }

        private async Task<(ParkingSession? Session, ResponseDTO? Error)> FindActiveSessionAsync(Guid? sessionId, string? licensePlate, Guid? cardId, string? cardCode, bool requireCardWhenNoSessionId)
        {
            if (!sessionId.HasValue && string.IsNullOrWhiteSpace(licensePlate) && !cardId.HasValue && string.IsNullOrWhiteSpace(cardCode))
            {
                return (null, new ResponseDTO("Vui lòng nhập SessionId hoặc biển số/thẻ xe", 400, false));
            }

            var query = QuerySessionsWithIncludes().Where(s => s.Status == "Active");

            if (sessionId.HasValue)
            {
                query = query.Where(s => s.SessionId == sessionId.Value);
            }

            if (!string.IsNullOrWhiteSpace(licensePlate))
            {
                var plate = NormalizePlate(licensePlate);
                query = query.Where(s => s.LicensePlateIn.ToLower() == plate.ToLower());
            }

            ParkingCard? card = null;
            if (cardId.HasValue || !string.IsNullOrWhiteSpace(cardCode))
            {
                var cardResult = await ResolveCardAsync(cardId, cardCode, false, false);
                if (cardResult.Error != null) return (null, cardResult.Error);
                card = cardResult.Card;
                if (card != null) query = query.Where(s => s.CardId == card.CardId);
            }
            else if (requireCardWhenNoSessionId && !sessionId.HasValue)
            {
                return (null, new ResponseDTO("Vui lòng nhập thẻ xe khi không có SessionId", 400, false));
            }

            var session = await query.FirstOrDefaultAsync();
            if (session == null) return (null, new ResponseDTO("Không tìm thấy phiên gửi xe active", 404, false));

            return (session, null);
        }

        private async Task<ResponseDTO?> ValidatePlateOutAsync(ParkingSession session, string? licensePlateOut)
        {
            if (string.IsNullOrWhiteSpace(licensePlateOut)) return null;

            var normalizedPlateOut = NormalizePlate(licensePlateOut);
            if (normalizedPlateOut == session.LicensePlateIn) return null;

            await CreateIncidentIfPossibleAsync(session, "PlateMismatch", $"Biển số ra {normalizedPlateOut} không khớp biển số vào {session.LicensePlateIn}.");
            await _unitOfWork.SaveChangeAsync();
            return new ResponseDTO("Biển số ra không khớp biển số vào", 409, false);
        }

        private async Task<(ParkingFeePreviewDTO? Preview, ResponseDTO? Error)> CalculateFeeAsync(ParkingSession session, DateTime exitTime)
        {
            var policy = await _unitOfWork.PricingPolicyRepo.GetAll()
                .Where(p => p.VehicleTypeId == session.VehicleTypeId && p.Status == "Active" && p.EffectiveDate <= exitTime)
                .OrderByDescending(p => p.EffectiveDate)
                .FirstOrDefaultAsync();

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

            if (session.CardId.HasValue)
            {
                var card = await _unitOfWork.ParkingCardRepo.GetByIdAsync(session.CardId.Value);
                if (card != null)
                {
                    card.Status = "Active";
                    await _unitOfWork.ParkingCardRepo.UpdateAsync(card);
                }
            }

            await _unitOfWork.ParkingSessionRepo.UpdateAsync(session);
        }

        private IQueryable<ParkingSession> QuerySessionsWithIncludes()
        {
            return _unitOfWork.ParkingSessionRepo.GetAll()
                .Include(s => s.Card)
                .Include(s => s.DriverUser)
                .Include(s => s.VehicleType)
                .Include(s => s.EntryGate)
                .Include(s => s.ExitGate)
                .Include(s => s.AssignedSlot)
                .Include(s => s.ActualSlot);
        }

        private async Task<Common.DTOs.ParkingSession.ParkingSessionDTO?> GetSessionDTOAsync(Guid sessionId)
        {
            var session = await QuerySessionsWithIncludes().FirstOrDefaultAsync(s => s.SessionId == sessionId);
            return session == null ? null : ParkingSessionService.MapToDTO(session);
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
                Status = "Open"
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
