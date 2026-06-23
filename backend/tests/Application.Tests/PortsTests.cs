using Academy.Application.Abstractions;
using Academy.Application.Billing;

namespace Academy.Application.Tests;

public class PortsTests
{
    [Fact]
    public void ProviderPorts_AreDefinedInApplicationLayer()
    {
        // Ports must live in Application (Infrastructure implements them in later milestones).
        Assert.True(typeof(IVideoProvider).IsInterface);
        Assert.True(typeof(IPaymentGateway).IsInterface);   // M3: moved to Academy.Application.Billing
        Assert.True(typeof(IEmailSender).IsInterface);
        Assert.True(typeof(IObjectStorage).IsInterface);
        Assert.True(typeof(INotificationSender).IsInterface);
    }
}
