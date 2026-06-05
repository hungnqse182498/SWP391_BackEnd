using BLL.Interfaces;
using Common.Settings;
using DAL.Models;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;

public class PayOSService : IPayOSService
{
    private readonly PayOSClient _payOS;
    private readonly PayOSConfig _config;

    public PayOSService(IOptions<PayOSConfig> options)
    {
        _config = options.Value;

        _payOS = new PayOSClient(
            _config.ClientId,
            _config.ApiKey,
            _config.ChecksumKey);
    }

    public async Task<string> CreatePaymentLinkAsync(Payment payment)
    {
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var request = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = (int)payment.Amount,
            Description = $"RES-{payment.PaymentId.ToString()[..8]}",
            ReturnUrl = _config.ReturnUrl,
            CancelUrl = _config.CancelUrl
        };

        var result = await _payOS.PaymentRequests.CreateAsync(request);

        payment.TransactionReference = orderCode.ToString();

        return result.CheckoutUrl;
    }
}