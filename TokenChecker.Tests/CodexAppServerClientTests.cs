using System.Reflection;
using TokenChecker.Services;

namespace TokenChecker.Tests;

public sealed class CodexAppServerClientTests
{
    [Fact]
    public void ResolveExecutable_PrefersNvmSymlinkOverPath()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("TokenCheckerTests-");
        var nvmDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.FullName, "nvm"));
        var pathDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.FullName, "path"));
        var originalNvmSymlink = Environment.GetEnvironmentVariable("NVM_SYMLINK");
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            var nvmCommandPath = Path.Combine(nvmDirectory.FullName, "codex.cmd");
            File.WriteAllText(nvmCommandPath, "@echo off");
            File.WriteAllText(Path.Combine(pathDirectory.FullName, "codex.exe"), string.Empty);
            Environment.SetEnvironmentVariable("NVM_SYMLINK", nvmDirectory.FullName);
            Environment.SetEnvironmentVariable("PATH", pathDirectory.FullName);

            var cl = new CodexAppServerClient();
            var method = typeof(CodexAppServerClient).GetMethod(
                "ResolveExecutable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var result = method.Invoke(cl, null);

            Assert.Equal(nvmCommandPath, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NVM_SYMLINK", originalNvmSymlink);
            Environment.SetEnvironmentVariable("PATH", originalPath);
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ResolveExecutable_SkipsUnsupportedWhereResult()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("TokenCheckerTests-");
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            var extensionlessPath = Path.Combine(tempDirectory.FullName, "codex");
            var commandPath = Path.Combine(tempDirectory.FullName, "codex.cmd");
            File.WriteAllText(extensionlessPath, string.Empty);
            File.WriteAllText(commandPath, "@echo off");
            Environment.SetEnvironmentVariable("PATH", tempDirectory.FullName);

            var cl = new CodexAppServerClient([]);
            var method = typeof(CodexAppServerClient).GetMethod(
                "ResolveExecutable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var result = method.Invoke(cl, null);

            Assert.Equal(commandPath, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            tempDirectory.Delete(recursive: true);
        }
    }
}
