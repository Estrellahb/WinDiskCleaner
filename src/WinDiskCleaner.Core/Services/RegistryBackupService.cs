using System.Text.RegularExpressions;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class RegistryBackupService : IRegistryBackupService
{
    private static readonly Regex BranchRegex = new(@"^\s*\[\-?(?<branch>[^\]]+)\]\s*$", RegexOptions.Compiled);
    private readonly IRegistryBranchExporter _exporter;
    private readonly IRegistryFileImporter _importer;
    private readonly string _logPath;
    private readonly string? _backupRoot;

    public RegistryBackupService(IRegistryBranchExporter exporter, IRegistryFileImporter importer, string? logPath = null, string? backupRoot = null)
    {
        _exporter = exporter;
        _importer = importer;
        _logPath = logPath ?? Path.Combine(GetDefaultAppDataRoot(), "Logs", $"registry-{DateTime.UtcNow:yyyyMMddHHmmss}.log");
        _backupRoot = backupRoot;
    }

    public async Task<string> BackupBranchAsync(string registryBranch, string outputDir)
    {
        if (!IsAllowedBranch(registryBranch))
        {
            await WriteLogAsync($"Backup rejected unsafe branch: {registryBranch}");
            throw new InvalidOperationException($"Unsafe registry branch is not allowed: {registryBranch}");
        }

        Directory.CreateDirectory(outputDir);
        var fileName = $"{SanitizeBranchName(registryBranch)}_{DateTime.Now:yyyyMMdd_HHmmss}.reg";
        var filePath = Path.Combine(outputDir, fileName);
        var suffix = 1;
        while (File.Exists(filePath))
        {
            filePath = Path.Combine(outputDir, $"{SanitizeBranchName(registryBranch)}_{DateTime.Now:yyyyMMdd_HHmmss}_{suffix}.reg");
            suffix++;
        }

        await _exporter.ExportBranchAsync(registryBranch, filePath);
        await WriteLogAsync($"Backup created: {registryBranch} -> {filePath}");
        return filePath;
    }

    public async Task<bool> RestoreFromFileAsync(string regFilePath)
    {
        var preview = await PreviewRestoreFileAsync(regFilePath);
        if (!preview.IsValid)
        {
            await WriteLogAsync($"Restore rejected: {regFilePath}; {preview.ErrorMessage}");
            return false;
        }

        var result = await _importer.ImportAsync(regFilePath);
        await WriteLogAsync($"Restore {(result ? "completed" : "failed")}: {regFilePath}; branches={string.Join(";", preview.Branches)}");
        return result;
    }

    public List<RegistryBackup> GetBackupHistory(string backupDir)
    {
        if (!Directory.Exists(backupDir))
        {
            return new List<RegistryBackup>();
        }

        return Directory.EnumerateFiles(backupDir, "*.reg")
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new RegistryBackup
                {
                    FilePath = path,
                    BranchName = TryReadBranches(path).FirstOrDefault() ?? InferBranchFromFileName(path),
                    BackupTime = info.LastWriteTime,
                    FileSize = info.Length
                };
            })
            .OrderByDescending(backup => backup.BackupTime)
            .ToList();
    }

    public bool DeleteBackup(string filePath)
    {
        if (!File.Exists(filePath) || !string.Equals(Path.GetExtension(filePath), ".reg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsUnderBackupRoot(filePath))
        {
            _ = WriteLogAsync($"Backup delete rejected outside backup root: {filePath}");
            return false;
        }

        File.Delete(filePath);
        _ = WriteLogAsync($"Backup deleted: {filePath}");
        return true;
    }

    public async Task<RegistryRestorePreview> PreviewRestoreFileAsync(string regFilePath)
    {
        if (!File.Exists(regFilePath) || !string.Equals(Path.GetExtension(regFilePath), ".reg", StringComparison.OrdinalIgnoreCase))
        {
            return InvalidPreview(regFilePath, "File does not exist or is not a .reg file.");
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(regFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return InvalidPreview(regFilePath, ex.Message);
        }

        if (!HasValidHeader(content))
        {
            return InvalidPreview(regFilePath, "Missing .reg header.");
        }

        if (ContainsDeletionSection(content))
        {
            return InvalidPreview(regFilePath, "Destructive registry deletion sections are not allowed.");
        }

        var branches = ExtractBranches(content).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (branches.Count == 0)
        {
            return InvalidPreview(regFilePath, "No registry branches found.");
        }

        var unsafeBranch = branches.FirstOrDefault(branch => !IsAllowedBranch(branch));
        if (unsafeBranch is not null)
        {
            return InvalidPreview(regFilePath, $"Unsafe registry branch is not allowed: {unsafeBranch}");
        }

        return new RegistryRestorePreview
        {
            FilePath = regFilePath,
            Branches = branches,
            IsValid = true
        };
    }

    public static bool IsAllowedBranch(string registryBranch)
    {
        var normalized = NormalizeBranch(registryBranch);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var allowedPrefixes = RegistryScanner.UninstallBranches;
        return allowedPrefixes.Any(prefix => normalized.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(prefix + @"\", StringComparison.OrdinalIgnoreCase));
    }

    private static RegistryRestorePreview InvalidPreview(string filePath, string error)
    {
        return new RegistryRestorePreview
        {
            FilePath = filePath,
            IsValid = false,
            ErrorMessage = error
        };
    }

    private static List<string> TryReadBranches(string filePath)
    {
        try
        {
            return ExtractBranches(File.ReadAllText(filePath)).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static bool HasValidHeader(string content)
    {
        var firstMeaningfulLine = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
        return string.Equals(firstMeaningfulLine, "Windows Registry Editor Version 5.00", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstMeaningfulLine, "REGEDIT4", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsDeletionSection(string content)
    {
        return content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Any(line => line.TrimStart().StartsWith("[-", StringComparison.Ordinal));
    }

    private bool IsUnderBackupRoot(string filePath)
    {
        if (string.IsNullOrWhiteSpace(_backupRoot))
        {
            return true;
        }

        var root = Path.GetFullPath(_backupRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(filePath);
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ExtractBranches(string content)
    {
        foreach (var line in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var match = BranchRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            yield return NormalizeBranch(match.Groups["branch"].Value);
        }
    }

    private static string NormalizeBranch(string branch)
    {
        var normalized = branch.Trim().TrimStart('-').Trim().Replace("/", @"\");
        return normalized
            .Replace("HKLM\\", "HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase)
            .Replace("HKCU\\", "HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase);
    }

    private static string InferBranchFromFileName(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var timestampIndex = Regex.Match(name, @"_\d{8}_\d{6}").Index;
        if (timestampIndex > 0)
        {
            name = name[..timestampIndex];
        }

        return name.Replace('_', '\\');
    }

    private static string SanitizeBranchName(string registryBranch)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }).ToHashSet();
        return string.Concat(registryBranch.Select(ch => invalid.Contains(ch) ? '_' : ch));
    }

    private async Task WriteLogAsync(string message)
    {
        var directory = Path.GetDirectoryName(_logPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.AppendAllTextAsync(_logPath, $"{DateTime.Now:O} {message}{Environment.NewLine}");
    }

    private static string GetDefaultAppDataRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Path.Combine(Path.GetTempPath(), "WinDiskCleaner");
        }

        return Path.Combine(appData, "WinDiskCleaner");
    }
}
