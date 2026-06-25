using System.Runtime.Versioning;
using Microsoft.Win32;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.Infrastructure.Registry;

public class WindowsRegistryReader : IRegistryReader
{
    public Task<List<RegistryValueSet>> ReadUninstallEntriesAsync(CancellationToken ct = default)
    {
        var entries = new List<RegistryValueSet>();
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(entries);
        }

        foreach (var branch in RegistryScanner.UninstallBranches)
        {
            ct.ThrowIfCancellationRequested();
            entries.AddRange(ReadBranch(branch));
        }

        return Task.FromResult(entries);
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<RegistryValueSet> ReadBranch(string branchPath)
    {
        var (hive, subKeyPath) = SplitHive(branchPath);
        using var baseKey = hive.OpenSubKey(subKeyPath);
        if (baseKey is null)
        {
            yield break;
        }

        foreach (var keyName in baseKey.GetSubKeyNames())
        {
            using var subKey = baseKey.OpenSubKey(keyName);
            if (subKey is null)
            {
                continue;
            }

            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var valueName in subKey.GetValueNames())
            {
                values[valueName] = subKey.GetValue(valueName);
            }

            yield return new RegistryValueSet
            {
                BranchPath = branchPath,
                KeyName = keyName,
                Values = values
            };
        }
    }

    [SupportedOSPlatform("windows")]
    private static (RegistryKey Hive, string SubKeyPath) SplitHive(string branchPath)
    {
        const string hklm = @"HKEY_LOCAL_MACHINE\";
        const string hkcu = @"HKEY_CURRENT_USER\";
        if (branchPath.StartsWith(hklm, StringComparison.OrdinalIgnoreCase))
        {
            return (Microsoft.Win32.Registry.LocalMachine, branchPath[hklm.Length..]);
        }

        if (branchPath.StartsWith(hkcu, StringComparison.OrdinalIgnoreCase))
        {
            return (Microsoft.Win32.Registry.CurrentUser, branchPath[hkcu.Length..]);
        }

        throw new InvalidOperationException($"Unsupported registry hive: {branchPath}");
    }
}
