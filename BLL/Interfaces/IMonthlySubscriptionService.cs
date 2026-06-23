using Common.DTOs;
using Common.DTOs.Subscription;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IMonthlySubscriptionService
    {
        Task<ResponseDTO> RegisterAsync(Guid userId, RegisterMonthlySubscriptionDTO dto);
        Task<ResponseDTO> CreatePaymentAsync(Guid subscriptionId, Guid userId);
        Task<ResponseDTO> GetMyAsync(Guid userId);
        Task<ResponseDTO> GetByUserAsync(Guid userId);
        Task<ResponseDTO> GetDetailAsync(Guid id);
        Task<ResponseDTO> CancelAsync(Guid id);
    }
}
