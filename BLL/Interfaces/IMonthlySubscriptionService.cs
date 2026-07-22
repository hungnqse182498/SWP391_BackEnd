using Common.DTOs;
using Common.DTOs.Subscription;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IMonthlySubscriptionService
    {
        Task<ResponseDTO> RegisterAsync(Guid userId, RegisterMonthlySubscriptionDTO dto);
        Task<ResponseDTO> CreateForUserAsync(ManagerCreateMonthlySubscriptionDTO dto);
        Task<ResponseDTO> GetAllAsync(); 
        Task<ResponseDTO> GetDetailAsync(Guid id);
        Task<ResponseDTO> GetMyAsync(Guid userId);
        Task<ResponseDTO> GetByUserAsync(Guid userId);
        Task<ResponseDTO> UpdateAsync(Guid id, UpdateMonthlySubscriptionDTO dto); 
        Task<ResponseDTO> CancelAsync(Guid id);
        Task<ResponseDTO> DeleteAsync(Guid id); 
        Task<ResponseDTO> CreatePaymentAsync(Guid subscriptionId, Guid userId);
    }
}
