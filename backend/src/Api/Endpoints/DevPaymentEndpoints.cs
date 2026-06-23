using System.Text.Json;
using Academy.Application.Billing;
using Academy.Infrastructure.Billing;

namespace Academy.Api.Endpoints;

/// <summary>
/// Dev-only endpoints simulating the payment provider: the dev-pay page calls these to
/// "complete" a checkout, which builds a signed webhook and feeds it through the REAL
/// webhook processor (so signature + idempotency + state machine are all exercised).
/// Mapped only when Billing:Provider = "dev".
/// </summary>
public static class DevPaymentEndpoints
{
    public static IEndpointRouteBuilder MapDevPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dev/payments").WithTags("DevPayments");

        group.MapPost("/{providerRef}/succeed", (string providerRef, BillingOptions opts, IPaymentWebhookProcessor processor, CancellationToken ct) =>
            SimulateAsync(providerRef, "succeeded", opts, processor, ct));

        group.MapPost("/{providerRef}/fail", (string providerRef, BillingOptions opts, IPaymentWebhookProcessor processor, CancellationToken ct) =>
            SimulateAsync(providerRef, "failed", opts, processor, ct));

        return app;
    }

    private static async Task<IResult> SimulateAsync(
        string providerRef, string type, BillingOptions opts, IPaymentWebhookProcessor processor, CancellationToken ct)
    {
        // Property names must match DevPaymentGateway.TryParseWebhook.
        var body = JsonSerializer.Serialize(new
        {
            externalId = $"evt_{Guid.CreateVersion7():N}",
            providerRef,
            type,
            method = "dev",
        });
        var signature = DevPaymentGateway.Sign(body, opts.CallbackToken);
        var result = await processor.ProcessAsync(body, signature, ct);
        return Results.Ok(new { outcome = result.Outcome.ToString() });
    }
}
