using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs.Subscription
{
    public class SubscriptionRenewalDTO
    {
        public Guid RenewalId { get; set; }
        public Guid SubscriptionId { get; set; }
        public DateTime OldEndDate { get; set; }
        public DateTime NewEndDate { get; set; }
        public decimal Amount { get; set; }
        public DateTime? RenewalDate { get; set; }
    }

    public class RenewSubscriptionDTO
    {
        public Guid PackageId { get; set; }
    }

    public class CreateDirectRenewalDTO
    {
        public Guid SubscriptionId { get; set; }
        public int Months { get; set; }
        public decimal Amount { get; set; }
    }

    public class UpdateRenewalDTO
    {
        public Guid RenewalId { get; set; }
        public decimal Amount { get; set; }
        public DateTime? RenewalDate { get; set; }
    }
}
