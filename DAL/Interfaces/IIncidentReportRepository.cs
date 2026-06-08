using DAL.Models;

namespace DAL.Interfaces
{
    public interface IIncidentReportRepository : IGenericRepository<IncidentReport>
    {
        Task<IEnumerable<IncidentReport>> GetAllWithDetailsAsync();
        Task<IncidentReport?> GetByIdWithDetailsAsync(Guid id);
    }
}
