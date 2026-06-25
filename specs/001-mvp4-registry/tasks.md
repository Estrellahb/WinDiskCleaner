# Tasks: WinDiskCleaner MVP4 Registry Scan, Backup, and Restore

**Input**: specs/001-mvp4-registry/spec.md and plan.md
**Required format**: checkbox, task ID, optional [P], optional story label, exact file path

## Phase 1: Setup

- [x] T001 Confirm branch `mvp4-registry` from `main`
- [x] T002 Create Spec Kit artifacts in specs/001-mvp4-registry/spec.md, specs/001-mvp4-registry/plan.md, specs/001-mvp4-registry/tasks.md, specs/001-mvp4-registry/checklists/requirements.md

## Phase 2: Foundational Core Contracts

- [ ] T003 [P] Add InstalledSoftware model in src/WinDiskCleaner.Core/Models/InstalledSoftware.cs
- [ ] T004 [P] Add RegistryBackup and RegistryRestorePreview models in src/WinDiskCleaner.Core/Models/RegistryBackup.cs and src/WinDiskCleaner.Core/Models/RegistryRestorePreview.cs
- [ ] T005 [P] Add registry scanner/backup interfaces and test seams in src/WinDiskCleaner.Core/Interfaces/IRegistryScanner.cs and src/WinDiskCleaner.Core/Interfaces/IRegistryBackupService.cs

## Phase 3: User Story 1 - Registry uninstall scan (P1)

- [ ] T006 [P] [US1] Add scanner parsing/orphan/invalid RED tests in tests/WinDiskCleaner.Core.Tests/Mvp4RegistryScannerTests.cs
- [ ] T007 [US1] Implement RegistryScanner service in src/WinDiskCleaner.Core/Services/RegistryScanner.cs
- [ ] T008 [US1] Add Windows registry reader adapter in src/WinDiskCleaner.Infrastructure/Registry/WindowsRegistryReader.cs

## Phase 4: User Story 2 - Software list display/search/sort (P2)

- [ ] T009 [P] [US2] Add registry view model filtering/sorting tests where practical in tests/WinDiskCleaner.Core.Tests/Mvp4RegistryViewModelContractTests.cs
- [ ] T010 [US2] Implement RegistryViewModel in src/WinDiskCleaner.App/ViewModels/RegistryViewModel.cs
- [ ] T011 [US2] Create RegistryTab XAML and code-behind in src/WinDiskCleaner.App/Views/RegistryTab.xaml and src/WinDiskCleaner.App/Views/RegistryTab.xaml.cs
- [ ] T012 [US2] Wire MainWindow fourth tab to RegistryTab in src/WinDiskCleaner.App/MainWindow.xaml.cs

## Phase 5: User Story 3 - Registry backup (P3)

- [ ] T013 [P] [US3] Add backup export/history/delete RED tests in tests/WinDiskCleaner.Core.Tests/Mvp4RegistryBackupTests.cs
- [ ] T014 [US3] Implement RegistryBackupService backup/history/delete behavior in src/WinDiskCleaner.Core/Services/RegistryBackupService.cs
- [ ] T015 [US3] Add Windows branch exporter in src/WinDiskCleaner.Infrastructure/Registry/WindowsRegistryBranchExporter.cs
- [ ] T016 [US3] Wire backup branch selection/history UI in src/WinDiskCleaner.App/Views/RegistryTab.xaml.cs

## Phase 6: User Story 4 - Restore with preview and confirmation (P4)

- [ ] T017 [P] [US4] Add restore preview/import invalid-file/unsafe-branch RED tests in tests/WinDiskCleaner.Core.Tests/Mvp4RegistryRestoreTests.cs
- [ ] T018 [US4] Implement restore preview/import safety in src/WinDiskCleaner.Core/Services/RegistryBackupService.cs
- [ ] T019 [US4] Add Windows .reg importer in src/WinDiskCleaner.Infrastructure/Registry/WindowsRegistryFileImporter.cs
- [ ] T020 [US4] Wire .reg chooser, preview, confirmation, restore log UI in src/WinDiskCleaner.App/Views/RegistryTab.xaml.cs

## Phase 7: Polish & Verification

- [ ] T021 Run focused MVP4 tests with `dotnet test tests/WinDiskCleaner.Core.Tests/WinDiskCleaner.Core.Tests.csproj --no-restore --filter Mvp4`
- [ ] T022 Run full Core tests with `dotnet test tests/WinDiskCleaner.Core.Tests/WinDiskCleaner.Core.Tests.csproj --no-restore`
- [ ] T023 Run solution build with `dotnet build WinDiskCleaner.sln --no-restore`
- [ ] T024 Perform review, fix blocking findings, and commit verified MVP4 changes

## Dependencies

- US1 depends on foundational contracts.
- US2 depends on US1 models/service outputs.
- US3 depends on foundational backup contracts.
- US4 depends on backup service and restore preview model.
- Verification depends on all selected MVP4 implementation tasks.

## Independent Test Criteria

- US1: scanner parses uninstall fixture entries and marks orphan/invalid states.
- US2: view model filters/sorts software and exposes status text without registry mutation.
- US3: backup service creates timestamped standard `.reg` files and lists history.
- US4: restore service previews branches, rejects unsafe/invalid files, and invokes importer only for valid confirmed service calls.
