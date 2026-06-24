using System.Security.Cryptography;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class DuplicateFinder : IDuplicateFinder
{
    private const int PartialHashBytes = 1024 * 1024;
    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".db", ".vhd", ".vhdx", ".qcow2"
    };

    public async Task<List<DuplicateGroup>> FindDuplicatesAsync(
        List<string> scanPaths,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var files = EnumerateCandidateFiles(scanPaths, ct).ToList();
        var groups = new List<DuplicateGroup>();
        var scanned = 0;

        var sameSizeGroups = files
            .GroupBy(file => file.Length)
            .Where(group => group.Key > 0 && group.Count() > 1);

        foreach (var sizeGroup in sameSizeGroups)
        {
            ct.ThrowIfCancellationRequested();
            var partialGroups = new Dictionary<string, List<FileInfo>>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in sizeGroup)
            {
                ct.ThrowIfCancellationRequested();
                var partialHash = await ComputeHashAsync(file.FullName, PartialHashBytes, ct);
                if (!partialGroups.TryGetValue(partialHash, out var bucket))
                {
                    bucket = new List<FileInfo>();
                    partialGroups[partialHash] = bucket;
                }

                bucket.Add(file);
                scanned++;
                progress?.Report(new ScanProgress { FilesScanned = scanned, CurrentPath = file.FullName });
            }

            foreach (var partialGroup in partialGroups.Values.Where(group => group.Count > 1))
            {
                ct.ThrowIfCancellationRequested();
                var shaGroups = new Dictionary<string, List<FileInfo>>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in partialGroup)
                {
                    var hash = await ComputeHashAsync(file.FullName, null, ct);
                    if (!shaGroups.TryGetValue(hash, out var bucket))
                    {
                        bucket = new List<FileInfo>();
                        shaGroups[hash] = bucket;
                    }

                    bucket.Add(file);
                }

                foreach (var duplicateSet in shaGroups.Where(pair => pair.Value.Count > 1))
                {
                    groups.Add(CreateDuplicateGroup(duplicateSet.Key, duplicateSet.Value));
                }
            }
        }

        return groups.OrderByDescending(group => group.WastedSpace).ToList();
    }

    private static IEnumerable<FileInfo> EnumerateCandidateFiles(IEnumerable<string> scanPaths, CancellationToken ct)
    {
        foreach (var scanPath in scanPaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            if (!Directory.Exists(scanPath) || IsExcludedPath(scanPath))
            {
                continue;
            }

            var pending = new Stack<string>();
            pending.Push(scanPath);
            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var directory = pending.Pop();
                if (IsExcludedPath(directory))
                {
                    continue;
                }

                IEnumerable<string> childDirectories;
                try
                {
                    childDirectories = Directory.EnumerateDirectories(directory).ToList();
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (var child in childDirectories)
                {
                    pending.Push(child);
                }

                IEnumerable<string> filePaths;
                try
                {
                    filePaths = Directory.EnumerateFiles(directory).ToList();
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (var filePath in filePaths)
                {
                    ct.ThrowIfCancellationRequested();
                    if (IsExcludedFile(filePath))
                    {
                        continue;
                    }

                    FileInfo file;
                    try
                    {
                        file = new FileInfo(filePath);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }
                    catch (IOException)
                    {
                        continue;
                    }

                    if (file.Exists)
                    {
                        yield return file;
                    }
                }
            }
        }
    }

    private static bool IsExcludedFile(string filePath)
    {
        return ExcludedExtensions.Contains(Path.GetExtension(filePath)) || IsExcludedPath(filePath);
    }

    private static bool IsExcludedPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return ContainsSegment(normalized, "Windows")
            || ContainsSegment(normalized, "Program Files")
            || ContainsSegment(normalized, "Program Files (x86)")
            || ContainsSegment(normalized, "ProgramData");
    }

    private static bool ContainsSegment(string normalizedPath, string segment)
    {
        return normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(part => string.Equals(part, segment, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> ComputeHashAsync(string path, int? maxBytes, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var sha = SHA256.Create();
        if (maxBytes is null)
        {
            var fullHash = await sha.ComputeHashAsync(stream, ct);
            return Convert.ToHexString(fullHash).ToLowerInvariant();
        }

        var remaining = maxBytes.Value;
        var buffer = new byte[Math.Min(81920, remaining)];
        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), ct);
            if (read == 0)
            {
                break;
            }

            sha.TransformBlock(buffer, 0, read, null, 0);
            remaining -= read;
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    private static DuplicateGroup CreateDuplicateGroup(string hash, List<FileInfo> files)
    {
        var ordered = files.OrderByDescending(file => file.LastWriteTimeUtc).ThenBy(file => file.FullName).ToList();
        var recommended = ordered.First();
        var duplicateFiles = ordered.Select(file => new DuplicateFileInfo
        {
            Path = file.FullName,
            Size = file.Length,
            LastModified = file.LastWriteTimeUtc,
            IsRecommended = string.Equals(file.FullName, recommended.FullName, StringComparison.OrdinalIgnoreCase),
            Selected = !string.Equals(file.FullName, recommended.FullName, StringComparison.OrdinalIgnoreCase)
        }).ToList();
        var size = duplicateFiles.First().Size;

        return new DuplicateGroup
        {
            Hash = hash,
            Files = duplicateFiles,
            TotalSize = duplicateFiles.Sum(file => file.Size),
            WastedSpace = Math.Max(0, duplicateFiles.Count - 1) * size
        };
    }
}
