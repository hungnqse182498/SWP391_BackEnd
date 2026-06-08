using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Implements
{
    public class IncidentReportRepository : GenericRepository<IncidentReport>, IIncidentReportRepository
    {
        public IncidentReportRepository(ParkingDBContext context) : base(context)
        {
        }

        public async Task<IEnumerable<IncidentReport>> GetAllWithDetailsAsync()
        {
            return await _context.IncidentReports
                .Include(i => i.ReportedByUser)
                .Include(i => i.HandledByStaff)
                .Include(i => i.Session)
                .OrderBy(i => i.Status)
                .ThenByDescending(i => i.IncidentId)
                .ToListAsync();
        }

        public async Task<IncidentReport?> GetByIdWithDetailsAsync(Guid id)
        {
            return await _context.IncidentReports
                .Include(i => i.ReportedByUser)
                .Include(i => i.HandledByStaff)
                .Include(i => i.Session)
                .FirstOrDefaultAsync(i => i.IncidentId == id);
        }
    }
}
