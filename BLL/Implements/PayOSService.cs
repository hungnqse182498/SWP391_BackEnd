using BLL.Interfaces;
using Common.Enums;
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
        var result = await CreatePaymentLinkDetailsAsync(payment);
        return result.PaymentUrl;
    }

    public async Task<PayOSPaymentLinkResult> CreatePaymentLinkDetailsAsync(Payment payment)
    {
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var descriptionPrefix = payment.PaymentType == PaymentType.CheckoutFee.ToString()
            ? "PARK"
            : payment.PaymentType == PaymentType.Deposit.ToString()
                ? "RES"
                : "SUB";

        var request = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = (int)payment.Amount,
            Description = $"{descriptionPrefix}-{payment.PaymentId.ToString()[..8]}",
            ReturnUrl = _config.ReturnUrl,
            CancelUrl = _config.CancelUrl
        };

        var result = await _payOS.PaymentRequests.CreateAsync(request);

        payment.TransactionReference = orderCode.ToString();

        return new PayOSPaymentLinkResult
        {
            PaymentUrl = result.CheckoutUrl,
            QrCode = result.QrCode,
            PaymentLinkId = result.PaymentLinkId
        };
    }

    public async Task CancelPaymentLinkAsync(Payment payment)
    {
        if (!long.TryParse(payment.TransactionReference, out var orderCode))
        {
            throw new InvalidOperationException("Thanh toán PayOS chưa có order code hợp lệ");
        }

        await _payOS.PaymentRequests.CancelAsync(orderCode, "Nhân viên hủy checkout");
    }

    public async Task<PayOSPaymentLinkResult> GetPaymentLinkDetailsAsync(Payment payment)
    {
        if (!long.TryParse(payment.TransactionReference, out var orderCode))
        {
            throw new InvalidOperationException("Thanh toán PayOS chưa có order code hợp lệ");
        }

        var result = await _payOS.PaymentRequests.GetAsync(orderCode);
        return new PayOSPaymentLinkResult
        {
            PaymentUrl = $"https://pay.payos.vn/web/{result.Id}",
            QrCode = string.Empty,
            PaymentLinkId = result.Id
        };
    }

}
