using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Interfaces;

public interface IRegistryScanner
{
    Task<List<InstalledSoftware>> ScanInstalledSoftwareAsync();
}

public interface IRegistryReader
{
    Task<List<RegistryValueSet>> ReadUninstallEntriesAsync(CancellationToken ct = default);
}
