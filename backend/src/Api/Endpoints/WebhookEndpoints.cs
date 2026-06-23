using Academy.Application.Billing;

namespace Academy.Api.Endpoints;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        // Inbound payment webhooks. No auth — the body is signature-verified and the
        // x-callback-token header is checked. Idempotent on webhook_events.external_id (GR-2).
        app.MapPost("/api/webhooks/xendit", async (
            HttpRequest request, IPaymentWebhookProcessor processor, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);
            var token = request.Headers["x-callback-token"].ToString();

            var result = await processor.ProcessAsync(rawBody, string.IsNullOrEmpty(token) ? null : token, ct);
            return result.Outcome switch
            {
                WebhookOutcome.InvalidSignature => Results.StatusCode(StatusCodes.Status403Forbidden),
                WebhookOutcome.Ignored => Results.BadRequest(),
                _ => Results.Ok(new { outcome = result.Outcome.ToString() }), // Processed | Duplicate → 200
            };
        }).RequireRateLimiting("payment").WithTags("Webhooks");

        return app;
    }
}
