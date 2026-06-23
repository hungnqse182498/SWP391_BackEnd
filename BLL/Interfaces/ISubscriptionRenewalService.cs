using Common.DTOs;
using Common.DTOs.Subscription;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface ISubscriptionRenewalService
    {
        Task<ResponseDTO> GetRenewalsAsync(Guid subscriptionId);
        Task<ResponseDTO> RenewAsync(Guid subscriptionId, Guid userId, RenewSubscriptionDTO dto);
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateDirectAsync(CreateDirectRenewalDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdateRenewalDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
