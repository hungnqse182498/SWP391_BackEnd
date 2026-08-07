using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IParkingSessionRepository : IGenericRepository<ParkingSession>
    {
        Task<List<ParkingSession>> GetAllSessionsWithDetailsAsync();
        Task<List<ParkingSession>> GetSessionsByDriverUserIdWithDetailsAsync(Guid userId);
        Task<ParkingSession?> GetSessionDetailAsync(Guid id);
        Task<ParkingSession?> GetActiveSessionWithDetailsAsync(Guid? sessionId, string? licensePlate);
        Task<bool> HasActiveSessionByLicensePlateAsync(string licensePlate, Guid? excludeSessionId = null);
        Task<int> CountActiveGuestSessionsByFloorAsync(Guid vehicleTypeId, Guid floorId);
        Task<List<ParkingSession>> GetEntrySessionsForReportAsync(DateTime from, DateTime to, Guid? vehicleTypeId);
        Task<List<ParkingSession>> GetExitSessionsForReportAsync(DateTime from, DateTime to, Guid? vehicleTypeId);
        Task<int> CountActiveSessionsForReportAsync(Guid? vehicleTypeId);
        Task<bool> ExistsAsync(Guid sessionId);
    }
}
