using Microsoft.Extensions.Configuration;

namespace Academy.Infrastructure.Billing;

public class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>Active payment gateway: "dev" (local sim) or "xendit" (real, needs creds).</summary>
    public string Provider { get; set; } = "dev";

    /// <summary>Shared secret used to sign/verify inbound webhooks (Xendit callback token).
    /// Dev default only — override via Billing__CallbackToken in real environments.</summary>
    public string CallbackToken { get; set; } = "dev-callback-token-change-me";

    /// <summary>Frontend base URL the hosted checkout redirects back to.</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:3001";

    public int MonthlyDays { get; set; } = 30;
    public int AnnualDays { get; set; } = 365;
    public int GraceDays { get; set; } = 14;

    public bool IsDev => string.Equals(Provider, "dev", StringComparison.OrdinalIgnoreCase);
}

public static class BillingOptionsFactory
{
    public static BillingOptions Build(IConfiguration config)
    {
        var o = new BillingOptions();
        config.GetSection(BillingOptions.SectionName).Bind(o);
        // Reuse the configured frontend origin if a billing-specific one isn't set.
        if (config["Billing:FrontendBaseUrl"] is not { Length: > 0 }
            && config["Cors:AllowedOrigins:0"] is { Length: > 0 } origin)
            o.FrontendBaseUrl = origin;
        return o;
    }
}
