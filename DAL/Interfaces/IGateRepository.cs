using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace DAL.Interfaces
{
    public interface IGateRepository : IGenericRepository<Gate>
    {
        Task<IEnumerable<Gate>> GetAllWithFloorAsync();
        Task<Gate?> GetByIdWithFloorAsync(Guid id);
        Task<bool> IsNameDuplicateAsync(string gateName, Guid? excludeGateId = null);
    }
}
