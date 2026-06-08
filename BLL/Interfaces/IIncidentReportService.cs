using Common.DTOs;
using Common.DTOs.IncidentReport;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IIncidentReportService
    {
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreateIncidentReportDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdateIncidentReportDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
        Task<ResponseDTO> AssignToStaffAsync(Guid incidentId, Guid staffId);
        Task<ResponseDTO> ResolveAsync(Guid incidentId, Guid staffId, ResolveIncidentDTO dto);
        Task<ResponseDTO> GetByUserIdAsync(Guid userId);


    }
}
