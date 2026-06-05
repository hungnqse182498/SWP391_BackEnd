using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs.Payment
{
    public class PayOSWebhookDTO
    {
        public string Code { get; set; } = "00";

        public string Desc { get; set; } = "success";

        public PayOSWebhookData Data { get; set; } = null!;
    }
}
