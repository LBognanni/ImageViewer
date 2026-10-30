using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[TypeConverter(typeof(TypeConverter<Configuration>))]
public class Configuration : Enumeration
{
    public static Configuration Release = new() { Value = nameof(Release) };

    public static implicit operator string(Configuration configuration)
    {
        return configuration.Value;
    }
}

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Publish);

    [Parameter("Configuration to build - Default is 'Release'")]
    readonly Configuration Configuration = Configuration.Release;

    [Parameter("Runtime to publish for, e.g. win-x64 - Default is this machine's. Native AOT can't cross-compile between operating systems.")]
    readonly string Runtime = GetPortableRuntime();

    [Solution] readonly Solution Solution;

    AbsolutePath OutputDirectory => RootDirectory / "output";

    Target Clean => _ => _
        .Executes(() =>
        {
            OutputDirectory.CreateOrCleanDirectory();
        });

    Target Test => _ => _
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution.Projects.First(x => x.Name == "ImageViewer.Tests")));
        });

    Target Publish => _ => _
        .After(Clean)
        .Executes(() =>
        {
            var version = Environment.GetEnvironmentVariable("VERSION");

            DotNetPublish(s => s
                .SetProject(Solution.Projects.First(x => x.Name == "ImageViewer"))
                .SetConfiguration(Configuration)
                .SetRuntime(Runtime)
                .SetOutput(OutputDirectory)
                .When(_ => !string.IsNullOrEmpty(version), o => o
                    .SetVersion(version)
                    .SetAssemblyVersion(version)
                    .SetFileVersion(version))
                .When(_ => NeedsGccToLink(), o => o
                    .SetProperty("CppCompilerAndLinker", "gcc")));
        });

    /// <summary>
    /// RuntimeInformation.RuntimeIdentifier can be specific to a distro, like ubuntu.24.04-x64
    /// </summary>
    static string GetPortableRuntime()
    {
        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        return $"{os}-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}";
    }

    /// <summary>
    /// Native AOT links with clang by default
    /// </summary>
    static bool NeedsGccToLink() =>
        OperatingSystem.IsLinux() && !File.Exists("/usr/bin/clang") && File.Exists("/usr/bin/gcc");
}
