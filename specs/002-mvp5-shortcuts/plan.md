# Implementation Plan: MVP5 快捷方式检测与修复

**Branch**: `mvp5-shortcuts` | **Date**: 2026-06-25 | **Spec**: `specs/002-mvp5-shortcuts/spec.md`

## Summary

新增快捷方式检测与修复能力：在 Core 层提供 `.lnk` 扫描、目标解析、失效判断、删除和报告导出服务；在 App 层新增 Tab5 快捷方式页面，支持扫描、搜索、排序、选择、确认删除和报告导出。

## Technical Context

- Language: C# / .NET 8
- UI: WPF
- Tests: xUnit in `tests/WinDiskCleaner.Core.Tests/`
- Allowed production scope:
  - `src/WinDiskCleaner.Core/Models/`
  - `src/WinDiskCleaner.Core/Interfaces/`
  - `src/WinDiskCleaner.Core/Services/`
  - `src/WinDiskCleaner.App/Views/`
  - `src/WinDiskCleaner.App/ViewModels/`
- Integration note: `MainWindow.xaml.cs` may need to instantiate and attach Tab5.
- No Infrastructure layer changes in MVP5.

## Constitution Check

- TDD required for Core behavior.
- Deletion must be safe and narrowly scoped to `.lnk` files.
- No registry, duplicate-file, infrastructure, or installer changes.
- All tests and solution build must pass before commit.

## Project Structure

```text
specs/002-mvp5-shortcuts/
  spec.md
  plan.md
  tasks.md
  checklists/requirements.md

src/WinDiskCleaner.Core/
  Models/ShortcutItem.cs
  Models/ShortcutDeleteResult.cs
  Interfaces/IShortcutScanner.cs
  Interfaces/IShortcutDeleteService.cs
  Interfaces/IShortcutReportExporter.cs
  Services/ShortcutScanner.cs
  Services/ShortcutDeleteService.cs
  Services/ShortcutReportExporter.cs

src/WinDiskCleaner.App/
  ViewModels/ShortcutViewModel.cs
  Views/ShortcutTab.xaml
  Views/ShortcutTab.xaml.cs

tests/WinDiskCleaner.Core.Tests/
  Mvp5ShortcutTests.cs
```

## Implementation Strategy

Core scanning should be testable without Windows Shell COM dependencies by isolating shortcut parsing behind an injectable parser seam. Production scanner can parse `.lnk` using a conservative text fallback for tests and Shell COM where available if needed, but MVP tests should target service behavior through test-controlled parser input.

Directory discovery should have a default provider for the five required locations and allow test injection of explicit roots. Scanning should recursively enumerate `.lnk` files, catch `UnauthorizedAccessException`/`IOException` per directory, and report progress.

Deletion service should accept `ShortcutItem` candidates, filter to selected invalid items, re-check `.lnk` extension and current file existence, delete only `ShortcutPath`, record per-item failures, and never use `TargetPath` for deletion.

Report exporter should produce CSV with escaped fields and HTML with encoded content.

UI should use a ViewModel with observable collections and computed statistics, while code-behind can handle button events and confirmation dialogs consistent with the existing WPF style.

## Verification

Focused MVP5 tests:

```bash
dotnet test tests/WinDiskCleaner.Core.Tests/WinDiskCleaner.Core.Tests.csproj --no-restore --filter Mvp5
```

Full Core tests:

```bash
dotnet test tests/WinDiskCleaner.Core.Tests/WinDiskCleaner.Core.Tests.csproj --no-restore
```

Solution build:

```bash
dotnet build WinDiskCleaner.sln --no-restore
```
