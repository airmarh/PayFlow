using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;

namespace PayFlow.API.Controllers;

[ApiController]
[Route("api/webhooks")]
[Produces("application/json")]
[EnableRateLimiting("webhook")]
public class WebhookController : ControllerBase
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger<WebhookController> _logger;
    private readonly string? _webhookSecret;

    public WebhookController(
        IWebhookService webhookService,
        ILogger<WebhookController> logger,
        IConfiguration configuration)
    {
        _webhookService = webhookService;
        _logger         = logger;
        _webhookSecret  = configuration["Webhook:SecretKey"];
    }

    /// <summary>Receives a payment-status notification from the payment provider.</summary>
    /// <response code="200">Webhook accepted and processed successfully.</response>
    /// <response code="400">Payload is malformed or required fields are missing.</response>
    /// <response code="401">Signature header is missing or invalid.</response>
    /// <response code="404">Referenced transaction does not exist.</response>
    [HttpPost("notify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReceiveWebhook(
        CancellationToken cancellationToken)
    {
        // Buffer the body so we can verify the HMAC signature before deserialising.
        Request.EnableBuffering();
        using var reader  = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody       = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        if (!string.IsNullOrWhiteSpace(_webhookSecret))
        {
            var signatureHeader = Request.Headers["X-PayFlow-Signature"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(signatureHeader))
            {
                _logger.LogWarning("Webhook rejected — X-PayFlow-Signature header is missing.");
                return Unauthorized(new { message = "Missing webhook signature header." });
            }

            var expectedSignature = ComputeHmacSha512(rawBody, _webhookSecret);

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(signatureHeader),
                    Encoding.UTF8.GetBytes(expectedSignature)))
            {
                _logger.LogWarning("Webhook rejected — signature mismatch.");
                return Unauthorized(new { message = "Invalid webhook signature." });
            }
        }
        else
        {
            _logger.LogWarning(
                "Webhook:SecretKey is not configured — signature verification skipped. " +
                "Do not use this setting in production.");
        }

        WebhookPayload? payload;
        try
        {
            payload = System.Text.Json.JsonSerializer.Deserialize<WebhookPayload>(
                rawBody,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });
        }
        catch
        {
            return BadRequest(new { message = "Webhook payload could not be parsed as JSON." });
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.TransactionReference))
            return BadRequest(new { message = "TransactionReference is required." });

        payload.RawPayload = rawBody;

        _logger.LogInformation(
            "Webhook notification received for reference {Reference}",
            payload.TransactionReference);

        await _webhookService.ProcessWebhookAsync(payload, cancellationToken);

        return Ok(new { message = "Webhook processed successfully." });
    }

    // Returns a lowercase hex HMAC-SHA512 — the same format Paystack and Flutterwave use.
    private static string ComputeHmacSha512(string data, string secret)
    {
        var keyBytes  = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA512(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
