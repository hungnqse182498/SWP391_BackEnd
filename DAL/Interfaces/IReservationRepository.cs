using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IReservationRepository : IGenericRepository<Reservation>
    {
        Task<List<Reservation>> GetByUserIdWithPaymentsAsync(Guid userId);
        Task<Reservation?> GetDetailWithRelationsAsync(Guid reservationId);
        Task<List<Reservation>> GetByAdminFiltersAsync(string? status, DateTime? date);
    }
}
