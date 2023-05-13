using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.IO.FileSystemTasks;
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

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;

    AbsolutePath OutputDirectory => RootDirectory / "output";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetPackageDirectory(Path.Combine(Solution.Directory, "packages"))
                .SetPlatform("x64")
                .SetRuntime("win-x64")
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            var parametersDict = new Dictionary<string, object>();
            var version = Environment.GetEnvironmentVariable("VERSION");
            if (!string.IsNullOrEmpty(version))
            {
                parametersDict.Add("Version", version!);                
            }
            
            DotNetBuild(s => s
                .SetProjectFile(Solution.Projects.First(x=>x.Name == "ImageViewer"))
                .SetConfiguration(Configuration)
                .SetPlatform("x64")
                .SetRuntime("win-x64")
                .SetOutputDirectory(OutputDirectory)
                .SetProperties(parametersDict)
                .SetAssemblyVersion(version)
                .EnableNoRestore());

            var sourceFile = Path.Combine(Solution.Directory,
                @"packages\freeimage-dotnet-core\4.3.6\runtimes\win7-x64\native\FreeImage.dll".Replace('\\',
                    Path.DirectorySeparatorChar));
            if (!File.Exists(sourceFile))
            {
                sourceFile = "~/.nuget/packages/freeimage-dotnet-core/4.3.6/runtimes/win7-x64/native/FreeImage.dll";
                if (!File.Exists(sourceFile))
                {
                    throw new Exception($"Can't find freeImage.dll in {sourceFile}");
                }
            }
            
            CopyFile(
                sourceFile, 
                Path.Combine(Solution.Directory, "output\\FreeImage.dll".Replace('\\', Path.DirectorySeparatorChar)), 
                FileExistsPolicy.Overwrite);
        });

}
