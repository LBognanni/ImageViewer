using System.Reflection;

namespace ImageViewer;

public static class AppVersion
{
    private const string Unstamped = "0.0.0";

    /// <summary>
    /// The version as it was tagged, including any suffix such as -rc1
    /// </summary>
    public static string? Running =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    public static string Describe(string? version) =>
        string.IsNullOrEmpty(version) || version == Unstamped ? "Development version" : $"Version {version}";
}
