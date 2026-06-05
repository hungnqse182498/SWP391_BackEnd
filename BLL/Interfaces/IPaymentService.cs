using Common.DTOs;
using Common.DTOs.Payment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IPaymentService
    {
        Task<ResponseDTO> PayOSWebhookAsync(PayOSWebhookDTO dto);
    }
}
