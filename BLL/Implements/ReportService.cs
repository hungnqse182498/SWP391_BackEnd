using System.Globalization;
using System.Text;
using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Reports;
using Common.Enums;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements
{
    public class ReportService : IReportService
    {
        private const int LatestRowLimit = 100;
        private static readonly string[] SupportedFormats = { "excel", "pdf" };
        private static readonly string[] SupportedReportTypes = { "summary", "revenue", "operations" };

        private readonly IUnitOfWork _unitOfWork;

        public ReportService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public Task<ResponseDTO> GetReportTypesAsync()
        {
            var reportTypes = new List<ReportTypeDTO>
            {
                new()
                {
                    Key = "summary",
                    Name = "Báo cáo tổng quan",
                    Description = "Tổng hợp doanh thu, lượt xe, đặt chỗ, gói tháng, sự cố và tình trạng chỗ đỗ.",
                    SupportedFormats = SupportedFormats
                },
                new()
                {
                    Key = "revenue",
                    Name = "Thống kê doanh thu",
                    Description = "Doanh thu thanh toán thành công theo thời gian, loại thanh toán và phương thức thanh toán.",
                    SupportedFormats = SupportedFormats
                },
                new()
                {
                    Key = "operations",
                    Name = "Thống kê vận hành bãi xe",
                    Description = "Lượt vào/ra, phiên đang hoạt động, đặt chỗ, gói tháng, sự cố và tỷ lệ sử dụng chỗ đỗ.",
                    SupportedFormats = SupportedFormats
                }
            };

            return Task.FromResult(new ResponseDTO("Lấy danh sách loại báo cáo thành công", 200, true, reportTypes));
        }

        public async Task<ResponseDTO> GetSummaryAsync(ReportFilterDTO filter)
        {
            var (range, error) = NormalizeFilter(filter);
            if (error != null) return error;

            var payments = await GetSuccessfulPaymentsAsync(range);
            var sessions = await GetEntrySessionsAsync(range);
            var exits = await GetExitSessionsAsync(range);
            var reservations = await GetReservationsAsync(range);
            var newSubscriptions = await GetNewSubscriptionsAsync(range);
            var activeSubscriptionCount = await CountActiveSubscriptionsAsync(range);
            var expiredSubscriptionCount = await CountExpiredSubscriptionsAsync(range);
            var expiringSubscriptionCount = await CountExpiringSubscriptionsAsync(range);
            var incidentOverview = await BuildIncidentOverviewAsync(range);
            var slots = await GetSlotsAsync(range);
            var floorOccupancy = await BuildFloorOccupancyAsync(range);

            var completedSessions = exits
                .Where(s => IsSameStatus(s.Status, SessionStatus.Completed.ToString()))
                .ToList();

            var slotOverview = BuildSlotOverview(slots);
            var totalRevenue = payments.Sum(p => p.Amount);
            var totalPayments = payments.Count;

            var summary = new ReportSummaryDTO
            {
                Range = range,
                RevenueSeries = BuildRevenueSeries(payments, range.GroupBy),
                RevenueByPaymentType = BuildPaymentBreakdown(payments, p => p.PaymentType),
                RevenueByPaymentMethod = BuildPaymentBreakdown(payments, p => p.PaymentMethod),
                Slots = slotOverview,
                SlotOccupancyByFloor = floorOccupancy,
                Metrics =
                {
                    Metric("totalRevenue", "Tổng doanh thu", totalRevenue, "VND"),
                    Metric("successfulPayments", "Số thanh toán thành công", totalPayments, "lần"),
                    Metric("entries", "Lượt xe vào", sessions.Count, "lượt"),
                    Metric("exits", "Lượt xe ra", exits.Count, "lượt"),
                    Metric("activeSessions", "Phiên đang gửi", await CountActiveSessionsAsync(range), "phiên"),
                    Metric("completedSessions", "Phiên hoàn tất", completedSessions.Count, "phiên"),
                    Metric("averageParkingMinutes", "Thời gian gửi trung bình", AverageParkingMinutes(completedSessions), "phút"),
                    Metric("reservations", "Lượt đặt chỗ", reservations.Count, "lượt"),
                    Metric("newSubscriptions", "Gói tháng tạo mới", newSubscriptions.Count, "gói"),
                    Metric("activeSubscriptions", "Gói tháng đang hoạt động", activeSubscriptionCount, "gói"),
                    Metric("expiredSubscriptions", "Gói tháng hết hạn", expiredSubscriptionCount, "gói"),
                    Metric("expiringSubscriptions", "Gói tháng sắp hết hạn 7 ngày", expiringSubscriptionCount, "gói"),
                    Metric("openIncidents", "Sự cố đang mở", incidentOverview.OpenIncidents, "sự cố"),
                    Metric("resolvedIncidents", "Sự cố đã xử lý trong kỳ", incidentOverview.ResolvedInRange, "sự cố"),
                    Metric("slotUtilizationRate", "Tỷ lệ sử dụng chỗ đỗ", slotOverview.UtilizationRate, "%")
                }
            };

            return new ResponseDTO("Tổng hợp báo cáo thành công", 200, true, summary);
        }

        public async Task<ResponseDTO> GetRevenueAsync(ReportFilterDTO filter)
        {
            var (range, error) = NormalizeFilter(filter);
            if (error != null) return error;

            var payments = await GetSuccessfulPaymentsAsync(range);
            var totalRevenue = payments.Sum(p => p.Amount);

            var report = new RevenueReportDTO
            {
                Range = range,
                TotalRevenue = totalRevenue,
                SuccessfulPaymentCount = payments.Count,
                AveragePaymentAmount = payments.Count == 0 ? 0 : Math.Round(totalRevenue / payments.Count, 2),
                RevenueSeries = BuildRevenueSeries(payments, range.GroupBy),
                ByPaymentType = BuildPaymentBreakdown(payments, p => p.PaymentType),
                ByPaymentMethod = BuildPaymentBreakdown(payments, p => p.PaymentMethod),
                LatestPayments = payments
                    .OrderByDescending(p => p.PaymentTime)
                    .Take(LatestRowLimit)
                    .Select(p => new RevenuePaymentRowDTO
                    {
                        PaymentId = p.PaymentId,
                        PaymentTime = p.PaymentTime,
                        PaymentType = p.PaymentType ?? "Unknown",
                        PaymentMethod = p.PaymentMethod ?? "Unknown",
                        Amount = p.Amount,
                        PaymentStatus = p.PaymentStatus ?? string.Empty,
                        TransactionReference = p.TransactionReference
                    })
                    .ToList()
            };

            return new ResponseDTO("Tính toán thống kê doanh thu thành công", 200, true, report);
        }

        public async Task<ResponseDTO> GetParkingOperationsAsync(ReportFilterDTO filter)
        {
            var (range, error) = NormalizeFilter(filter);
            if (error != null) return error;

            var sessions = await GetEntrySessionsAsync(range);
            var exits = await GetExitSessionsAsync(range);
            var reservations = await GetReservationsAsync(range);
            var newSubscriptions = await GetNewSubscriptionsAsync(range);
            var slots = await GetSlotsAsync(range);
            var incidentOverview = await BuildIncidentOverviewAsync(range);
            var completedSessions = exits
                .Where(s => IsSameStatus(s.Status, SessionStatus.Completed.ToString()))
                .ToList();

            var report = new ParkingOperationReportDTO
            {
                Range = range,
                Sessions = new ParkingSessionOverviewDTO
                {
                    Entries = sessions.Count,
                    Exits = exits.Count,
                    ActiveSessions = await CountActiveSessionsAsync(range),
                    CompletedSessions = completedSessions.Count,
                    AverageParkingMinutes = AverageParkingMinutes(completedSessions)
                },
                Reservations = new ReservationOverviewDTO
                {
                    Total = reservations.Count,
                    Pending = CountByStatus(reservations, r => r.Status, ReservationStatus.Pending.ToString()),
                    Confirmed = CountByStatus(reservations, r => r.Status, ReservationStatus.Confirmed.ToString()),
                    CheckedIn = CountByStatus(reservations, r => r.Status, ReservationStatus.CheckedIn.ToString()),
                    Completed = CountByStatus(reservations, r => r.Status, ReservationStatus.Completed.ToString()),
                    Cancelled = CountByStatus(reservations, r => r.Status, ReservationStatus.Cancelled.ToString()),
                    NoShow = CountByStatus(reservations, r => r.Status, ReservationStatus.NoShow.ToString())
                },
                Subscriptions = new SubscriptionOverviewDTO
                {
                    NewSubscriptions = newSubscriptions.Count,
                    ActiveSubscriptions = await CountActiveSubscriptionsAsync(range),
                    ExpiredSubscriptions = await CountExpiredSubscriptionsAsync(range),
                    ExpiringInNext7Days = await CountExpiringSubscriptionsAsync(range)
                },
                Incidents = incidentOverview,
                Slots = BuildSlotOverview(slots),
                SessionsByVehicleType = BuildCountBreakdown(sessions, s => s.VehicleType?.TypeName),
                ReservationsByStatus = BuildCountBreakdown(reservations, r => r.Status),
                SubscriptionsByStatus = BuildCountBreakdown(newSubscriptions, s => s.Status),
                IncidentsByStatus = await BuildIncidentStatusBreakdownAsync(range),
                SlotOccupancyByFloor = await BuildFloorOccupancyAsync(range),
                LatestSessions = sessions
                    .OrderByDescending(s => s.EntryTime)
                    .Take(LatestRowLimit)
                    .Select(s => new ParkingSessionReportRowDTO
                    {
                        SessionId = s.SessionId,
                        LicensePlate = s.LicensePlateIn,
                        VehicleTypeName = s.VehicleType?.TypeName ?? string.Empty,
                        EntryTime = s.EntryTime,
                        ExitTime = s.ExitTime,
                        Status = s.Status ?? string.Empty
                    })
                    .ToList()
            };

            return new ResponseDTO("Tính toán thống kê vận hành thành công", 200, true, report);
        }

        public async Task<ResponseDTO> ExportAsync(ReportExportRequestDTO request)
        {
            var reportType = NormalizeToken(request.ReportType);
            var format = NormalizeToken(request.Format);

            if (!SupportedReportTypes.Contains(reportType))
            {
                return new ResponseDTO("Loại báo cáo chỉ được là summary, revenue hoặc operations", 400, false);
            }

            if (!SupportedFormats.Contains(format))
            {
                return new ResponseDTO("Định dạng xuất báo cáo chỉ được là excel hoặc pdf", 400, false);
            }

            ResponseDTO reportResponse = reportType switch
            {
                "revenue" => await GetRevenueAsync(request),
                "operations" => await GetParkingOperationsAsync(request),
                _ => await GetSummaryAsync(request)
            };

            if (!reportResponse.IsSuccess || reportResponse.Result == null)
            {
                return reportResponse;
            }

            var rows = BuildExportRows(reportType, reportResponse.Result);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var fileBaseName = $"report_{reportType}_{timestamp}";

            ReportExportFileDTO file = format == "pdf"
                ? new ReportExportFileDTO
                {
                    Content = BuildPdf(fileBaseName, RowsToPdfLines(rows)),
                    ContentType = "application/pdf",
                    FileName = $"{fileBaseName}.pdf"
                }
                : new ReportExportFileDTO
                {
                    Content = BuildCsv(rows),
                    ContentType = "application/vnd.ms-excel; charset=utf-8",
                    FileName = $"{fileBaseName}.csv"
                };

            return new ResponseDTO("Xuất báo cáo thành công", 200, true, file);
        }

        private async Task<List<Payment>> GetSuccessfulPaymentsAsync(ReportRangeDTO range)
        {
            var query = _unitOfWork.PaymentRepo.GetAll()
                .AsNoTracking()
                .Include(p => p.Session)
                .Include(p => p.Reservation)
                .Include(p => p.Subscription)
                .Where(p =>
                    p.PaymentStatus == PaymentStatus.Success.ToString() &&
                    p.PaymentTime >= range.From &&
                    p.PaymentTime <= range.To);

            if (range.VehicleTypeId.HasValue)
            {
                var vehicleTypeId = range.VehicleTypeId.Value;
                query = query.Where(p =>
                    (p.Session != null && p.Session.VehicleTypeId == vehicleTypeId) ||
                    (p.Reservation != null && p.Reservation.VehicleTypeId == vehicleTypeId) ||
                    (p.Subscription != null && p.Subscription.VehicleTypeId == vehicleTypeId));
            }

            return await query.ToListAsync();
        }

        private async Task<List<ParkingSession>> GetEntrySessionsAsync(ReportRangeDTO range)
        {
            var query = _unitOfWork.ParkingSessionRepo.GetAll()
                .AsNoTracking()
                .Include(s => s.VehicleType)
                .Where(s => s.EntryTime >= range.From && s.EntryTime <= range.To);

            if (range.VehicleTypeId.HasValue)
            {
                query = query.Where(s => s.VehicleTypeId == range.VehicleTypeId.Value);
            }

            return await query.ToListAsync();
        }

        private async Task<List<ParkingSession>> GetExitSessionsAsync(ReportRangeDTO range)
        {
            var query = _unitOfWork.ParkingSessionRepo.GetAll()
                .AsNoTracking()
                .Include(s => s.VehicleType)
                .Where(s => s.ExitTime.HasValue && s.ExitTime.Value >= range.From && s.ExitTime.Value <= range.To);

            if (range.VehicleTypeId.HasValue)
            {
                query = query.Where(s => s.VehicleTypeId == range.VehicleTypeId.Value);
            }

            return await query.ToListAsync();
        }

        private async Task<int> CountActiveSessionsAsync(ReportRangeDTO range)
        {
            var query = _unitOfWork.ParkingSessionRepo.GetAll()
                .AsNoTracking()
                .Where(s => s.Status == SessionStatus.Active.ToString());

            if (range.VehicleTypeId.HasValue)
            {
                query = query.Where(s => s.VehicleTypeId == range.VehicleTypeId.Value);
            }

            return await query.CountAsync();
        }

        private async Task<List<Reservation>> GetReservationsAsync(ReportRangeDTO range)
        {
            var query = _unitOfWork.ReservationRepo.GetAll()
                .AsNoTracking()
                .Include(r => r.VehicleType)
                .Where(r => r.ExpectedEntryTime >= range.From && r.ExpectedEntryTime <= range.To);

            if (range.VehicleTypeId.HasValue)
            {
                query = query.Where(r => r.VehicleTypeId == range.VehicleTypeId.Value);
            }

            return await query.ToListAsync();
        }

        private async Task<List<MonthlySubscription>> GetNewSubscriptionsAsync(ReportRangeDTO range)
        {
            var query = _unitOfWork.MonthlySubscriptionRepo.GetAll()
                .AsNoTracking()
                .Include(s => s.VehicleType)
                .Where(s => s.StartDate >= range.From && s.StartDate <= range.To);

            if (range.VehicleTypeId.HasValue)
            {
                query = query.Where(s => s.VehicleTypeId == range.VehicleTypeId.Value);
            }

            return await query.ToListAsync();
        }

        private async Task<int> CountActiveSubscriptionsAsync(ReportRangeDTO range)
        {
            var now = DateTime.Now;
            var query = _unitOfWork.MonthlySubscriptionRepo.GetAll()
                .AsNoTracking()
                .Where(s => s.Status == MonthlySubscriptionStatus.Active.ToString() && s.EndDate >= now);

            if (range.VehicleTypeId.HasValue)
            {
                query = query.Where(s => s.VehicleTypeId == range.VehicleTypeId.Value);
            }

            return await query.CountAsync();
        }

        private async Task<int> CountExpiredSubscriptionsAsync(ReportRangeDTO range)
        {
            var now = DateTime.Now;
            var query = _unitOfWork.MonthlySubscriptionRepo.GetAll()
                .AsNoTracking()
                .Where(s => s.Status == MonthlySubscriptionStatus.Expired.ToString() || s.EndDate < now);

            if (range.VehicleTypeId.HasValue)
            {
                query = query.Where(s => s.VehicleTypeId == range.VehicleTypeId.Value);
            }

            return await query.CountAsync();
        }

        private async Task<int> CountExpiringSubscriptionsAsync(ReportRangeDTO range)
        {
            var now = DateTime.Now;
            var sevenDaysLater = now.AddDays(7);

            var query = _unitOfWork.MonthlySubscriptionRepo.GetAll()
                .AsNoTracking()
                .Where(s =>
                    s.Status == MonthlySubscriptionStatus.Active.ToString() &&
                    s.EndDate >= now &&
                    s.EndDate <= sevenDaysLater);

            if (range.VehicleTypeId.HasValue)
            {
                query = query.Where(s => s.VehicleTypeId == range.VehicleTypeId.Value);
            }

            return await query.CountAsync();
        }

        private async Task<IncidentOverviewDTO> BuildIncidentOverviewAsync(ReportRangeDTO range)
        {
            var incidents = await GetIncidentsAsync(range);

            return new IncidentOverviewDTO
            {
                OpenIncidents = CountByStatus(incidents, i => i.Status, IncidentStatus.Open.ToString()),
                InProgressIncidents = CountByStatus(incidents, i => i.Status, IncidentStatus.InProgress.ToString()),
                CancelledIncidents = CountByStatus(incidents, i => i.Status, IncidentStatus.Cancelled.ToString()),
                ResolvedInRange = incidents.Count(i =>
                    IsSameStatus(i.Status, IncidentStatus.Resolved.ToString()) &&
                    i.ResolvedAt.HasValue &&
                    i.ResolvedAt.Value >= range.From &&
                    i.ResolvedAt.Value <= range.To)
            };
        }

        private async Task<List<ReportBreakdownDTO>> BuildIncidentStatusBreakdownAsync(ReportRangeDTO range)
        {
            var incidents = await GetIncidentsAsync(range);
            return BuildCountBreakdown(incidents, i => i.Status);
        }

        private async Task<List<IncidentReport>> GetIncidentsAsync(ReportRangeDTO range)
        {
            var query = _unitOfWork.IncidentReportRepo.GetAll()
                .AsNoTracking()
                .Include(i => i.Session)
                .AsQueryable();

            if (range.VehicleTypeId.HasValue)
            {
                query = query.Where(i => i.Session != null && i.Session.VehicleTypeId == range.VehicleTypeId.Value);
            }

            return await query.ToListAsync();
        }

        private async Task<List<ParkingSlot>> GetSlotsAsync(ReportRangeDTO range)
        {
            var query = _unitOfWork.ParkingSlotRepo.GetAll()
                .AsNoTracking()
                .Include(s => s.Floor)
                .Include(s => s.VehicleType)
                .AsQueryable();

            if (range.VehicleTypeId.HasValue)
            {
                query = query.Where(s => s.VehicleTypeId == range.VehicleTypeId.Value);
            }

            return await query.ToListAsync();
        }

        private async Task<List<FloorOccupancyDTO>> BuildFloorOccupancyAsync(ReportRangeDTO range)
        {
            var slots = await GetSlotsAsync(range);

            return slots
                .GroupBy(s => new
                {
                    s.FloorId,
                    FloorName = s.Floor != null ? s.Floor.FloorName : "Unknown"
                })
                .OrderBy(g => g.Key.FloorName)
                .Select(g =>
                {
                    var floorSlots = g.ToList();
                    var total = floorSlots.Count;
                    var available = CountByStatus(floorSlots, s => s.Status, ParkingSlotStatus.Available.ToString());
                    var occupied = CountByStatus(floorSlots, s => s.Status, ParkingSlotStatus.Occupied.ToString());
                    var reserved = CountByStatus(floorSlots, s => s.Status, ParkingSlotStatus.Reserved.ToString());
                    var assigned = CountByStatus(floorSlots, s => s.Status, ParkingSlotStatus.Assigned.ToString());

                    return new FloorOccupancyDTO
                    {
                        FloorId = g.Key.FloorId,
                        FloorName = g.Key.FloorName,
                        TotalSlots = total,
                        AvailableSlots = available,
                        OccupiedSlots = occupied,
                        ReservedSlots = reserved,
                        AssignedSlots = assigned,
                        UtilizationRate = Percent(occupied + reserved + assigned, total)
                    };
                })
                .ToList();
        }

        private static SlotOverviewDTO BuildSlotOverview(List<ParkingSlot> slots)
        {
            var total = slots.Count;
            var available = CountByStatus(slots, s => s.Status, ParkingSlotStatus.Available.ToString());
            var occupied = CountByStatus(slots, s => s.Status, ParkingSlotStatus.Occupied.ToString());
            var reserved = CountByStatus(slots, s => s.Status, ParkingSlotStatus.Reserved.ToString());
            var assigned = CountByStatus(slots, s => s.Status, ParkingSlotStatus.Assigned.ToString());

            return new SlotOverviewDTO
            {
                TotalSlots = total,
                AvailableSlots = available,
                OccupiedSlots = occupied,
                ReservedSlots = reserved,
                AssignedSlots = assigned,
                UtilizationRate = Percent(occupied + reserved + assigned, total)
            };
        }

        private static List<ReportSeriesPointDTO> BuildRevenueSeries(List<Payment> payments, string groupBy)
        {
            return payments
                .GroupBy(p => GetPeriodKey(p.PaymentTime, groupBy))
                .OrderBy(g => g.Key)
                .Select(g => new ReportSeriesPointDTO
                {
                    Period = g.Key,
                    Count = g.Count(),
                    Amount = g.Sum(p => p.Amount)
                })
                .ToList();
        }

        private static List<ReportBreakdownDTO> BuildPaymentBreakdown(List<Payment> payments, Func<Payment, string?> keySelector)
        {
            var totalAmount = payments.Sum(p => p.Amount);

            return payments
                .GroupBy(p => NormalizeGroupName(keySelector(p)))
                .OrderByDescending(g => g.Sum(p => p.Amount))
                .Select(g =>
                {
                    var amount = g.Sum(p => p.Amount);
                    return new ReportBreakdownDTO
                    {
                        Name = g.Key,
                        Count = g.Count(),
                        Amount = amount,
                        Percent = totalAmount <= 0 ? 0 : Math.Round(amount / totalAmount * 100, 2)
                    };
                })
                .ToList();
        }

        private static List<ReportBreakdownDTO> BuildCountBreakdown<T>(List<T> items, Func<T, string?> keySelector)
        {
            var total = items.Count;

            return items
                .GroupBy(item => NormalizeGroupName(keySelector(item)))
                .OrderByDescending(g => g.Count())
                .Select(g => new ReportBreakdownDTO
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Amount = 0,
                    Percent = Percent(g.Count(), total)
                })
                .ToList();
        }

        private static ReportMetricDTO Metric(string key, string label, decimal value, string unit)
        {
            return new ReportMetricDTO
            {
                Key = key,
                Label = label,
                Value = value,
                Unit = unit
            };
        }

        private static int CountByStatus<T>(IEnumerable<T> items, Func<T, string?> selector, string status)
        {
            return items.Count(item => IsSameStatus(selector(item), status));
        }

        private static decimal AverageParkingMinutes(List<ParkingSession> sessions)
        {
            var validDurations = sessions
                .Where(s => s.ExitTime.HasValue && s.ExitTime.Value >= s.EntryTime)
                .Select(s => (decimal)(s.ExitTime!.Value - s.EntryTime).TotalMinutes)
                .ToList();

            return validDurations.Count == 0
                ? 0
                : Math.Round(validDurations.Average(), 2);
        }

        private static decimal Percent(decimal value, decimal total)
        {
            return total <= 0 ? 0 : Math.Round(value / total * 100, 2);
        }

        private static string GetPeriodKey(DateTime value, string groupBy)
        {
            return groupBy == "month"
                ? value.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                : value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string NormalizeGroupName(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
        }

        private static string NormalizeToken(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private static bool IsSameStatus(string? actual, string expected)
        {
            return string.Equals(actual?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
        }

        private static (ReportRangeDTO Range, ResponseDTO? Error) NormalizeFilter(ReportFilterDTO? filter)
        {
            filter ??= new ReportFilterDTO();

            var from = (filter.From ?? DateTime.Today.AddDays(-30)).Date;
            var to = (filter.To ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            var groupBy = NormalizeToken(filter.GroupBy);
            groupBy = string.IsNullOrWhiteSpace(groupBy) ? "day" : groupBy;

            if (from > to)
            {
                return (new ReportRangeDTO(), new ResponseDTO("Ngày bắt đầu không được lớn hơn ngày kết thúc", 400, false));
            }

            if (groupBy is not ("day" or "month"))
            {
                return (new ReportRangeDTO(), new ResponseDTO("GroupBy chỉ được là day hoặc month", 400, false));
            }

            return (new ReportRangeDTO
            {
                From = from,
                To = to,
                GroupBy = groupBy,
                VehicleTypeId = filter.VehicleTypeId
            }, null);
        }

        private static List<string[]> BuildExportRows(string reportType, object report)
        {
            return reportType switch
            {
                "revenue" when report is RevenueReportDTO revenue => BuildRevenueRows(revenue),
                "operations" when report is ParkingOperationReportDTO operations => BuildOperationRows(operations),
                _ when report is ReportSummaryDTO summary => BuildSummaryRows(summary),
                _ => new List<string[]> { new[] { "Không có dữ liệu báo cáo để xuất" } }
            };
        }

        private static List<string[]> BuildSummaryRows(ReportSummaryDTO report)
        {
            var rows = BaseRows("Báo cáo tổng quan", report.Range);
            rows.Add(new[] { "Chỉ số", "Giá trị", "Đơn vị" });
            rows.AddRange(report.Metrics.Select(m => new[] { m.Label, FormatValue(m.Value), m.Unit }));

            rows.Add(Section("Doanh thu theo thời gian"));
            rows.Add(new[] { "Kỳ", "Số thanh toán", "Doanh thu" });
            rows.AddRange(report.RevenueSeries.Select(x => new[] { x.Period, x.Count.ToString(), FormatValue(x.Amount) }));

            rows.Add(Section("Tình trạng chỗ đỗ theo tầng"));
            rows.Add(new[] { "Tầng", "Tổng", "Trống", "Đang dùng", "Đặt trước", "Đã gán", "Tỷ lệ sử dụng" });
            rows.AddRange(report.SlotOccupancyByFloor.Select(x => new[]
            {
                x.FloorName,
                x.TotalSlots.ToString(),
                x.AvailableSlots.ToString(),
                x.OccupiedSlots.ToString(),
                x.ReservedSlots.ToString(),
                x.AssignedSlots.ToString(),
                $"{FormatValue(x.UtilizationRate)}%"
            }));

            return rows;
        }

        private static List<string[]> BuildRevenueRows(RevenueReportDTO report)
        {
            var rows = BaseRows("Thống kê doanh thu", report.Range);
            rows.Add(new[] { "Tổng doanh thu", FormatValue(report.TotalRevenue) });
            rows.Add(new[] { "Số thanh toán thành công", report.SuccessfulPaymentCount.ToString() });
            rows.Add(new[] { "Giá trị thanh toán trung bình", FormatValue(report.AveragePaymentAmount) });

            rows.Add(Section("Doanh thu theo thời gian"));
            rows.Add(new[] { "Kỳ", "Số thanh toán", "Doanh thu" });
            rows.AddRange(report.RevenueSeries.Select(x => new[] { x.Period, x.Count.ToString(), FormatValue(x.Amount) }));

            rows.Add(Section("Theo loại thanh toán"));
            rows.Add(new[] { "Loại", "Số thanh toán", "Doanh thu", "Tỷ trọng" });
            rows.AddRange(report.ByPaymentType.Select(x => new[] { x.Name, x.Count.ToString(), FormatValue(x.Amount), $"{FormatValue(x.Percent)}%" }));

            rows.Add(Section("Theo phương thức thanh toán"));
            rows.Add(new[] { "Phương thức", "Số thanh toán", "Doanh thu", "Tỷ trọng" });
            rows.AddRange(report.ByPaymentMethod.Select(x => new[] { x.Name, x.Count.ToString(), FormatValue(x.Amount), $"{FormatValue(x.Percent)}%" }));

            rows.Add(Section("Thanh toán gần nhất"));
            rows.Add(new[] { "PaymentId", "Thời gian", "Loại", "Phương thức", "Số tiền", "Trạng thái", "Mã giao dịch" });
            rows.AddRange(report.LatestPayments.Select(x => new[]
            {
                x.PaymentId.ToString(),
                FormatDateTime(x.PaymentTime),
                x.PaymentType,
                x.PaymentMethod,
                FormatValue(x.Amount),
                x.PaymentStatus,
                x.TransactionReference ?? string.Empty
            }));

            return rows;
        }

        private static List<string[]> BuildOperationRows(ParkingOperationReportDTO report)
        {
            var rows = BaseRows("Thống kê vận hành bãi xe", report.Range);
            rows.Add(Section("Phiên gửi xe"));
            rows.Add(new[] { "Lượt vào", report.Sessions.Entries.ToString() });
            rows.Add(new[] { "Lượt ra", report.Sessions.Exits.ToString() });
            rows.Add(new[] { "Phiên đang gửi", report.Sessions.ActiveSessions.ToString() });
            rows.Add(new[] { "Phiên hoàn tất", report.Sessions.CompletedSessions.ToString() });
            rows.Add(new[] { "Thời gian gửi trung bình", $"{FormatValue(report.Sessions.AverageParkingMinutes)} phút" });

            rows.Add(Section("Đặt chỗ"));
            rows.Add(new[] { "Tổng", report.Reservations.Total.ToString() });
            rows.Add(new[] { "Pending", report.Reservations.Pending.ToString() });
            rows.Add(new[] { "Confirmed", report.Reservations.Confirmed.ToString() });
            rows.Add(new[] { "CheckedIn", report.Reservations.CheckedIn.ToString() });
            rows.Add(new[] { "Completed", report.Reservations.Completed.ToString() });
            rows.Add(new[] { "Cancelled", report.Reservations.Cancelled.ToString() });
            rows.Add(new[] { "NoShow", report.Reservations.NoShow.ToString() });

            rows.Add(Section("Gói tháng"));
            rows.Add(new[] { "Tạo mới", report.Subscriptions.NewSubscriptions.ToString() });
            rows.Add(new[] { "Đang hoạt động", report.Subscriptions.ActiveSubscriptions.ToString() });
            rows.Add(new[] { "Hết hạn", report.Subscriptions.ExpiredSubscriptions.ToString() });
            rows.Add(new[] { "Sắp hết hạn 7 ngày", report.Subscriptions.ExpiringInNext7Days.ToString() });

            rows.Add(Section("Sự cố"));
            rows.Add(new[] { "Open", report.Incidents.OpenIncidents.ToString() });
            rows.Add(new[] { "InProgress", report.Incidents.InProgressIncidents.ToString() });
            rows.Add(new[] { "Resolved trong kỳ", report.Incidents.ResolvedInRange.ToString() });
            rows.Add(new[] { "Cancelled", report.Incidents.CancelledIncidents.ToString() });

            rows.Add(Section("Tình trạng chỗ đỗ theo tầng"));
            rows.Add(new[] { "Tầng", "Tổng", "Trống", "Đang dùng", "Đặt trước", "Đã gán", "Tỷ lệ sử dụng" });
            rows.AddRange(report.SlotOccupancyByFloor.Select(x => new[]
            {
                x.FloorName,
                x.TotalSlots.ToString(),
                x.AvailableSlots.ToString(),
                x.OccupiedSlots.ToString(),
                x.ReservedSlots.ToString(),
                x.AssignedSlots.ToString(),
                $"{FormatValue(x.UtilizationRate)}%"
            }));

            rows.Add(Section("Phiên gửi xe gần nhất"));
            rows.Add(new[] { "SessionId", "Biển số", "Loại xe", "Giờ vào", "Giờ ra", "Trạng thái" });
            rows.AddRange(report.LatestSessions.Select(x => new[]
            {
                x.SessionId.ToString(),
                x.LicensePlate,
                x.VehicleTypeName,
                FormatDateTime(x.EntryTime),
                x.ExitTime.HasValue ? FormatDateTime(x.ExitTime.Value) : string.Empty,
                x.Status
            }));

            return rows;
        }

        private static List<string[]> BaseRows(string title, ReportRangeDTO range)
        {
            return new List<string[]>
            {
                new[] { title },
                new[] { "Từ ngày", FormatDate(range.From) },
                new[] { "Đến ngày", FormatDate(range.To) },
                new[] { "Nhóm theo", range.GroupBy },
                new[] { "VehicleTypeId", range.VehicleTypeId?.ToString() ?? "Tất cả" },
                Array.Empty<string>()
            };
        }

        private static string[] Section(string title)
        {
            return new[] { string.Empty, title };
        }

        private static string FormatDate(DateTime value)
        {
            return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string FormatDateTime(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static string FormatValue(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static byte[] BuildCsv(List<string[]> rows)
        {
            var csv = new StringBuilder();
            csv.Append('\uFEFF');

            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(",", row.Select(EscapeCsv)));
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        private static string EscapeCsv(string? value)
        {
            value ??= string.Empty;
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static List<string> RowsToPdfLines(List<string[]> rows)
        {
            return rows
                .Select(row => row.Length == 0 ? string.Empty : string.Join(" | ", row.Where(value => !string.IsNullOrWhiteSpace(value))))
                .ToList();
        }

        private static byte[] BuildPdf(string title, List<string> lines)
        {
            var pdfLines = new List<string>
            {
                title,
                $"Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                string.Empty
            };
            pdfLines.AddRange(lines.Select(RemoveDiacritics));

            var pages = pdfLines
                .Select(line => line.Length > 100 ? line[..100] : line)
                .Chunk(44)
                .Select(chunk => chunk.ToList())
                .ToList();

            if (pages.Count == 0)
            {
                pages.Add(new List<string> { "No data" });
            }

            var fontObjectId = 3 + pages.Count * 2;
            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                $"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => $"{3 + i * 2} 0 R"))}] /Count {pages.Count} >>"
            };

            for (var i = 0; i < pages.Count; i++)
            {
                var pageObjectId = 3 + i * 2;
                var contentObjectId = pageObjectId + 1;
                var content = BuildPdfPageContent(pages[i]);
                var contentLength = Encoding.ASCII.GetByteCount(content);

                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {fontObjectId} 0 R >> >> /Contents {contentObjectId} 0 R >>");
                objects.Add($"<< /Length {contentLength} >>\nstream\n{content}\nendstream");
            }

            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

            var output = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int>();

            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(output.ToString()));
                output.Append(i + 1).Append(" 0 obj\n")
                    .Append(objects[i]).Append("\nendobj\n");
            }

            var xrefOffset = Encoding.ASCII.GetByteCount(output.ToString());
            output.Append("xref\n")
                .Append("0 ").Append(objects.Count + 1).Append('\n')
                .Append("0000000000 65535 f \n");

            foreach (var offset in offsets)
            {
                output.Append(offset.ToString("0000000000", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            }

            output.Append("trailer\n")
                .Append("<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\n")
                .Append("startxref\n")
                .Append(xrefOffset).Append('\n')
                .Append("%%EOF");

            return Encoding.ASCII.GetBytes(output.ToString());
        }

        private static string BuildPdfPageContent(List<string> lines)
        {
            var content = new StringBuilder();
            content.AppendLine("BT");
            content.AppendLine("/F1 11 Tf");
            content.AppendLine("50 800 Td");

            foreach (var line in lines)
            {
                content.Append('(').Append(EscapePdfText(line)).AppendLine(") Tj");
                content.AppendLine("0 -16 Td");
            }

            content.AppendLine("ET");
            return content.ToString();
        }

        private static string EscapePdfText(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)");
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var normalized = text
                .Replace('đ', 'd')
                .Replace('Đ', 'D')
                .Normalize(NormalizationForm.FormD);

            var builder = new StringBuilder();
            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category != UnicodeCategory.NonSpacingMark && character <= 127)
                {
                    builder.Append(character);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
