using Common.DTOs;
using Common.DTOs.Reports;

namespace BLL.Interfaces
{
    public interface IReportService
    {
        Task<ResponseDTO> GetReportTypesAsync();
        Task<ResponseDTO> GetSummaryAsync(ReportFilterDTO filter);
        Task<ResponseDTO> GetRevenueAsync(ReportFilterDTO filter);
        Task<ResponseDTO> GetParkingOperationsAsync(ReportFilterDTO filter);
        Task<ResponseDTO> ExportAsync(ReportExportRequestDTO request);
    }
}
