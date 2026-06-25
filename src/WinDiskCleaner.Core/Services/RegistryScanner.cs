using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class RegistryScanner : IRegistryScanner
{
    public static readonly string[] UninstallBranches =
    {
        @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Uninstall",
        @"HKEY_LOCAL_MACHINE\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    private readonly IRegistryReader _reader;

    public RegistryScanner(IRegistryReader reader)
    {
        _reader = reader;
    }

    public async Task<List<InstalledSoftware>> ScanInstalledSoftwareAsync()
    {
        var entries = await _reader.ReadUninstallEntriesAsync();
        return entries.Select(ParseEntry)
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.RegistryPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static InstalledSoftware ParseEntry(RegistryValueSet entry)
    {
        var displayName = GetString(entry, "DisplayName").Trim();
        var installLocation = GetString(entry, "InstallLocation").Trim();
        var registryPath = string.IsNullOrWhiteSpace(entry.KeyName)
            ? entry.BranchPath
            : $"{entry.BranchPath.TrimEnd('\\')}\\{entry.KeyName}";

        return new InstalledSoftware
        {
            DisplayName = displayName,
            DisplayVersion = GetString(entry, "DisplayVersion").Trim(),
            Publisher = GetString(entry, "Publisher").Trim(),
            InstallLocation = installLocation,
            EstimatedSize = GetEstimatedSizeBytes(entry),
            UninstallString = GetString(entry, "UninstallString").Trim(),
            RegistryPath = registryPath,
            IsValid = IsValidDisplayName(displayName),
            IsOrphan = !string.IsNullOrWhiteSpace(installLocation) && !Directory.Exists(installLocation)
        };
    }

    private static string GetString(RegistryValueSet entry, string name)
    {
        if (!entry.Values.TryGetValue(name, out var value) || value is null)
        {
            return string.Empty;
        }

        return Convert.ToString(value) ?? string.Empty;
    }

    private static long GetEstimatedSizeBytes(RegistryValueSet entry)
    {
        if (!entry.Values.TryGetValue("EstimatedSize", out var value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            int intValue when intValue > 0 => intValue * 1024L,
            long longValue when longValue > 0 => longValue * 1024L,
            string text when long.TryParse(text, out var parsed) && parsed > 0 => parsed * 1024L,
            _ => 0
        };
    }

    private static bool IsValidDisplayName(string displayName)
    {
        return !string.IsNullOrWhiteSpace(displayName)
            && displayName.Any(character => !char.IsControl(character) && !char.IsWhiteSpace(character));
    }
}
