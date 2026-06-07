using Common.DTOs;
using Common.DTOs.PricingPolicy;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IPricingPolicyService
    {
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreatePricingPolicyDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdatePricingPolicyDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
