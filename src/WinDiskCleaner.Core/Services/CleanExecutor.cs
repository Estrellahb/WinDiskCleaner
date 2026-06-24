using Microsoft.VisualBasic.FileIO;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class CleanExecutor : ICleanExecutor
{
    private readonly string _recycleRoot;
    private readonly string _logPath;

    public CleanExecutor(string? recycleRoot = null, string? logPath = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Path.Combine(Path.GetTempPath(), "WinDiskCleaner");
        }

        _recycleRoot = recycleRoot ?? Path.Combine(appData, "WinDiskCleaner", "RecycleBin");
        _logPath = logPath ?? Path.Combine(appData, "WinDiskCleaner", "Logs", $"clean-{DateTime.UtcNow:yyyyMMddHHmmss}.log");
    }

    public async Task<CleanResult> ExecuteAsync(List<AISuggestionItem> items, bool toRecycleBin = true)
    {
        var result = new CleanResult();
        Directory.CreateDirectory(_recycleRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath) ?? _recycleRoot);

        var riskClassifier = new RiskClassifier();
        foreach (var item in items.Where(x => string.Equals(x.Action, "delete", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                if (!File.Exists(item.Path) && !Directory.Exists(item.Path))
                {
                    result.Errors.Add($"路径不存在：{item.Path}");
                    result.Failed++;
                    continue;
                }

                var isDirectory = Directory.Exists(item.Path);
                if (riskClassifier.Classify(item.Path, isDirectory) != RiskLevel.Low)
                {
                    result.Errors.Add($"跳过非 Low 风险项：{item.Path}");
                    result.Failed++;
                    continue;
                }

                if (toRecycleBin)
                {
                    MoveToRecycleBin(item.Path);
                }
                else
                {
                    DeletePermanently(item.Path);
                }

                result.Succeeded++;
                result.FreedBytes += item.EstimatedSpace;
                result.SucceededPaths.Add(item.Path);
            }
            catch (IOException)
            {
                result.Errors.Add($"文件正在使用：{item.Path}");
                result.Failed++;
            }
            catch (UnauthorizedAccessException)
            {
                result.Errors.Add($"无权限删除：{item.Path}");
                result.Failed++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{item.Path}：{ex.Message}");
                result.Failed++;
            }
        }

        await WriteLogAsync(result);
        return result;
    }

    private void MoveToRecycleBin(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            if (Directory.Exists(path))
            {
                FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            else
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }

            return;
        }

        MoveToLocalRecycleArea(path);
    }

    private static void DeletePermanently(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
        else
        {
            File.Delete(path);
        }
    }

    private void MoveToLocalRecycleArea(string sourcePath)
    {
        var name = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetPath = Path.Combine(_recycleRoot, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}-{name}");
        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, targetPath);
        }
        else
        {
            File.Move(sourcePath, targetPath);
        }
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
