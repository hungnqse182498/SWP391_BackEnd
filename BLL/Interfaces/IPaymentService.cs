using Common.DTOs;
using Common.DTOs.Payment;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IPaymentService
    {
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreatePaymentDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdatePaymentDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
