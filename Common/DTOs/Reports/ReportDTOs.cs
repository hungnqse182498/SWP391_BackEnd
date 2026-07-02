namespace Common.DTOs.Reports
{
    public class ReportFilterDTO
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? GroupBy { get; set; } = "day";
        public Guid? VehicleTypeId { get; set; }
    }

    public class ReportExportRequestDTO : ReportFilterDTO
    {
        public string ReportType { get; set; } = "summary";
        public string Format { get; set; } = "excel";
    }

    public class ReportExportFileDTO
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public class ReportRangeDTO
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string GroupBy { get; set; } = "day";
        public Guid? VehicleTypeId { get; set; }
    }

    public class ReportTypeDTO
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] SupportedFormats { get; set; } = Array.Empty<string>();
    }

    public class ReportMetricDTO
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class ReportBreakdownDTO
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public decimal Percent { get; set; }
    }

    public class ReportSeriesPointDTO
    {
        public string Period { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }

    public class ReportSummaryDTO
    {
        public ReportRangeDTO Range { get; set; } = new();
        public List<ReportMetricDTO> Metrics { get; set; } = new();
        public List<ReportSeriesPointDTO> RevenueSeries { get; set; } = new();
        public List<ReportBreakdownDTO> RevenueByPaymentType { get; set; } = new();
        public List<ReportBreakdownDTO> RevenueByPaymentMethod { get; set; } = new();
        public SlotOverviewDTO Slots { get; set; } = new();
        public List<FloorOccupancyDTO> SlotOccupancyByFloor { get; set; } = new();
    }

    public class RevenueReportDTO
    {
        public ReportRangeDTO Range { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public int SuccessfulPaymentCount { get; set; }
        public decimal AveragePaymentAmount { get; set; }
        public List<ReportSeriesPointDTO> RevenueSeries { get; set; } = new();
        public List<ReportBreakdownDTO> ByPaymentType { get; set; } = new();
        public List<ReportBreakdownDTO> ByPaymentMethod { get; set; } = new();
        public List<RevenuePaymentRowDTO> LatestPayments { get; set; } = new();
    }

    public class RevenuePaymentRowDTO
    {
        public Guid PaymentId { get; set; }
        public DateTime PaymentTime { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string? TransactionReference { get; set; }
    }

    public class ParkingOperationReportDTO
    {
        public ReportRangeDTO Range { get; set; } = new();
        public ParkingSessionOverviewDTO Sessions { get; set; } = new();
        public ReservationOverviewDTO Reservations { get; set; } = new();
        public SubscriptionOverviewDTO Subscriptions { get; set; } = new();
        public IncidentOverviewDTO Incidents { get; set; } = new();
        public SlotOverviewDTO Slots { get; set; } = new();
        public List<ReportBreakdownDTO> SessionsByVehicleType { get; set; } = new();
        public List<ReportBreakdownDTO> ReservationsByStatus { get; set; } = new();
        public List<ReportBreakdownDTO> SubscriptionsByStatus { get; set; } = new();
        public List<ReportBreakdownDTO> IncidentsByStatus { get; set; } = new();
        public List<FloorOccupancyDTO> SlotOccupancyByFloor { get; set; } = new();
        public List<ParkingSessionReportRowDTO> LatestSessions { get; set; } = new();
    }

    public class ParkingSessionOverviewDTO
    {
        public int Entries { get; set; }
        public int Exits { get; set; }
        public int ActiveSessions { get; set; }
        public int CompletedSessions { get; set; }
        public decimal AverageParkingMinutes { get; set; }
    }

    public class ReservationOverviewDTO
    {
        public int Total { get; set; }
        public int Pending { get; set; }
        public int Confirmed { get; set; }
        public int CheckedIn { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public int NoShow { get; set; }
    }

    public class SubscriptionOverviewDTO
    {
        public int NewSubscriptions { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int ExpiredSubscriptions { get; set; }
        public int ExpiringInNext7Days { get; set; }
    }

    public class IncidentOverviewDTO
    {
        public int OpenIncidents { get; set; }
        public int InProgressIncidents { get; set; }
        public int ResolvedInRange { get; set; }
        public int CancelledIncidents { get; set; }
    }

    public class SlotOverviewDTO
    {
        public int TotalSlots { get; set; }
        public int AvailableSlots { get; set; }
        public int OccupiedSlots { get; set; }
        public int ReservedSlots { get; set; }
        public int AssignedSlots { get; set; }
        public decimal UtilizationRate { get; set; }
    }

    public class FloorOccupancyDTO
    {
        public Guid FloorId { get; set; }
        public string FloorName { get; set; } = string.Empty;
        public int TotalSlots { get; set; }
        public int AvailableSlots { get; set; }
        public int OccupiedSlots { get; set; }
        public int ReservedSlots { get; set; }
        public int AssignedSlots { get; set; }
        public decimal UtilizationRate { get; set; }
    }

    public class ParkingSessionReportRowDTO
    {
        public Guid SessionId { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string VehicleTypeName { get; set; } = string.Empty;
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
