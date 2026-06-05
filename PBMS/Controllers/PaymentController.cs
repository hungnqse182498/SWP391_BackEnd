using BLL.Interfaces;
using Common.DTOs.Payment;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("payos-webhook")]
    public async Task<IActionResult> PayOSWebhook([FromBody] PayOSWebhookDTO dto)
    {
        var result = await _paymentService.PayOSWebhookAsync(dto);
        return StatusCode(result.StatusCode, result);
    }
}