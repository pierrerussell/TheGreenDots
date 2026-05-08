using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectCallisto.Application.Billing;

namespace ProjectCallisto.API.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly ILogger<BillingController> _logger;

    public BillingController(IBillingService billingService, ILogger<BillingController> logger)
    {
        _billingService = billingService;
        _logger = logger;
    }

    [HttpPost("webhook")]
    [AllowAnonymous] // Stripe calls this endpoint
    public async Task<IActionResult> HandleWebhook()
    {
        try
        {
            // Read raw body
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();

            // Get Stripe signature header
            if (!Request.Headers.TryGetValue("Stripe-Signature", out var signatureHeader))
            {
                _logger.LogWarning("Webhook received without Stripe-Signature header");
                return BadRequest(new { error = "Missing Stripe-Signature header" });
            }

            var signature = signatureHeader.ToString();

            // Process webhook
            await _billingService.HandleWebhookEventAsync(json, signature);

            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid webhook signature");
            return BadRequest(new { error = "Invalid signature" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            // Still return 200 to prevent Stripe retries for non-transient errors
            // Only return 5xx for actual server errors that should be retried
            return StatusCode(500, new { error = "Webhook processing failed" });
        }
    }

    [HttpGet("subscription/{organisationId}")]
    [Authorize]
    public async Task<IActionResult> GetSubscription(Guid organisationId)
    {
        try
        {
            var subscription = await _billingService.GetSubscriptionAsync(organisationId);
            return Ok(subscription);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to get subscription for organisation {OrganisationId}", organisationId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching subscription for organisation {OrganisationId}", organisationId);
            return StatusCode(500, new { error = "Failed to fetch subscription" });
        }
    }

    [HttpPost("subscription/{organisationId}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelSubscription(Guid organisationId)
    {
        try
        {
            await _billingService.CancelSubscriptionAsync(organisationId);
            return Ok(new { message = "Subscription will be cancelled at the end of the current billing period" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to cancel subscription for organisation {OrganisationId}", organisationId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription for organisation {OrganisationId}", organisationId);
            return StatusCode(500, new { error = "Failed to cancel subscription" });
        }
    }

    [HttpPost("subscription/{organisationId}/uncancel")]
    [Authorize]
    public async Task<IActionResult> UncancelSubscription(Guid organisationId)
    {
        try
        {
            await _billingService.UncancelSubscriptionAsync(organisationId);
            return Ok(new { message = "Subscription cancellation has been reversed" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to uncancel subscription for organisation {OrganisationId}", organisationId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uncancelling subscription for organisation {OrganisationId}", organisationId);
            return StatusCode(500, new { error = "Failed to uncancel subscription" });
        }
    }

    [HttpPost("checkout")]
    [Authorize]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutRequest request)
    {
        try
        {
            var result = await _billingService.CreateCheckoutSessionAsync(
                request.OrganisationId,
                request.SeatCount,
                request.BillingInterval);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid checkout request for organisation {OrganisationId}", request.OrganisationId);
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Checkout failed for organisation {OrganisationId}", request.OrganisationId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checkout session for organisation {OrganisationId}", request.OrganisationId);
            return StatusCode(500, new { error = "Failed to create checkout session" });
        }
    }
}

public class CreateCheckoutRequest
{
    public Guid OrganisationId { get; set; }
    public int SeatCount { get; set; }
    public BillingInterval BillingInterval { get; set; }
}
