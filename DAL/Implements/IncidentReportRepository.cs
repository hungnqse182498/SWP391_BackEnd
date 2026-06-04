using DAL.Interfaces;
using DAL.Models;

namespace DAL.Implements
{
    public class IncidentReportRepository : GenericRepository<IncidentReport>, IIncidentReportRepository
    {
        public IncidentReportRepository(ParkingDBContext context) : base(context)
        {
        }
    }
}
