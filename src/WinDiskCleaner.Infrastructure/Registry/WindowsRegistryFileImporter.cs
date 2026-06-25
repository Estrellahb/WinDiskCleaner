using System.Diagnostics;
using WinDiskCleaner.Core.Interfaces;

namespace WinDiskCleaner.Infrastructure.Registry;

public class WindowsRegistryFileImporter : IRegistryFileImporter
{
    public async Task<bool> ImportAsync(string regFilePath, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return File.Exists(regFilePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "reg.exe",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add("import");
        startInfo.ArgumentList.Add(regFilePath);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        await process.WaitForExitAsync(ct);
        return process.ExitCode == 0;
    }
}
