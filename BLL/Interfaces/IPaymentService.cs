using Common.DTOs;
using Common.DTOs.Payment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BLL.Interfaces
{
    public interface IPaymentService
    {
        //Task<ResponseDTO> PayOSWebhookAsync(PayOSWebhookDTO dto);
        Task PayOSWebhookAsync(PayOSWebhookDTO dto);
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(CreatePaymentDTO dto);
        Task<ResponseDTO> UpdateAsync(UpdatePaymentDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
