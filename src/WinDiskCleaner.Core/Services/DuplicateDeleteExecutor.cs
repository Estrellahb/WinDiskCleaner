using Microsoft.VisualBasic.FileIO;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class DuplicateDeleteExecutor
{
    private readonly string _recycleRoot;
    private readonly string _logPath;

    public DuplicateDeleteExecutor(string? recycleRoot = null, string? logPath = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Path.Combine(Path.GetTempPath(), "WinDiskCleaner");
        }

        _recycleRoot = recycleRoot ?? Path.Combine(appData, "WinDiskCleaner", "RecycleBin");
        _logPath = logPath ?? Path.Combine(appData, "WinDiskCleaner", "Logs", $"duplicate-delete-{DateTime.UtcNow:yyyyMMddHHmmss}.log");
    }

    public async Task<CleanResult> DeleteAsync(List<DuplicateGroup> groups, IEnumerable<string> selectedPaths, bool toRecycleBin = true, CancellationToken ct = default)
    {
        var result = new CleanResult();
        Directory.CreateDirectory(_recycleRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath) ?? _recycleRoot);

        var selected = selectedPaths.Select(NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();
            var selectedInGroup = group.Files.Where(file => selected.Contains(NormalizePath(file.Path))).ToList();
            if (selectedInGroup.Count == 0)
            {
                continue;
            }

            var remainingInGroup = group.Files.Where(file => !selected.Contains(NormalizePath(file.Path))).ToList();
            if (remainingInGroup.Count == 0)
            {
                foreach (var file in selectedInGroup)
                {
                    result.Errors.Add($"至少保留一份重复文件：{file.Path}");
                    result.Failed++;
                }

                continue;
            }

            var validationErrors = await ValidateGroupAsync(group, selectedInGroup, remainingInGroup, ct);
            if (validationErrors.Count > 0)
            {
                foreach (var error in validationErrors)
                {
                    result.Errors.Add(error);
                    result.Failed++;
                }

                continue;
            }

            foreach (var file in selectedInGroup)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (!File.Exists(file.Path))
                    {
                        result.Errors.Add($"路径不存在：{file.Path}");
                        result.Failed++;
                        continue;
                    }

                    if (toRecycleBin)
                    {
                        MoveToRecycleBin(file.Path);
                    }
                    else
                    {
                        File.Delete(file.Path);
                    }

                    result.Succeeded++;
                    result.FreedBytes += file.Size;
                    result.SucceededPaths.Add(file.Path);
                }
                catch (IOException)
                {
                    result.Errors.Add($"文件正在使用：{file.Path}");
                    result.Failed++;
                }
                catch (UnauthorizedAccessException)
                {
                    result.Errors.Add($"无权限删除：{file.Path}");
                    result.Failed++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{file.Path}：{ex.Message}");
                    result.Failed++;
                }
            }
        }

        await WriteLogAsync(result);
        return result;
    }

    private static async Task<List<string>> ValidateGroupAsync(DuplicateGroup group, List<DuplicateFileInfo> selectedInGroup, List<DuplicateFileInfo> remainingInGroup, CancellationToken ct)
    {
        var errors = new List<string>();
        foreach (var file in selectedInGroup.Concat(remainingInGroup))
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(file.Path))
            {
                errors.Add($"路径不存在：{file.Path}");
                continue;
            }

            var info = new FileInfo(file.Path);
            if (info.Length != file.Size)
            {
                errors.Add($"文件已变化：{file.Path}");
                continue;
            }

            try
            {
                var hash = await DuplicateFinder.ComputeSha256Async(file.Path, ct);
                if (!string.Equals(hash, group.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"文件已变化：{file.Path}");
                }
            }
            catch (IOException)
            {
                errors.Add($"文件正在使用：{file.Path}");
            }
            catch (UnauthorizedAccessException)
            {
                errors.Add($"无权限读取：{file.Path}");
            }
        }

        if (remainingInGroup.All(file => !File.Exists(file.Path)))
        {
            errors.Add("至少保留一份重复文件：保留项不存在");
        }

        return errors;
    }

    private void MoveToRecycleBin(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            return;
        }

        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetPath = Path.Combine(_recycleRoot, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}-{name}");
        File.Move(path, targetPath);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private async Task WriteLogAsync(CleanResult result)
    {
        var lines = new List<string>
        {
            $"Time: {DateTime.Now:O}",
            $"Succeeded: {result.Succeeded}",
            $"Failed: {result.Failed}",
            $"FreedBytes: {result.FreedBytes}"
        };
        lines.AddRange(result.Errors.Select(error => $"Error: {error}"));
        await File.WriteAllLinesAsync(_logPath, lines);
    }
}
