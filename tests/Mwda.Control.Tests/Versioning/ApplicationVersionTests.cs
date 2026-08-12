using Mwda.Control.Versioning;

namespace Mwda.Control.Tests.Versioning;

public sealed class ApplicationVersionTests
{
    [Fact]
    public void InformationalVersionDropsBuildMetadata()
    {
        Assert.Equal(
            "1.2.3",
            ApplicationVersion.Normalize("1.2.3+commit.abc", new Version(9, 9, 9)));
    }

    [Fact]
    public void AssemblyVersionIsUsedWhenInformationalVersionIsMissing()
    {
        Assert.Equal("4.5.6", ApplicationVersion.Normalize(null, new Version(4, 5, 6, 7)));
    }

    [Fact]
    public void UnknownIsUsedWhenNoVersionMetadataExists()
    {
        Assert.Equal("unknown", ApplicationVersion.Normalize(null, null));
    }
}
