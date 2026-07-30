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
        private static readonly string[] SupportedFormats = { "pdf" };
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
            var previousRange = BuildPreviousPeriodRange(range);
            var samePeriodRange = BuildSamePeriodLastYearRange(range);
            var previousPayments = await GetSuccessfulPaymentsAsync(previousRange);
            var samePeriodPayments = await GetSuccessfulPaymentsAsync(samePeriodRange);
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
                RevenueSeries = BuildRevenueSeries(payments, range),
                RevenueByPaymentType = BuildPaymentBreakdown(payments, p => p.PaymentType),
                RevenueByPaymentMethod = BuildPaymentBreakdown(payments, p => p.PaymentMethod),
                RevenueComparison = BuildRevenueComparison(range, payments, previousRange, previousPayments, samePeriodRange, samePeriodPayments),
                RevenueCharts = BuildRevenueCharts(range, payments, previousRange, previousPayments, samePeriodRange, samePeriodPayments),
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
            var previousRange = BuildPreviousPeriodRange(range);
            var samePeriodRange = BuildSamePeriodLastYearRange(range);
            var previousPayments = await GetSuccessfulPaymentsAsync(previousRange);
            var samePeriodPayments = await GetSuccessfulPaymentsAsync(samePeriodRange);
            var totalRevenue = payments.Sum(p => p.Amount);
            var revenueSeries = BuildRevenueSeries(payments, range);

            var report = new RevenueReportDTO
            {
                Range = range,
                TotalRevenue = totalRevenue,
                SuccessfulPaymentCount = payments.Count,
                AveragePaymentAmount = payments.Count == 0 ? 0 : Math.Round(totalRevenue / payments.Count, 2),
                Overview = BuildRevenueOverview(totalRevenue, payments.Count, revenueSeries),
                Comparison = BuildRevenueComparison(range, payments, previousRange, previousPayments, samePeriodRange, samePeriodPayments),
                Charts = BuildRevenueCharts(range, payments, previousRange, previousPayments, samePeriodRange, samePeriodPayments),
                RevenueSeries = revenueSeries,
                ByPaymentType = BuildPaymentBreakdown(payments, p => p.PaymentType),
                ByPaymentMethod = BuildPaymentBreakdown(payments, p => p.PaymentMethod),
                ByVehicleType = BuildPaymentBreakdown(payments, GetPaymentVehicleTypeName),
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
                return new ResponseDTO("Định dạng xuất báo cáo chỉ được là pdf", 400, false);
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

            var file = new ReportExportFileDTO
            {
                Content = BuildPdf(fileBaseName, rows),
                ContentType = "application/pdf",
                FileName = $"{fileBaseName}.pdf"
            };

            return new ResponseDTO("Xuất báo cáo thành công", 200, true, file);
        }

        private async Task<List<Payment>> GetSuccessfulPaymentsAsync(ReportRangeDTO range)
        {
            var query = _unitOfWork.PaymentRepo.GetAll()
                .AsNoTracking()
                .Include(p => p.Session)
                    .ThenInclude(s => s.VehicleType)
                .Include(p => p.Reservation)
                    .ThenInclude(r => r.VehicleType)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.VehicleType)
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
                    var assigned = CountByStatus(floorSlots, s => s.Status, ParkingSlotStatus.Assigned.ToString());

                    return new FloorOccupancyDTO
                    {
                        FloorId = g.Key.FloorId,
                        FloorName = g.Key.FloorName,
                        TotalSlots = total,
                        AvailableSlots = available,
                        OccupiedSlots = occupied,
                        AssignedSlots = assigned,
                        UtilizationRate = Percent(occupied + assigned, total)
                    };
                })
                .ToList();
        }

        private static SlotOverviewDTO BuildSlotOverview(List<ParkingSlot> slots)
        {
            var total = slots.Count;
            var available = CountByStatus(slots, s => s.Status, ParkingSlotStatus.Available.ToString());
            var occupied = CountByStatus(slots, s => s.Status, ParkingSlotStatus.Occupied.ToString());
            var assigned = CountByStatus(slots, s => s.Status, ParkingSlotStatus.Assigned.ToString());

            return new SlotOverviewDTO
            {
                TotalSlots = total,
                AvailableSlots = available,
                OccupiedSlots = occupied,
                AssignedSlots = assigned,
                UtilizationRate = Percent(occupied + assigned, total)
            };
        }

        private static List<ReportSeriesPointDTO> BuildRevenueSeries(List<Payment> payments, ReportRangeDTO range)
        {
            var groupedPayments = payments
                .GroupBy(p => GetPeriodKey(p.PaymentTime, range.GroupBy))
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Count = g.Count(),
                        Amount = g.Sum(p => p.Amount)
                    });

            return BuildPeriodBuckets(range)
                .Select(bucket =>
                {
                    groupedPayments.TryGetValue(bucket.Label, out var data);

                    return new ReportSeriesPointDTO
                    {
                        Period = bucket.Label,
                        Count = data?.Count ?? 0,
                        Amount = data?.Amount ?? 0
                    };
                })
                .ToList();
        }

        private static RevenueOverviewDTO BuildRevenueOverview(
            decimal totalRevenue,
            int successfulPaymentCount,
            List<ReportSeriesPointDTO> revenueSeries)
        {
            var averagePaymentAmount = successfulPaymentCount == 0
                ? 0
                : Math.Round(totalRevenue / successfulPaymentCount, 2);

            var highest = revenueSeries
                .OrderByDescending(x => x.Amount)
                .FirstOrDefault();
            var lowest = revenueSeries
                .OrderBy(x => x.Amount)
                .FirstOrDefault();

            return new RevenueOverviewDTO
            {
                TotalRevenue = totalRevenue,
                SuccessfulPaymentCount = successfulPaymentCount,
                AveragePaymentAmount = averagePaymentAmount,
                HighestRevenueAmount = highest?.Amount ?? 0,
                HighestRevenuePeriod = highest?.Period ?? string.Empty,
                LowestRevenueAmount = lowest?.Amount ?? 0,
                LowestRevenuePeriod = lowest?.Period ?? string.Empty
            };
        }

        private static RevenueComparisonDTO BuildRevenueComparison(
            ReportRangeDTO currentRange,
            List<Payment> currentPayments,
            ReportRangeDTO previousRange,
            List<Payment> previousPayments,
            ReportRangeDTO samePeriodRange,
            List<Payment> samePeriodPayments)
        {
            return new RevenueComparisonDTO
            {
                PreviousPeriod = BuildRevenueComparisonItem(
                    "Kỳ trước",
                    currentRange,
                    currentPayments,
                    previousRange,
                    previousPayments),
                SamePeriodLastYear = BuildRevenueComparisonItem(
                    "Cùng kỳ năm trước",
                    currentRange,
                    currentPayments,
                    samePeriodRange,
                    samePeriodPayments)
            };
        }

        private static RevenueComparisonItemDTO BuildRevenueComparisonItem(
            string label,
            ReportRangeDTO currentRange,
            List<Payment> currentPayments,
            ReportRangeDTO comparisonRange,
            List<Payment> comparisonPayments)
        {
            var currentRevenue = currentPayments.Sum(p => p.Amount);
            var comparisonRevenue = comparisonPayments.Sum(p => p.Amount);
            var currentCount = currentPayments.Count;
            var comparisonCount = comparisonPayments.Count;

            return new RevenueComparisonItemDTO
            {
                Label = label,
                CurrentFrom = currentRange.From,
                CurrentTo = currentRange.To,
                ComparisonFrom = comparisonRange.From,
                ComparisonTo = comparisonRange.To,
                CurrentRevenue = currentRevenue,
                ComparisonRevenue = comparisonRevenue,
                DifferenceAmount = currentRevenue - comparisonRevenue,
                GrowthPercent = GrowthPercent(currentRevenue, comparisonRevenue),
                CurrentPaymentCount = currentCount,
                ComparisonPaymentCount = comparisonCount,
                PaymentCountDifference = currentCount - comparisonCount,
                PaymentCountGrowthPercent = GrowthPercent(currentCount, comparisonCount)
            };
        }

        private static RevenueChartsDTO BuildRevenueCharts(
            ReportRangeDTO currentRange,
            List<Payment> currentPayments,
            ReportRangeDTO previousRange,
            List<Payment> previousPayments,
            ReportRangeDTO samePeriodRange,
            List<Payment> samePeriodPayments)
        {
            var byPaymentType = BuildPaymentBreakdown(currentPayments, p => p.PaymentType);
            var byPaymentMethod = BuildPaymentBreakdown(currentPayments, p => p.PaymentMethod);
            var byVehicleType = BuildPaymentBreakdown(currentPayments, GetPaymentVehicleTypeName);

            return new RevenueChartsDTO
            {
                LineChart = BuildRevenueLineChart(currentRange, currentPayments),
                PieCharts =
                {
                    BuildPieChart("Tỷ trọng doanh thu theo loại thanh toán", "paymentType", byPaymentType),
                    BuildPieChart("Tỷ trọng doanh thu theo phương thức thanh toán", "paymentMethod", byPaymentMethod),
                    BuildPieChart("Tỷ trọng doanh thu theo loại xe", "vehicleType", byVehicleType)
                },
                DoubleBarChart = BuildDoubleBarChart(
                    "Doanh thu kỳ này so với cùng kỳ năm trước",
                    "Kỳ này",
                    "Cùng kỳ năm trước",
                    currentRange,
                    currentPayments,
                    samePeriodRange,
                    samePeriodPayments),
                PreviousPeriodDoubleBarChart = BuildDoubleBarChart(
                    "Doanh thu kỳ này so với kỳ trước",
                    "Kỳ này",
                    "Kỳ trước",
                    currentRange,
                    currentPayments,
                    previousRange,
                    previousPayments)
            };
        }

        private static LineChartDTO BuildRevenueLineChart(ReportRangeDTO range, List<Payment> payments)
        {
            var groupedPayments = payments
                .GroupBy(p => GetPeriodKey(p.PaymentTime, range.GroupBy))
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Count = g.Count(),
                        Amount = g.Sum(p => p.Amount)
                    });

            return new LineChartDTO
            {
                Title = "Biểu đồ đường doanh thu",
                GroupBy = range.GroupBy,
                Unit = "VND",
                Points = BuildPeriodBuckets(range)
                    .Select(bucket =>
                    {
                        groupedPayments.TryGetValue(bucket.Label, out var data);

                        return new ChartPointDTO
                        {
                            Label = bucket.Label,
                            From = bucket.From,
                            To = bucket.To,
                            Value = data?.Amount ?? 0,
                            Count = data?.Count ?? 0
                        };
                    })
                    .ToList()
            };
        }

        private static PieChartDTO BuildPieChart(
            string title,
            string dimension,
            List<ReportBreakdownDTO> breakdown)
        {
            return new PieChartDTO
            {
                Title = title,
                Dimension = dimension,
                Unit = "VND",
                Slices = breakdown
                    .Select(item => new PieChartSliceDTO
                    {
                        Label = item.Name,
                        Value = item.Amount,
                        Count = item.Count,
                        Percent = item.Percent
                    })
                    .ToList()
            };
        }

        private static DoubleBarChartDTO BuildDoubleBarChart(
            string title,
            string currentSeriesName,
            string comparisonSeriesName,
            ReportRangeDTO currentRange,
            List<Payment> currentPayments,
            ReportRangeDTO comparisonRange,
            List<Payment> comparisonPayments)
        {
            var currentGrouped = currentPayments
                .GroupBy(p => GetPeriodKey(p.PaymentTime, currentRange.GroupBy))
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Count = g.Count(),
                        Amount = g.Sum(p => p.Amount)
                    });

            var comparisonGrouped = comparisonPayments
                .GroupBy(p => GetPeriodKey(p.PaymentTime, comparisonRange.GroupBy))
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Count = g.Count(),
                        Amount = g.Sum(p => p.Amount)
                    });

            var currentBuckets = BuildPeriodBuckets(currentRange);
            var comparisonBuckets = BuildPeriodBuckets(comparisonRange);

            return new DoubleBarChartDTO
            {
                Title = title,
                Unit = "VND",
                GroupBy = currentRange.GroupBy,
                CurrentSeriesName = currentSeriesName,
                ComparisonSeriesName = comparisonSeriesName,
                Points = currentBuckets
                    .Select((currentBucket, index) =>
                    {
                        currentGrouped.TryGetValue(currentBucket.Label, out var currentData);
                        if (index < comparisonBuckets.Count)
                        {
                            var comparisonBucket = comparisonBuckets[index];
                            comparisonGrouped.TryGetValue(comparisonBucket.Label, out var comparisonData);
                            return new DoubleBarChartPointDTO
                            {
                                Label = currentBucket.Label,
                                CurrentPeriod = currentBucket.Label,
                                ComparisonPeriod = comparisonBucket.Label,
                                CurrentValue = currentData?.Amount ?? 0,
                                ComparisonValue = comparisonData?.Amount ?? 0,
                                CurrentCount = currentData?.Count ?? 0,
                                ComparisonCount = comparisonData?.Count ?? 0
                            };
                        }

                        return new DoubleBarChartPointDTO
                        {
                            Label = currentBucket.Label,
                            CurrentPeriod = currentBucket.Label,
                            ComparisonPeriod = string.Empty,
                            CurrentValue = currentData?.Amount ?? 0,
                            ComparisonValue = 0,
                            CurrentCount = currentData?.Count ?? 0,
                            ComparisonCount = 0
                        };
                    })
                    .ToList()
            };
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

        private static decimal GrowthPercent(decimal current, decimal comparison)
        {
            if (comparison == 0)
            {
                return current == 0 ? 0 : 100;
            }

            return Math.Round((current - comparison) / comparison * 100, 2);
        }

        private static string GetPeriodKey(DateTime value, string groupBy)
        {
            return groupBy switch
            {
                "month" => value.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                "quarter" => $"{value.Year}-Q{GetQuarter(value)}",
                "year" => value.ToString("yyyy", CultureInfo.InvariantCulture),
                _ => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
        }

        private static int GetQuarter(DateTime value)
        {
            return ((value.Month - 1) / 3) + 1;
        }

        private static DateTime GetPeriodStart(DateTime value, string groupBy)
        {
            var date = value.Date;
            return groupBy switch
            {
                "month" => new DateTime(date.Year, date.Month, 1),
                "quarter" => new DateTime(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1),
                "year" => new DateTime(date.Year, 1, 1),
                _ => date
            };
        }

        private static DateTime MoveToNextPeriod(DateTime periodStart, string groupBy)
        {
            return groupBy switch
            {
                "month" => periodStart.AddMonths(1),
                "quarter" => periodStart.AddMonths(3),
                "year" => periodStart.AddYears(1),
                _ => periodStart.AddDays(1)
            };
        }

        private static List<(string Label, DateTime From, DateTime To)> BuildPeriodBuckets(ReportRangeDTO range)
        {
            var buckets = new List<(string Label, DateTime From, DateTime To)>();
            var cursor = GetPeriodStart(range.From, range.GroupBy);

            while (cursor <= range.To)
            {
                var nextPeriod = MoveToNextPeriod(cursor, range.GroupBy);
                var bucketFrom = cursor < range.From ? range.From : cursor;
                var bucketTo = nextPeriod.AddTicks(-1) > range.To ? range.To : nextPeriod.AddTicks(-1);

                buckets.Add((GetPeriodKey(cursor, range.GroupBy), bucketFrom, bucketTo));
                cursor = nextPeriod;
            }

            return buckets;
        }

        private static ReportRangeDTO BuildPreviousPeriodRange(ReportRangeDTO range)
        {
            var durationTicks = range.To.Ticks - range.From.Ticks + 1;

            return new ReportRangeDTO
            {
                From = range.From.AddTicks(-durationTicks),
                To = range.From.AddTicks(-1),
                GroupBy = range.GroupBy,
                VehicleTypeId = range.VehicleTypeId
            };
        }

        private static ReportRangeDTO BuildSamePeriodLastYearRange(ReportRangeDTO range)
        {
            return new ReportRangeDTO
            {
                From = range.From.AddYears(-1),
                To = range.To.AddYears(-1),
                GroupBy = range.GroupBy,
                VehicleTypeId = range.VehicleTypeId
            };
        }

        private static string GetPaymentVehicleTypeName(Payment payment)
        {
            return payment.Session?.VehicleType?.TypeName
                ?? payment.Reservation?.VehicleType?.TypeName
                ?? payment.Subscription?.VehicleType?.TypeName
                ?? "Unknown";
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
            var groupBy = !string.IsNullOrWhiteSpace(filter.Period)
                ? NormalizeToken(filter.Period)
                : NormalizeToken(filter.GroupBy);
            groupBy = string.IsNullOrWhiteSpace(groupBy) ? "day" : groupBy;

            if (from > to)
            {
                return (new ReportRangeDTO(), new ResponseDTO("Ngày bắt đầu không được lớn hơn ngày kết thúc", 400, false));
            }

            if (groupBy is not ("day" or "month" or "quarter" or "year"))
            {
                return (new ReportRangeDTO(), new ResponseDTO("GroupBy must be one of: day, month, quarter, year", 400, false));
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
            rows.Add(new[] { "Tầng", "Tổng", "Trống", "Đang dùng", "Đã gán", "Tỷ lệ sử dụng" });
            rows.AddRange(report.SlotOccupancyByFloor.Select(x => new[]
            {
                x.FloorName,
                x.TotalSlots.ToString(),
                x.AvailableSlots.ToString(),
                x.OccupiedSlots.ToString(),
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
            rows.Add(new[] { "Tầng", "Tổng", "Trống", "Đang dùng", "Đã gán", "Tỷ lệ sử dụng" });
            rows.AddRange(report.SlotOccupancyByFloor.Select(x => new[]
            {
                x.FloorName,
                x.TotalSlots.ToString(),
                x.AvailableSlots.ToString(),
                x.OccupiedSlots.ToString(),
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

        private static byte[] BuildPdf(string fileBaseName, List<string[]> rows)
        {
            const double pageWidth = 595;
            const double pageHeight = 842;
            const double margin = 36;
            const double footerHeight = 44;
            const double contentWidth = pageWidth - margin * 2;

            var reportTitle = rows.FirstOrDefault(row => row.Length > 0)?.FirstOrDefault() ?? fileBaseName;
            var generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var pages = new List<string>();
            var content = new StringBuilder();
            var pageNumber = 0;
            var y = 0d;
            var currentSection = string.Empty;

            void StartPage()
            {
                pageNumber++;
                content = new StringBuilder();
                DrawPageChrome(content, reportTitle, generatedAt, pageNumber, pageWidth, pageHeight, margin);
                y = pageHeight - 138;
            }

            void FinishPage()
            {
                pages.Add(content.ToString());
            }

            void EnsureSpace(double requiredHeight, bool repeatSectionOnNewPage = true)
            {
                if (y - requiredHeight >= footerHeight)
                {
                    return;
                }

                FinishPage();
                StartPage();

                if (repeatSectionOnNewPage && !string.IsNullOrWhiteSpace(currentSection))
                {
                    DrawSectionHeader(currentSection + " (continued)");
                }
            }

            void DrawSectionHeader(string title)
            {
                currentSection = title.Replace(" (continued)", string.Empty);
                EnsureSpace(34, false);
                y -= 6;
                DrawRect(content, margin, y - 24, 4, 22, "0.15 0.39 0.92");
                DrawText(content, title, margin + 12, y - 18, 12, "F2", "0.08 0.13 0.24");
                DrawLine(content, margin, y - 29, pageWidth - margin, y - 29, "0.86 0.90 0.96");
                y -= 42;
            }

            void DrawMetadata(List<string[]> metadataRows)
            {
                if (metadataRows.Count == 0)
                {
                    return;
                }

                var rowsPerColumn = (int)Math.Ceiling(metadataRows.Count / 2d);
                var cardHeight = 28 + rowsPerColumn * 22;
                EnsureSpace(cardHeight + 12);
                DrawRect(content, margin, y - cardHeight, contentWidth, cardHeight, "0.94 0.97 1.00");
                DrawStrokeRect(content, margin, y - cardHeight, contentWidth, cardHeight, "0.78 0.86 0.98");
                DrawText(content, "Report information", margin + 14, y - 19, 11, "F2", "0.15 0.39 0.92");

                for (var index = 0; index < metadataRows.Count; index++)
                {
                    var row = metadataRows[index];
                    var column = index / rowsPerColumn;
                    var rowIndex = index % rowsPerColumn;
                    var x = margin + 14 + column * (contentWidth / 2);
                    var textY = y - 43 - rowIndex * 22;
                    var label = row.ElementAtOrDefault(0) ?? string.Empty;
                    var value = row.ElementAtOrDefault(1) ?? string.Empty;

                    DrawText(content, FitText(label, 82, 8), x, textY, 8, "F2", "0.38 0.46 0.58");
                    DrawText(content, FitText(value, contentWidth / 2 - 104, 9), x + 90, textY, 9, "F1", "0.08 0.13 0.24");
                }

                y -= cardHeight + 18;
            }

            void DrawKeyValueBlock(List<string[]> block)
            {
                const double rowHeight = 24;
                foreach (var (row, index) in block.Select((row, index) => (row, index)))
                {
                    EnsureSpace(rowHeight + 2);
                    var bg = index % 2 == 0 ? "1 1 1" : "0.98 0.99 1";
                    DrawRect(content, margin, y - rowHeight, contentWidth, rowHeight, bg);
                    DrawStrokeRect(content, margin, y - rowHeight, contentWidth, rowHeight, "0.88 0.91 0.95");
                    DrawText(content, FitText(row.ElementAtOrDefault(0) ?? string.Empty, contentWidth * 0.42, 9), margin + 8, y - 16, 9, "F2", "0.30 0.36 0.47");
                    DrawText(content, FitText(row.ElementAtOrDefault(1) ?? string.Empty, contentWidth * 0.52, 9), margin + contentWidth * 0.44, y - 16, 9, "F1", "0.08 0.13 0.24");
                    y -= rowHeight;
                }

                y -= 12;
            }

            void DrawTable(List<string[]> block)
            {
                if (block.Count == 0)
                {
                    return;
                }

                var columnCount = Math.Max(1, block.Max(row => row.Length));
                var widths = GetColumnWidths(columnCount, contentWidth);
                var header = block[0];
                var dataRows = block.Skip(1).ToList();
                var fontSize = columnCount >= 6 ? 7 : 8;
                const double headerHeight = 25;
                const double rowHeight = 23;

                void DrawHeader()
                {
                    EnsureSpace(headerHeight + rowHeight);
                    DrawRect(content, margin, y - headerHeight, contentWidth, headerHeight, "0.15 0.39 0.92");

                    var x = margin;
                    for (var col = 0; col < columnCount; col++)
                    {
                        DrawText(content, FitText(header.ElementAtOrDefault(col) ?? string.Empty, widths[col] - 10, fontSize), x + 5, y - 16, fontSize, "F2", "1 1 1");
                        x += widths[col];
                    }

                    y -= headerHeight;
                }

                DrawHeader();

                if (dataRows.Count == 0)
                {
                    EnsureSpace(rowHeight);
                    DrawRect(content, margin, y - rowHeight, contentWidth, rowHeight, "1 1 1");
                    DrawStrokeRect(content, margin, y - rowHeight, contentWidth, rowHeight, "0.88 0.91 0.95");
                    DrawText(content, "No data", margin + 8, y - 15, 9, "F1", "0.38 0.46 0.58");
                    y -= rowHeight + 12;
                    return;
                }

                for (var rowIndex = 0; rowIndex < dataRows.Count; rowIndex++)
                {
                    if (y - rowHeight < footerHeight)
                    {
                        FinishPage();
                        StartPage();
                        if (!string.IsNullOrWhiteSpace(currentSection))
                        {
                            DrawSectionHeader(currentSection + " (continued)");
                        }
                        DrawHeader();
                    }

                    var row = dataRows[rowIndex];
                    var bg = rowIndex % 2 == 0 ? "1 1 1" : "0.98 0.99 1";
                    DrawRect(content, margin, y - rowHeight, contentWidth, rowHeight, bg);
                    DrawStrokeRect(content, margin, y - rowHeight, contentWidth, rowHeight, "0.88 0.91 0.95");

                    var x = margin;
                    for (var col = 0; col < columnCount; col++)
                    {
                        var value = row.ElementAtOrDefault(col) ?? string.Empty;
                        DrawText(content, FitText(value, widths[col] - 10, fontSize), x + 5, y - 15, fontSize, "F1", "0.08 0.13 0.24");
                        x += widths[col];
                    }

                    y -= rowHeight;
                }

                y -= 14;
            }

            StartPage();

            var metadata = rows
                .Skip(1)
                .TakeWhile(row => row.Length > 0)
                .ToList();
            DrawMetadata(metadata);

            var i = 1 + metadata.Count;
            while (i < rows.Count && rows[i].Length == 0)
            {
                i++;
            }

            while (i < rows.Count)
            {
                var row = rows[i];
                if (row.Length == 0)
                {
                    y -= 8;
                    i++;
                    continue;
                }

                if (IsSectionRow(row))
                {
                    DrawSectionHeader(row.ElementAtOrDefault(1) ?? string.Empty);
                    i++;
                    continue;
                }

                var block = new List<string[]>();
                while (i < rows.Count && rows[i].Length > 0 && !IsSectionRow(rows[i]))
                {
                    block.Add(rows[i]);
                    i++;
                }

                if (string.IsNullOrWhiteSpace(currentSection))
                {
                    DrawSectionHeader("Overview");
                }

                var maxColumns = block.Count == 0 ? 0 : block.Max(item => item.Length);
                if (maxColumns <= 2)
                {
                    DrawKeyValueBlock(block);
                }
                else
                {
                    DrawTable(block);
                }
            }

            if (pages.Count == 0 || content.Length > 0)
            {
                FinishPage();
            }

            return BuildPdfDocument(pages);
        }

        private static bool IsSectionRow(string[] row)
        {
            return row.Length >= 2 && string.IsNullOrWhiteSpace(row[0]) && !string.IsNullOrWhiteSpace(row[1]);
        }

        private static void DrawPageChrome(StringBuilder content, string title, string generatedAt, int pageNumber, double pageWidth, double pageHeight, double margin)
        {
            DrawRect(content, 0, 0, pageWidth, pageHeight, "0.96 0.98 1.00");
            DrawRect(content, margin, pageHeight - 108, pageWidth - margin * 2, 72, "0.15 0.39 0.92");
            DrawText(content, "PARKING BUILDING MANAGEMENT", margin + 18, pageHeight - 62, 8, "F2", "0.78 0.86 1.00");
            DrawText(content, FitText(title, pageWidth - margin * 2 - 36, 17), margin + 18, pageHeight - 84, 17, "F2", "1 1 1");
            DrawText(content, $"Generated at {generatedAt}", margin + 18, pageHeight - 100, 8, "F1", "0.88 0.93 1.00");
            DrawText(content, $"Page {pageNumber}", pageWidth - margin - 52, 28, 8, "F1", "0.45 0.52 0.64");
            DrawLine(content, margin, 48, pageWidth - margin, 48, "0.86 0.90 0.96");
        }

        private static void DrawRect(StringBuilder content, double x, double y, double width, double height, string color)
        {
            content.AppendLine($"q {color} rg {PdfNumber(x)} {PdfNumber(y)} {PdfNumber(width)} {PdfNumber(height)} re f Q");
        }

        private static void DrawStrokeRect(StringBuilder content, double x, double y, double width, double height, string color)
        {
            content.AppendLine($"q {color} RG 0.6 w {PdfNumber(x)} {PdfNumber(y)} {PdfNumber(width)} {PdfNumber(height)} re S Q");
        }

        private static void DrawLine(StringBuilder content, double x1, double y1, double x2, double y2, string color)
        {
            content.AppendLine($"q {color} RG 0.7 w {PdfNumber(x1)} {PdfNumber(y1)} m {PdfNumber(x2)} {PdfNumber(y2)} l S Q");
        }

        private static void DrawText(StringBuilder content, string text, double x, double y, int fontSize, string fontName, string color)
        {
            content.AppendLine($"BT /{fontName} {fontSize} Tf {color} rg {PdfNumber(x)} {PdfNumber(y)} Td ({EscapePdfText(RemoveDiacritics(text))}) Tj ET");
        }

        private static string FitText(string value, double maxWidth, double fontSize)
        {
            value = RemoveDiacritics(value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var maxCharacters = Math.Max(3, (int)Math.Floor(maxWidth / (fontSize * 0.52)));
            return value.Length <= maxCharacters
                ? value
                : value[..Math.Max(1, maxCharacters - 3)] + "...";
        }

        private static double[] GetColumnWidths(int columnCount, double totalWidth)
        {
            double[] weights = columnCount switch
            {
                2 => new[] { 0.42, 0.58 },
                3 => new[] { 0.42, 0.18, 0.40 },
                4 => new[] { 0.34, 0.17, 0.25, 0.24 },
                5 => new[] { 0.26, 0.15, 0.20, 0.20, 0.19 },
                6 => new[] { 0.20, 0.13, 0.18, 0.16, 0.16, 0.17 },
                7 => new[] { 0.18, 0.13, 0.14, 0.13, 0.14, 0.13, 0.15 },
                _ => Enumerable.Repeat(1d / columnCount, columnCount).ToArray()
            };

            var sum = weights.Sum();
            return weights.Select(weight => totalWidth * weight / sum).ToArray();
        }

        private static string PdfNumber(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static byte[] BuildPdfDocument(List<string> pages)
        {
            if (pages.Count == 0)
            {
                pages.Add(string.Empty);
            }

            var fontRegularId = 3 + pages.Count * 2;
            var fontBoldId = fontRegularId + 1;
            var fontMonoId = fontRegularId + 2;
            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                $"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => $"{3 + i * 2} 0 R"))}] /Count {pages.Count} >>"
            };

            for (var i = 0; i < pages.Count; i++)
            {
                var pageObjectId = 3 + i * 2;
                var contentObjectId = pageObjectId + 1;
                var pageContent = pages[i];
                var contentLength = Encoding.ASCII.GetByteCount(pageContent);
                var resources = $"<< /Font << /F1 {fontRegularId} 0 R /F2 {fontBoldId} 0 R /F3 {fontMonoId} 0 R >> >>";

                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources {resources} /Contents {contentObjectId} 0 R >>");
                objects.Add($"<< /Length {contentLength} >>\nstream\n{pageContent}\nendstream");
            }

            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");

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
