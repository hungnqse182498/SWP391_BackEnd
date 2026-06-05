using System;

namespace Common.DTOs.IncidentReport
{
    public class IncidentReportDTO
    {
        public Guid IncidentId { get; set; }
        public Guid? SessionId { get; set; }
        public Guid ReportedByUserId { get; set; }
        public string? ReportedByUserFullName { get; set; }
        public string IssueType { get; set; }
        public string Description { get; set; }
        public string? ProofImageUrl { get; set; }
        public string Status { get; set; }
        public Guid? HandledByStaffId { get; set; }
        public string? HandledByStaffFullName { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolutionNotes { get; set; }
    }

    public class CreateIncidentReportDTO
    {
        public Guid? SessionId { get; set; }
        public Guid ReportedByUserId { get; set; }
        public string IssueType { get; set; }
        public string Description { get; set; }
        public string? ProofImageUrl { get; set; }
        public string? Status { get; set; }
        public Guid? HandledByStaffId { get; set; }
    }

    public class UpdateIncidentReportDTO
    {
        public Guid IncidentId { get; set; }
        public Guid? SessionId { get; set; }
        public Guid ReportedByUserId { get; set; }
        public string IssueType { get; set; }
        public string Description { get; set; }
        public string? ProofImageUrl { get; set; }
        public string Status { get; set; }
        public Guid? HandledByStaffId { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolutionNotes { get; set; }
    }
}
