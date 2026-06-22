using Academy.Domain.Common;

namespace Academy.Domain.Tests;

public class EntityTests
{
    private sealed class Sample : Entity;

    [Fact]
    public void NewEntity_HasAppAssignedUuidV7()
    {
        var e = new Sample();

        Assert.NotEqual(Guid.Empty, e.Id);
        Assert.Equal(7, e.Id.Version);                 // UUID v7 (index locality)
        Assert.NotEqual(default, e.CreatedAt);
    }
}
