using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs.Payment
{
    public class PayOSWebhookData
    {
        public long OrderCode { get; set; }

        public int Amount { get; set; }

        public string Description { get; set; } = string.Empty;

        public string Reference { get; set; } = string.Empty;

        public string TransactionDateTime { get; set; } = string.Empty;
    }
}
