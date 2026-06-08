using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.IncidentReport;
using Common.Enums; 
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Implements
{
    public class IncidentReportService : IIncidentReportService
    {
        private readonly IUnitOfWork _unitOfWork;

        public IncidentReportService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var incidents = await _unitOfWork.IncidentReportRepo.GetAllWithDetailsAsync();
            var incidentList = incidents.ToList();

            if (incidentList.Count == 0)
            {
                return new ResponseDTO("Không có sự cố nào trong hệ thống", 404, false);
            }

            var dtos = incidentList.Select(MapToDTO).ToList();
            return new ResponseDTO("Lấy danh sách sự cố thành công", 200, true, dtos);
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
                return new ResponseDTO("Vui lòng nhập IncidentId", 400, false);

            var incident = await _unitOfWork.IncidentReportRepo.GetByIdWithDetailsAsync(id);

            if (incident == null)
                return new ResponseDTO("Không tìm thấy thông tin sự cố", 404, false);

            return new ResponseDTO("Lấy thông tin sự cố thành công", 200, true, MapToDTO(incident));
        }

        public async Task<ResponseDTO> GetByUserIdAsync(Guid userId)
        {
            if (userId == Guid.Empty)
                return new ResponseDTO("Vui lòng đăng nhập hệ thống", 400, false);

            var currentUser = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(userId);
            if (currentUser == null)
            {
                return new ResponseDTO("Không tìm thấy thông tin tài khoản", 404, false);
            }

            var allIncidents = await _unitOfWork.IncidentReportRepo.GetAllWithDetailsAsync();

            var roleName = currentUser.Role?.RoleName?.Trim();
            bool isStaff = string.Equals(roleName, "Staff", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(roleName, "Parking Staff", StringComparison.OrdinalIgnoreCase);

            var myIncidents = isStaff
                ? allIncidents.Where(i => i.ReportedByUserId == userId || i.HandledByStaffId == userId)
                : allIncidents.Where(i => i.ReportedByUserId == userId);

            var resultData = myIncidents.Select(MapToDTO).ToList();

            return new ResponseDTO("Lấy danh sách sự cố cá nhân thành công", 200, true, resultData);
        }

        public async Task<ResponseDTO> CreateAsync(CreateIncidentReportDTO dto)
        {
            if (dto == null)
                return new ResponseDTO("Dữ liệu gửi lên không hợp lệ", 400, false);

            var initialStatusStr = dto.Status ?? nameof(IncidentStatus.Open);

            var validation = await ValidateIncidentAsync(dto.SessionId, dto.ReportedByUserId, dto.IssueType, dto.Description, initialStatusStr, dto.HandledByStaffId);
            if (validation.Error != null)
                return validation.Error;

            var incident = new IncidentReport
            {
                IncidentId = Guid.NewGuid(),
                SessionId = dto.SessionId,
                ReportedByUserId = dto.ReportedByUserId,
                IssueType = dto.IssueType.Trim(),
                Description = dto.Description.Trim(),
                ProofImageUrl = string.IsNullOrWhiteSpace(dto.ProofImageUrl) ? null : dto.ProofImageUrl.Trim(),
                Status = validation.Status.ToString(),
                HandledByStaffId = dto.HandledByStaffId,
                ResolvedAt = (validation.Status == IncidentStatus.Resolved) ? DateTime.UtcNow : null
            };

            try
            {
                await _unitOfWork.IncidentReportRepo.AddAsync(incident);
                await _unitOfWork.SaveChangeAsync();

                incident.ReportedByUser = validation.ReportedByUser;
                incident.HandledByStaff = validation.HandledByStaff;

                return new ResponseDTO("Tạo báo cáo sự cố thành công", 201, true, MapToDTO(incident));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi lưu báo cáo sự cố: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> UpdateAsync(UpdateIncidentReportDTO dto)
        {
            if (dto == null || dto.IncidentId == Guid.Empty)
                return new ResponseDTO("Dữ liệu cập nhật không hợp lệ", 400, false);

            var incident = await _unitOfWork.IncidentReportRepo.GetByIdWithDetailsAsync(dto.IncidentId);
            if (incident == null)
                return new ResponseDTO("Không tìm thấy sự cố cần cập nhật", 404, false);

            var validation = await ValidateIncidentAsync(dto.SessionId, dto.ReportedByUserId, dto.IssueType, dto.Description, dto.Status, dto.HandledByStaffId);
            if (validation.Error != null)
                return validation.Error;

            incident.SessionId = dto.SessionId;
            incident.ReportedByUserId = dto.ReportedByUserId;
            incident.IssueType = dto.IssueType.Trim();
            incident.Description = dto.Description.Trim();
            incident.ProofImageUrl = string.IsNullOrWhiteSpace(dto.ProofImageUrl) ? null : dto.ProofImageUrl.Trim();
            incident.Status = validation.Status.ToString(); 
            incident.HandledByStaffId = dto.HandledByStaffId;
            incident.ResolutionNotes = string.IsNullOrWhiteSpace(dto.ResolutionNotes) ? null : dto.ResolutionNotes.Trim();

            if (validation.Status == IncidentStatus.Resolved)
            {
                incident.ResolvedAt = incident.ResolvedAt ?? DateTime.UtcNow;
            }
            else
            {
                incident.ResolvedAt = null;
            }

            try
            {
                await _unitOfWork.IncidentReportRepo.UpdateAsync(incident);
                await _unitOfWork.SaveChangeAsync();

                incident.ReportedByUser = validation.ReportedByUser;
                incident.HandledByStaff = validation.HandledByStaff;

                return new ResponseDTO("Cập nhật thông tin sự cố thành công", 200, true, MapToDTO(incident));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi cập nhật sự cố: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> AssignToStaffAsync(Guid incidentId, Guid staffId)
        {
            if (incidentId == Guid.Empty || staffId == Guid.Empty)
                return new ResponseDTO("Thông tin ID không hợp lệ", 400, false);

            var incident = await _unitOfWork.IncidentReportRepo.GetByIdWithDetailsAsync(incidentId);
            if (incident == null)
                return new ResponseDTO("Không tìm thấy sự cố", 404, false);

            if (!string.Equals(incident.Status, nameof(IncidentStatus.Open), StringComparison.OrdinalIgnoreCase))
                return new ResponseDTO("Chỉ có thể tiếp nhận sự cố đang ở trạng thái chờ (Open)", 400, false);

            var staffUser = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(staffId);
            if (staffUser == null)
                return new ResponseDTO("Nhân viên xử lý không tồn tại", 404, false);

            incident.HandledByStaffId = staffId;
            incident.Status = nameof(IncidentStatus.InProgress);

            await _unitOfWork.IncidentReportRepo.UpdateAsync(incident);
            await _unitOfWork.SaveChangeAsync();

            incident.HandledByStaff = staffUser;
            return new ResponseDTO("Nhân viên đã tiếp nhận sự cố thành công", 200, true, MapToDTO(incident));
        }

        public async Task<ResponseDTO> ResolveAsync(Guid incidentId, Guid staffId, ResolveIncidentDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.ResolutionNotes))
                return new ResponseDTO("Vui lòng nhập ghi chú giải quyết sự cố", 400, false);

            var incident = await _unitOfWork.IncidentReportRepo.GetByIdWithDetailsAsync(incidentId);
            if (incident == null)
                return new ResponseDTO("Không tìm thấy sự cố", 404, false);

            if (string.Equals(incident.Status, nameof(IncidentStatus.Resolved), StringComparison.OrdinalIgnoreCase))
                return new ResponseDTO("Sự cố này đã được xử lý hoàn tất từ trước", 400, false);

            if (incident.HandledByStaffId != staffId)
                return new ResponseDTO("Bạn không thể đóng sự cố này vì bạn không phải là nhân viên được gán xử lý", 403, false);

            incident.Status = nameof(IncidentStatus.Resolved); 
            incident.ResolutionNotes = dto.ResolutionNotes.Trim();
            incident.ResolvedAt = DateTime.UtcNow;

            await _unitOfWork.IncidentReportRepo.UpdateAsync(incident);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO("Đã đóng và hoàn tất xử lý sự cố", 200, true, MapToDTO(incident));
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty)
                return new ResponseDTO("Vui lòng nhập IncidentId", 400, false);

            var incident = await _unitOfWork.IncidentReportRepo.GetByIdAsync(id);
            if (incident == null)
                return new ResponseDTO("Không tìm thấy sự cố cần xóa", 404, false);

            try
            {
                _unitOfWork.IncidentReportRepo.Delete(incident);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Xóa báo cáo sự cố thành công", 200, true);
            }
            catch (DbUpdateException)
            {
                return new ResponseDTO("Không thể xóa sự cố này vì dữ liệu đã liên kết với báo cáo doanh thu/lịch sử bãi xe", 400, false);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi xóa sự cố: {ex.Message}", 500, false);
            }
        }

        private async Task<(User? ReportedByUser, User? HandledByStaff, IncidentStatus Status, ResponseDTO? Error)> ValidateIncidentAsync(
            Guid? sessionId, Guid reportedByUserId, string? issueType, string? description, string? status, Guid? handledByStaffId)
        {
            if (reportedByUserId == Guid.Empty)
                return (null, null, default, new ResponseDTO("Vui lòng chỉ định người báo cáo sự cố", 400, false));

            if (string.IsNullOrWhiteSpace(issueType))
                return (null, null, default, new ResponseDTO("Loại sự cố không được để trống", 400, false));

            if (string.IsNullOrWhiteSpace(description))
                return (null, null, default, new ResponseDTO("Vui lòng nhập nội dung mô tả chi tiết sự cố", 400, false));

            if (string.IsNullOrWhiteSpace(status) || !Enum.TryParse<IncidentStatus>(status.Trim(), true, out var parsedStatus))
            {
                return (null, null, default, new ResponseDTO("Trạng thái sự cố không hợp lệ (Chỉ nhận: Open, InProgress, Resolved, Cancelled)", 400, false));
            }

            if (sessionId.HasValue && sessionId.Value != Guid.Empty)
            {
                var sessionExists = await _unitOfWork.ParkingSessionRepo.AnyAsync(s => s.SessionId == sessionId.Value);
                if (!sessionExists)
                    return (null, null, default, new ResponseDTO("Mã phiên gửi xe liên quan không tồn tại trên hệ thống", 400, false));
            }

            var reportedByUser = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(reportedByUserId);
            if (reportedByUser == null)
                return (null, null, default, new ResponseDTO("Tài khoản người báo cáo không tồn tại", 400, false));

            User? handledByStaff = null;
            if (handledByStaffId.HasValue && handledByStaffId.Value != Guid.Empty)
            {
                handledByStaff = await _unitOfWork.UserRepo.GetByIdWithRoleAsync(handledByStaffId.Value);
                if (handledByStaff == null)
                    return (null, null, default, new ResponseDTO("Nhân viên phụ trách được chỉ định không tồn tại", 400, false));

                var roleName = handledByStaff.Role?.RoleName?.Trim();
                bool isStaffOrManager = string.Equals(roleName, "Staff", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(roleName, "Manager", StringComparison.OrdinalIgnoreCase);

                if (!isStaffOrManager)
                {
                    return (null, null, default, new ResponseDTO("Tài khoản được gán xử lý phải có quyền là Staff hoặc Manager", 400, false));
                }
            }

            return (reportedByUser, handledByStaff, parsedStatus, null);
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