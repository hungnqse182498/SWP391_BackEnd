using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public class PayOSPaymentLinkResult
    {
        public string PaymentUrl { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
        public string PaymentLinkId { get; set; } = string.Empty;
    }

    public interface IPayOSService
    {
        Task<string> CreatePaymentLinkAsync(Payment payment);
        Task<PayOSPaymentLinkResult> CreatePaymentLinkDetailsAsync(Payment payment);
        Task CancelPaymentLinkAsync(Payment payment);
    }
}
