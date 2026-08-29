namespace FlurNetz.Foundation.Tests;

public sealed class BootstrapTests
{
    [Fact]
    public void TestAssemblyUsesFlurNetzNamespace()
    {
        Assert.Equal("FlurNetz.Foundation.Tests", typeof(BootstrapTests).Namespace);
    }
}
