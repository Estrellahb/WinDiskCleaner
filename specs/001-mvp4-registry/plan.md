# Implementation Plan: WinDiskCleaner MVP4 Registry Scan, Backup, and Restore

**Branch**: `mvp4-registry` | **Date**: 2026-06-25 | **Spec**: specs/001-mvp4-registry/spec.md

## Summary

Add a conservative registry tab that scans Windows uninstall registry entries, highlights invalid/orphaned entries, exports timestamped `.reg` backups, previews/restores `.reg` files with explicit confirmation, and logs registry operations. The implementation stays within the authorized MVP4 file scope and does not add automatic registry cleanup.

## Technical Context

**Language/Version**: C# / .NET 8  
**Primary Dependencies**: WPF, xUnit, Microsoft.Win32.Registry APIs on Windows  
**Storage**: Local `.reg` backup files and operation log text in local app data or selected backup directory  
**Testing**: xUnit Core tests using fixture-backed registry readers/export/import seams  
**Target Platform**: Windows desktop app; Linux CI/build cross-targets WPF with `EnableWindowsTargeting`  
**Project Type**: WPF desktop app with Core service layer and Infrastructure registry adapters  
**Safety Constraints**: No one-click registry cleanup, no automatic registry deletion, no system core branch scanning/modification, restore confirmation required in UI, log all registry operations

## Scope Boundaries

Allowed paths:
- src/WinDiskCleaner.Core/Models/
- src/WinDiskCleaner.Core/Interfaces/
- src/WinDiskCleaner.Core/Services/
- src/WinDiskCleaner.App/Views/
- src/WinDiskCleaner.App/ViewModels/
- src/WinDiskCleaner.Infrastructure/Registry/
- tests/WinDiskCleaner.Core.Tests/

Necessary shell wiring:
- src/WinDiskCleaner.App/MainWindow.xaml.cs may instantiate the new registry view for the existing fourth tab.

Excluded areas:
- Duplicate file detection
- Shortcut scanning
- Settings page behavior
- Installer/package work

## Design Decisions

### Decision: Core owns testable contracts; Infrastructure owns Windows registry access

Rationale: Existing tests reference Core only. Scanner and backup services can be tested through small registry-reader/exporter/importer abstractions in Core, while Infrastructure provides Windows-specific adapters.

Alternatives considered:
- Put scanner in Infrastructure only: rejected because Core tests would need platform registry access.
- Put raw Registry API calls directly in WPF code-behind: rejected because it is harder to test and mixes UI with registry operations.

### Decision: `.reg` restore is command-seam based with preview validation

Rationale: Real import is platform-specific and destructive. Tests should validate parsing, unsafe branch rejection, and import invocation through an injectable seam.

Alternatives considered:
- Direct registry writes from parsed `.reg`: rejected for MVP safety and scope.

### Decision: UI uses a code-behind/ViewModel hybrid consistent with current app

Rationale: Existing WPF tabs are code-behind heavy. A focused RegistryViewModel can keep filtering/sorting/history testable enough while avoiding a broad MVVM rewrite.

Alternatives considered:
- Full MVVM migration: rejected as outside MVP4 scope.

## Data Model

### InstalledSoftware

Fields:
- DisplayName: string
- DisplayVersion: string
- Publisher: string
- InstallLocation: string
- EstimatedSize: long bytes
- UninstallString: string
- RegistryPath: string
- IsValid: bool
- IsOrphan: bool

Validation:
- DisplayName must be non-empty and contain at least one non-control character.
- InstallLocation is orphaned only when non-empty and the directory does not exist.

### RegistryBackup

Fields:
- FilePath: string
- BranchName: string
- BackupTime: DateTime
- FileSize: long

Validation:
- File extension should be `.reg` for history and restore candidates.
- Timestamp is parsed from file metadata or timestamped file name.

### RegistryValueSet

Internal service input representing one uninstall entry. Fields: branch path, key name, and string/DWORD registry values.

### RegistryRestorePreview

Fields:
- FilePath
- Branches
- IsValid
- ErrorMessage

## Contracts

Public Core interfaces:

```csharp
public interface IRegistryScanner
{
    Task<List<InstalledSoftware>> ScanInstalledSoftwareAsync();
}

public interface IRegistryBackupService
{
    Task<string> BackupBranchAsync(string registryBranch, string outputDir);
    Task<bool> RestoreFromFileAsync(string regFilePath);
    List<RegistryBackup> GetBackupHistory(string backupDir);
}
```

Additional test seams:

```csharp
public interface IRegistryReader
{
    Task<List<RegistryValueSet>> ReadUninstallEntriesAsync(CancellationToken ct = default);
}

public interface IRegistryBranchExporter
{
    Task ExportBranchAsync(string registryBranch, string outputFilePath, CancellationToken ct = default);
}

public interface IRegistryFileImporter
{
    Task<bool> ImportAsync(string regFilePath, CancellationToken ct = default);
}
```

## Implementation Phases

1. Core models and interfaces.
2. RED tests for scanner parsing, orphan detection, invalid names.
3. Core scanner service using fixture reader seam.
4. RED tests for backup export/history/restore preview/import failure.
5. Core backup service with branch validation, timestamp naming, `.reg` parsing, safe import seam, logging.
6. Infrastructure Windows registry reader/exporter/importer adapters.
7. Registry ViewModel and WPF registry tab with software list, search/sort, backup, restore preview, confirmation, and log display.
8. MainWindow fourth tab wiring.
9. Focused tests, full test project, solution build, review, commit.

## Verification

Required commands:

```text
dotnet test tests/WinDiskCleaner.Core.Tests/WinDiskCleaner.Core.Tests.csproj --no-restore --filter Mvp4
dotnet test tests/WinDiskCleaner.Core.Tests/WinDiskCleaner.Core.Tests.csproj --no-restore
dotnet build WinDiskCleaner.sln --no-restore
```

Review command pattern:

```text
git diff --cached
git diff
```

## Safety Gates

- Restore rejects invalid `.reg` files.
- Restore rejects unsafe branches including HKLM\\SYSTEM and HKLM\\SAM.
- UI restore button requires confirmation before service call.
- No registry delete API is added.
- Backup file names include timestamp and never overwrite existing files.
- Registry operation logs include scan, backup, restore, rejection, and failure messages.
