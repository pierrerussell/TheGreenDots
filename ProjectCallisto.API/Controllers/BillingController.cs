using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectCallisto.Application.Billing;

namespace ProjectCallisto.API.Controllers;

[ApiController]
[Route("api/billing")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly ILogger<BillingController> _logger;

    public BillingController(IBillingService billingService, ILogger<BillingController> logger)
    {
        _billingService = billingService;
        _logger = logger;
    }

    [HttpPost("checkout")]
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
