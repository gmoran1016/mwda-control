namespace Mwda.Control.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void TestAssemblyLoads()
    {
        Assert.NotNull(typeof(Mwda.Control.App).Assembly);
    }
}
