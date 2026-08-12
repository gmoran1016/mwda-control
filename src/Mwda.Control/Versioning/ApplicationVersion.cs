using System.Reflection;

namespace Mwda.Control.Versioning;

public static class ApplicationVersion
{
    public static string Current => Normalize(
        typeof(ApplicationVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion,
        typeof(ApplicationVersion).Assembly.GetName().Version);

    public static string Normalize(
        string? informationalVersion,
        Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var withoutBuildMetadata = informationalVersion.Split('+', 2)[0];
            if (!string.IsNullOrWhiteSpace(withoutBuildMetadata))
            {
                return withoutBuildMetadata;
            }
        }

        return assemblyVersion?.ToString(3) ?? "unknown";
    }
}
