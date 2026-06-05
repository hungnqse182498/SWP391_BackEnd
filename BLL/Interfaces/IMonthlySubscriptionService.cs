using Common.DTOs;
using Common.DTOs.MonthlySubscription;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IMonthlySubscriptionService
    {
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreateMonthlySubscriptionDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdateMonthlySubscriptionDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
