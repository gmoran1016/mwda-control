namespace Mwda.Control.Tests.Build;

public sealed class PublishConfigurationTests
{
    [Fact]
    public void ProjectSetsThePublicExecutableName()
    {
        var project = ReadRepositoryFile("src", "Mwda.Control", "Mwda.Control.csproj");

        Assert.Contains(
            "<AssemblyName>MWDA-Control</AssemblyName>",
            project,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublishScriptAndWorkflowUseThePublicExecutableName()
    {
        var publishScript = ReadRepositoryFile("publish.ps1");
        var workflow = ReadRepositoryFile(".github", "workflows", "release.yml");

        Assert.Contains("MWDA-Control.exe", publishScript, StringComparison.Ordinal);
        Assert.Contains("MWDA-Control.exe", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void MasterPushesRunThePublishJob()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "release.yml");

        Assert.Contains(
            "if: github.event_name == 'workflow_dispatch' || github.event_name == 'push'",
            workflow,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VersionChangesTriggerReleaseCreation()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "release.yml");

        Assert.Contains("name: Detect version change", workflow, StringComparison.Ordinal);
        Assert.Contains("VersionPrefix", workflow, StringComparison.Ordinal);
        Assert.Contains("needs: [test, version]", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "needs.version.outputs.should_release == 'true'",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains("gh release create", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void AppResourcesReferenceTheRenamedAssembly()
    {
        var app = ReadRepositoryFile("src", "Mwda.Control", "App.xaml");

        Assert.Contains(
            "Source=\"/MWDA-Control;component/Resources/Theme.xaml\"",
            app,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "/Mwda.Control;component/",
            app,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "MWDA.Control.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory!.FullName, .. relativePath]));
    }
}
