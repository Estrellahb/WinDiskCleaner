# Feature Specification: WinDiskCleaner MVP4 Registry Scan, Backup, and Restore

**Feature Branch**: `mvp4-registry`  
**Created**: 2026-06-25  
**Status**: Draft  
**Input**: MVP4 registry scanning and backup/restore requirements

## User Scenarios & Testing

### User Story 1 - Identify invalid uninstall registry entries (Priority: P1)

A Windows user scans the standard uninstall registry locations and sees installed software with invalid and orphaned entries highlighted without any automatic deletion.

**Independent Test**: Provide registry reader fixtures for the three uninstall branches and assert installed software rows include DisplayName, version, publisher, install path, estimated size, registry path, orphan state, and invalid state.

**Acceptance Scenarios**:

1. Given standard uninstall entries exist, when scanning software, then entries from HKLM, WOW6432Node, and HKCU uninstall paths are listed.
2. Given an entry has an InstallLocation pointing to a missing directory, when scanning, then that row is marked orphaned.
3. Given an entry has an empty or abnormal DisplayName, when scanning, then that row is marked invalid.
4. Given scan results contain invalid or orphaned entries, when displayed in the registry tab, then those entries are visually highlighted and no delete action is offered.

---

### User Story 2 - Browse and search installed software (Priority: P2)

A user reviews installed software, searches by visible metadata, sorts by name, size, or publisher, and opens the Windows uninstall settings entry point.

**Independent Test**: Load a known software list into the registry view model and assert search filtering, sort commands, formatted status, and uninstall entry launch command contract.

**Acceptance Scenarios**:

1. Given software scan results, when the user enters a search query, then the list is filtered by name, publisher, version, or install path.
2. Given software scan results, when sorting by name, size, or publisher, then the displayed order changes accordingly.
3. Given a software row, when the user opens the uninstall entry, then the app attempts to launch Windows Apps & Features without modifying registry data.

---

### User Story 3 - Back up uninstall registry branches (Priority: P3)

A user selects a registry branch, exports it to a timestamped `.reg` backup file, and can view or delete backup history.

**Independent Test**: Use a test registry exporter fixture to write a `.reg` file and assert timestamped naming, standard header, branch content, history listing, and deletion behavior.

**Acceptance Scenarios**:

1. Given the registry tab opens, when backup options are shown, then uninstall-related branches are preselected/default options.
2. Given a selected branch and output directory, when backup runs, then a timestamped `.reg` file is created without overwriting prior backups.
3. Given backup files exist, when backup history is loaded, then file path, branch name, backup time, and file size are shown.
4. Given a backup history item, when deleted, then only the selected backup file is removed.

---

### User Story 4 - Restore from a `.reg` file with confirmation (Priority: P4)

A user chooses a local `.reg` file, previews contained branches, confirms twice, and imports the file while the operation is logged.

**Independent Test**: Provide valid and invalid `.reg` files to the restore service and assert preview parsing, safe branch rejection, restore result, and error handling without crashes.

**Acceptance Scenarios**:

1. Given a selected `.reg` file, when preview is requested, then contained registry branches are displayed before import.
2. Given a valid `.reg` file and user confirmation, when restore executes, then the import command is invoked and the operation is logged.
3. Given an invalid `.reg` file, when restore executes, then the result is false or failed and the app remains stable.
4. Given a `.reg` file references system core branches such as HKLM\\SYSTEM or HKLM\\SAM, when restore is requested, then the operation is rejected.

## Requirements

### Functional Requirements

- **FR-001**: The scanner MUST read these uninstall registry paths: `HKLM\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall`, `HKLM\\Software\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall`, and `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall`.
- **FR-002**: The scanner MUST parse `DisplayName`, `InstallLocation`, `EstimatedSize`, `Publisher`, `DisplayVersion`, `UninstallString`, and registry path for each entry.
- **FR-003**: The scanner MUST mark entries whose non-empty `InstallLocation` directory no longer exists as orphaned.
- **FR-004**: The scanner MUST mark entries with empty, whitespace-only, control-character-only, or abnormal `DisplayName` as invalid.
- **FR-005**: The registry tab MUST list software name, version, publisher, install path, estimated size, and status.
- **FR-006**: The registry tab MUST support search filtering and sorting by name, size, and publisher.
- **FR-007**: Invalid and orphaned rows MUST be visually distinguishable with red highlighting or red status text.
- **FR-008**: The app MUST provide an action to open the Windows Apps & Features uninstall entry point without editing registry data.
- **FR-009**: The backup UI MUST default to uninstall-related registry branches and allow selecting one branch for export.
- **FR-010**: Backup export MUST create standard `.reg` files with a timestamped file name.
- **FR-011**: Backup history MUST list existing `.reg` backup files and allow deleting selected backup files.
- **FR-012**: Restore UI MUST allow selecting a local `.reg` file and previewing contained branch names before import.
- **FR-013**: Restore execution MUST require confirmation in the UI before invoking import.
- **FR-014**: Restore MUST reject invalid `.reg` files and unsafe system branches without crashing.
- **FR-015**: Registry operations MUST write log entries for scan, backup, restore, rejection, and failure events.
- **FR-016**: The feature MUST NOT offer one-click registry cleanup, automatic registry deletion, or scanning/modifying system core branches.

### Key Entities

- **InstalledSoftware**: A parsed uninstall registry entry with display metadata, size estimate, uninstall string, registry path, validity, and orphan state.
- **RegistryBackup**: A timestamped backup file record with file path, branch name, backup time, and file size.
- **Registry Branch Option**: A selectable allowed registry branch for backup or preview.
- **Operation Log Entry**: A user-visible string describing scan, backup, restore, rejection, or failure events.

## Success Criteria

- **SC-001**: Scanner tests prove standard uninstall entries from all three target paths can be parsed.
- **SC-002**: Scanner tests prove missing install directories are marked orphaned and empty/abnormal names are marked invalid.
- **SC-003**: Backup tests prove exported files contain the Windows Registry Editor header, selected branch, and timestamped names.
- **SC-004**: Restore tests prove valid `.reg` files can be parsed/imported through the service seam and invalid files fail safely.
- **SC-005**: The solution builds with the registry tab wired into the fourth navigation slot.
- **SC-006**: No implemented flow deletes registry keys automatically.

## Assumptions

- Running on non-Windows development hosts uses fixture-backed services for tests and may skip real registry access.
- Real registry export/import uses Windows platform commands or APIs only when running on Windows.
- EstimatedSize values in uninstall entries are treated as kilobytes when read from registry data and converted to bytes for display.
