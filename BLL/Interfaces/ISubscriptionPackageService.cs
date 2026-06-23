using Common.DTOs;
using Common.DTOs.Subscription;

namespace BLL.Interfaces
{
    public interface ISubscriptionPackageService
    {
        Task<ResponseDTO> GetAllPackagesAsync();
        Task<ResponseDTO> GetPackageByIdAsync(Guid id);
        Task<ResponseDTO> CreatePackageAsync(CreateSubscriptionPackageDTO dto);
        Task<ResponseDTO> UpdatePackageAsync(Guid id, UpdateSubscriptionPackageDTO dto);
        Task<ResponseDTO> DeletePackageAsync(Guid id);
    }
}
