using System.Diagnostics;
using WinDiskCleaner.Core.Interfaces;

namespace WinDiskCleaner.Infrastructure.Registry;

public class WindowsRegistryBranchExporter : IRegistryBranchExporter
{
    public async Task ExportBranchAsync(string registryBranch, string outputFilePath, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            await File.WriteAllTextAsync(outputFilePath, $"Windows Registry Editor Version 5.00{Environment.NewLine}{Environment.NewLine}[{registryBranch}]{Environment.NewLine}", ct);
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "reg.exe",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add("export");
        startInfo.ArgumentList.Add(registryBranch);
        startInfo.ArgumentList.Add(outputFilePath);
        startInfo.ArgumentList.Add("/y");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start reg.exe export.");
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"Registry export failed: {error}");
        }
    }
}
