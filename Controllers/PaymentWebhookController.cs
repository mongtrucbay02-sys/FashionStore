using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using OutfitStudio.Services;

namespace OutfitStudio.Controllers;

[ApiController]
public sealed class PaymentWebhookController(QrPaymentService payments, IConfiguration configuration) : ControllerBase
{
    [HttpPost("/payments/sepay/webhook")]
    [RequestSizeLimit(16384)]
    public IActionResult SePay([FromBody] SePayTransfer transfer)
    {
        var key = configuration["SePay:WebhookApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return NotFound();
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes("Apikey " + key));
        var supplied = SHA256.HashData(Encoding.UTF8.GetBytes(Request.Headers.Authorization.ToString()));
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied)) return Unauthorized();

        var accepted = payments.Reconcile(transfer);
        return Ok(new { success = true, matched = accepted });
    }
}
