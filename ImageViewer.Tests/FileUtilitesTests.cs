using System.Collections.Generic;
using NUnit.Framework;

namespace ImageViewer.Tests;

public class FileUtilitesTests
{
    [TestCaseSource(nameof(GetSplitCases))]
    public void SplitCommand_WorksAsExpected(string command, string expectedExe, string expectedParams)
    {
        var (exe, parameters) = FileUtilites.SplitCommand(command);
        Assert.That(exe, Is.EqualTo(expectedExe));
        Assert.That(parameters, Is.EqualTo(expectedParams));
    }

    private static IEnumerable<string[]> GetSplitCases()
    {
        yield return new[]
            { @"""c:\program files\program.exe"" --param {0}", @"c:\program files\program.exe", "--param {0}" };
        yield return new[]
        {
            @"""c:\program files\some program\program.exe"" --param {0}", @"c:\program files\some program\program.exe",
            "--param {0}"
        };
        yield return new[]
            { @"c:\program_files\program.exe --param {0}", @"c:\program_files\program.exe", "--param {0}" };
        yield return new[]
            { @"program do {0}", @"program", "do {0}" };
    }
}