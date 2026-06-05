using System;

namespace Common.DTOs.PricingPolicy
{
    public class PricingPolicyDTO
    {
        public Guid PolicyId { get; set; }
        public Guid VehicleTypeId { get; set; }
        public string? VehicleTypeName { get; set; }
        public decimal BasePrice { get; set; }
        public int BaseHours { get; set; }
        public decimal ExtraHourPrice { get; set; }
        public decimal? NightSurcharge { get; set; }
        public DateTime EffectiveDate { get; set; }
        public string Status { get; set; }
    }

    public class CreatePricingPolicyDTO
    {
        public Guid VehicleTypeId { get; set; }
        public decimal BasePrice { get; set; }
        public int BaseHours { get; set; }
        public decimal ExtraHourPrice { get; set; }
        public decimal? NightSurcharge { get; set; }
        public DateTime EffectiveDate { get; set; }
        public string? Status { get; set; }
    }

    public class UpdatePricingPolicyDTO
    {
        public Guid PolicyId { get; set; }
        public Guid VehicleTypeId { get; set; }
        public decimal BasePrice { get; set; }
        public int BaseHours { get; set; }
        public decimal ExtraHourPrice { get; set; }
        public decimal? NightSurcharge { get; set; }
        public DateTime EffectiveDate { get; set; }
        public string Status { get; set; }
    }
}
