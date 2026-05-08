namespace ProjectCallisto.Application.Billing;

public class CheckoutResult
{
    public bool Success { get; set; }
    public string? SessionId { get; set; }
    public string? SessionUrl { get; set; }
    public string? Message { get; set; }
}