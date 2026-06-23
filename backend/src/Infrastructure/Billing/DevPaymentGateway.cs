using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Academy.Application.Billing;

namespace Academy.Infrastructure.Billing;

/// <summary>
/// Local gateway that simulates Xendit so the full payment flow is testable without
/// credentials: checkout returns a dev-pay URL, and webhooks are HMAC-SHA256 signed
/// with the shared callback token. Swapped for a real XenditGateway when
/// Billing:Provider = "xendit" + creds are supplied.
/// </summary>
public class DevPaymentGateway(BillingOptions options) : IPaymentGateway
{
    public string Source => "dev";

    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutIntent intent, CancellationToken ct = default)
    {
        var providerRef = $"dev_{Guid.CreateVersion7():N}";
        var url = $"{options.FrontendBaseUrl.TrimEnd('/')}/checkout/dev-pay" +
                  $"?ref={Uri.EscapeDataString(providerRef)}" +
                  $"&amount={intent.AmountIdr:0}" +
                  $"&kind={intent.Kind}";
        return Task.FromResult(new CheckoutSession(url, providerRef));
    }

    public bool VerifyWebhookSignature(string rawBody, string? callbackToken)
    {
        if (string.IsNullOrEmpty(callbackToken)) return false;
        var expected = Sign(rawBody, options.CallbackToken);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(callbackToken));
    }

    public bool TryParseWebhook(string rawBody, out PaymentWebhook webhook)
    {
        webhook = default!;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var externalId = root.GetProperty("externalId").GetString();
            var providerRef = root.GetProperty("providerRef").GetString();
            var typeStr = root.GetProperty("type").GetString();
            if (externalId is null || providerRef is null || typeStr is null) return false;

            PaymentWebhookType type;
            if (typeStr.Equals("succeeded", StringComparison.OrdinalIgnoreCase)) type = PaymentWebhookType.Succeeded;
            else if (typeStr.Equals("failed", StringComparison.OrdinalIgnoreCase)) type = PaymentWebhookType.Failed;
            else return false;

            decimal? amount = root.TryGetProperty("amountIdr", out var a) && a.ValueKind == JsonValueKind.Number
                ? a.GetDecimal() : null;
            string? method = root.TryGetProperty("method", out var m) ? m.GetString() : null;

            webhook = new PaymentWebhook(externalId, providerRef, type, amount, method);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>HMAC-SHA256(body) hex — the dev-pay endpoint signs with this; we verify with it.</summary>
    public static string Sign(string body, string secret)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexStringLower(h.ComputeHash(Encoding.UTF8.GetBytes(body)));
    }
}
