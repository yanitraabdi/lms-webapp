namespace Academy.Api.Tests;

public class SmokeTests
{
    [Fact]
    public void ApiAssembly_IsReferenced() => Assert.NotNull(typeof(Program).Assembly);
}
