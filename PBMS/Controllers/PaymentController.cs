using BLL.Interfaces;
using Common.DTOs.Payment;
using Microsoft.AspNetCore.Authorization;
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
    [AllowAnonymous]
    public async Task<IActionResult> PayOSWebhook([FromBody] PayOSWebhookDTO dto)
    {
        await _paymentService.PayOSWebhookAsync(dto);
        return Ok();
    }

    [HttpGet]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> GetAll()
    {
        var res = await _paymentService.GetAllAsync();
        return StatusCode(res.StatusCode, res);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var res = await _paymentService.GetByIdAsync(id);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Create([FromBody] CreatePaymentDTO dto)
    {
        var res = await _paymentService.CreateAsync(dto);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPut]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Update([FromBody] UpdatePaymentDTO dto)
    {
        var res = await _paymentService.UpdateAsync(dto);
        return StatusCode(res.StatusCode, res);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var res = await _paymentService.DeleteAsync(id);
        return StatusCode(res.StatusCode, res);
    }
}

