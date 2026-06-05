using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.IncidentReport;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements
{
    public class IncidentReportService : IIncidentReportService
    {
        private readonly IUnitOfWork _unitOfWork;
        private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Open",
            "InProgress",
            "Resolved",
            "Cancelled"
        };

        public IncidentReportService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var incidents = await _unitOfWork.IncidentReportRepo.GetAll()
                .Include(i => i.ReportedByUser)
                .Include(i => i.HandledByStaff)
                .OrderBy(i => i.Status)
                .ToListAsync();

            return new ResponseDTO("Lấy danh sách sự cố thành công", 200, true, incidents.Select(MapToDTO).ToList());
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập IncidentId", 400, false);

            var incident = await _unitOfWork.IncidentReportRepo.GetAll()
                .Include(i => i.ReportedByUser)
                .Include(i => i.HandledByStaff)
                .FirstOrDefaultAsync(i => i.IncidentId == id);

            if (incident == null) return new ResponseDTO("Không tìm thấy sự cố", 404, false);
            return new ResponseDTO("Lấy thông tin sự cố thành công", 200, true, MapToDTO(incident));
        }

        public async Task<ResponseDTO> CreateAsync(CreateIncidentReportDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu tạo sự cố không hợp lệ", 400, false);

            var validation = await ValidateIncidentAsync(dto.SessionId, dto.ReportedByUserId, dto.IssueType, dto.Description, dto.Status ?? "Open", dto.HandledByStaffId);
            if (validation.Error != null) return validation.Error;

            var incident = new IncidentReport
            {
                IncidentId = Guid.NewGuid(),
                SessionId = dto.SessionId,
                ReportedByUserId = dto.ReportedByUserId,
                IssueType = dto.IssueType.Trim(),
                Description = dto.Description.Trim(),
                ProofImageUrl = string.IsNullOrWhiteSpace(dto.ProofImageUrl) ? null : dto.ProofImageUrl.Trim(),
                Status = validation.Status!,
                HandledByStaffId = dto.HandledByStaffId,
                ResolvedAt = string.Equals(validation.Status, "Resolved", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null
            };

            await _unitOfWork.IncidentReportRepo.AddAsync(incident);
            await _unitOfWork.SaveChangeAsync();
            incident.ReportedByUser = validation.ReportedByUser;
            incident.HandledByStaff = validation.HandledByStaff;

            return new ResponseDTO("Tạo sự cố thành công", 201, true, MapToDTO(incident));
        }

        public async Task<ResponseDTO> UpdateAsync(UpdateIncidentReportDTO dto)
        {
            if (dto == null || dto.IncidentId == Guid.Empty) return new ResponseDTO("Dữ liệu cập nhật sự cố không hợp lệ", 400, false);

            var incident = await _unitOfWork.IncidentReportRepo.GetByIdAsync(dto.IncidentId);
            if (incident == null) return new ResponseDTO("Không tìm thấy sự cố", 404, false);

            var validation = await ValidateIncidentAsync(dto.SessionId, dto.ReportedByUserId, dto.IssueType, dto.Description, dto.Status, dto.HandledByStaffId);
            if (validation.Error != null) return validation.Error;

            incident.SessionId = dto.SessionId;
            incident.ReportedByUserId = dto.ReportedByUserId;
            incident.IssueType = dto.IssueType.Trim();
            incident.Description = dto.Description.Trim();
            incident.ProofImageUrl = string.IsNullOrWhiteSpace(dto.ProofImageUrl) ? null : dto.ProofImageUrl.Trim();
            incident.Status = validation.Status!;
            incident.HandledByStaffId = dto.HandledByStaffId;
            incident.ResolvedAt = dto.ResolvedAt ?? (string.Equals(validation.Status, "Resolved", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null);
            incident.ResolutionNotes = string.IsNullOrWhiteSpace(dto.ResolutionNotes) ? null : dto.ResolutionNotes.Trim();

            await _unitOfWork.IncidentReportRepo.UpdateAsync(incident);
            await _unitOfWork.SaveChangeAsync();
            incident.ReportedByUser = validation.ReportedByUser;
            incident.HandledByStaff = validation.HandledByStaff;

            return new ResponseDTO("Cập nhật sự cố thành công", 200, true, MapToDTO(incident));
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập IncidentId", 400, false);

            var incident = await _unitOfWork.IncidentReportRepo.GetByIdAsync(id);
            if (incident == null) return new ResponseDTO("Không tìm thấy sự cố", 404, false);

            try
            {
                _unitOfWork.IncidentReportRepo.Delete(incident);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Xóa sự cố thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi xóa sự cố: {ex.Message}", 500, false);
            }
        }

        private async Task<(User? ReportedByUser, User? HandledByStaff, string? Status, ResponseDTO? Error)> ValidateIncidentAsync(
            Guid? sessionId,
            Guid reportedByUserId,
            string? issueType,
            string? description,
            string? status,
            Guid? handledByStaffId)
        {
            if (reportedByUserId == Guid.Empty) return (null, null, null, new ResponseDTO("Vui lòng chọn người báo cáo", 400, false));
            if (string.IsNullOrWhiteSpace(issueType)) return (null, null, null, new ResponseDTO("Vui lòng nhập loại sự cố", 400, false));
            if (string.IsNullOrWhiteSpace(description)) return (null, null, null, new ResponseDTO("Vui lòng nhập mô tả sự cố", 400, false));

            var normalizedStatus = NormalizeStatus(status);
            if (normalizedStatus == null) return (null, null, null, new ResponseDTO("Trạng thái sự cố không hợp lệ", 400, false));

            if (sessionId.HasValue)
            {
                var sessionExists = await _unitOfWork.ParkingSessionRepo.AnyAsync(s => s.SessionId == sessionId.Value);
                if (!sessionExists) return (null, null, null, new ResponseDTO("Phiên gửi xe không tồn tại", 400, false));
            }

            var reportedByUser = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(reportedByUserId);
            if (reportedByUser == null) return (null, null, null, new ResponseDTO("Người báo cáo không tồn tại", 400, false));

            User? handledByStaff = null;
            if (handledByStaffId.HasValue)
            {
                handledByStaff = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(handledByStaffId.Value);
                if (handledByStaff == null) return (null, null, null, new ResponseDTO("Nhân viên xử lý không tồn tại", 400, false));
            }

            return (reportedByUser, handledByStaff, normalizedStatus, null);
        }

        private static string? NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            return ValidStatuses.FirstOrDefault(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static IncidentReportDTO MapToDTO(IncidentReport incident)
        {
            return new IncidentReportDTO
            {
                IncidentId = incident.IncidentId,
                SessionId = incident.SessionId,
                ReportedByUserId = incident.ReportedByUserId,
                ReportedByUserFullName = incident.ReportedByUser?.FullName,
                IssueType = incident.IssueType,
                Description = incident.Description,
                ProofImageUrl = incident.ProofImageUrl,
                Status = incident.Status,
                HandledByStaffId = incident.HandledByStaffId,
                HandledByStaffFullName = incident.HandledByStaff?.FullName,
                ResolvedAt = incident.ResolvedAt,
                ResolutionNotes = incident.ResolutionNotes
            };
        }
    }
}
