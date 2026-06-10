# Backup & Restore

The Backup & Restore module provides full platform backup and restore (export/import) for Virto Commerce. It packages platform data — users, roles, permissions, settings and dynamic properties — together with the data of all installed modules into a single, optionally AES-256 encrypted ZIP archive, and restores it back on demand. Long-running operations execute as Hangfire background jobs with a real-time progress timeline, and the module also installs sample data for fresh environments.

This module owns the platform export/import feature that previously shipped inside the platform core. It registers the same `api/platform/*` endpoints and the same permissions, so existing API clients and role assignments keep working unchanged.

## Key Features

* **Full platform backup** — exports security data (roles, users, user API keys), settings, and dynamic properties (including dictionary items), plus the data of every installed module that participates via `IExportSupport`.
* **One-click restore** — imports a backup back into the platform, creating or merging entities; domain events are suppressed during restore to avoid cascading side effects, and the operator who started the restore is preserved so their session is not invalidated.
* **Optional AES-256 encryption** — password-protected ZIP archives; the generated password is shown once. `Manifest.json` is always written unencrypted so the import UI can detect encryption and prompt before reading protected entries.
* **Module participation model** — any module that implements `IExportSupport` / `IImportSupport` is automatically included in backups and restores; no per-module wiring required.
* **Streaming & memory-efficient** — large JSON collections are read and written in pages (batches of 50) so big datasets never load fully into memory.
* **Real-time progress** — export and restore run as Hangfire background jobs and stream a structured progress log (info/warning/error) to the admin UI via push notifications.
* **Sample data installation** — discovers and imports sample datasets, and contributes the sample-data step to the platform setup wizard.
* **Backward compatible** — preserves the original `api/platform/*` routes, permission strings, backup ZIP layout, and admin UI; the implementation satisfies both the modern `IBackupRestoreManager` and the legacy `IPlatformExportImportManager`.

## Configuration

### Permissions

Registered under the **Platform** group (string values unchanged from the platform core, so existing roles keep their access):

| Permission | Description |
| --- | --- |
| `platform:exportImport:access` | View Backup/Restore in the main menu |
| `platform:export` | Backup Platform and modules data |
| `platform:import` | Restore Platform and modules data |

### Application settings

The module reuses the platform's `PlatformOptions` for file locations:

| Option | Default | Description |
| --- | --- | --- |
| `Platform:DefaultExportFolder` | `export` | Folder where completed backups are written and served from. |
| `Platform:DefaultExportFileName` | `vc_backup_{0:yyyyMMddHHmmss}.zip` | Backup file name template. |
| `Platform:LocalUploadFolderPath` | `app_data/uploads` | Staging folder for backups uploaded before a restore. |
| `Platform:SampleDataUrl` | — | Base URL (or direct `.zip`) used to discover and download sample data. |

## Architecture

The module follows the standard Virto Commerce vertical slice (Core / Data / Web) and consumes the export/import contracts that remain in `VirtoCommerce.Platform.Core`.

```
┌───────────────────────────────────────────────────────────────┐
│  VirtoCommerce.BackupRestore.Web                              │
│  • BackupRestoreController  (api/platform/export…)            │
│  • SampleDataController     (api/platform/sampledata)         │
│  • AngularJS admin UI (export / import / sample data wizard)  │
├───────────────────────────────────────────────────────────────┤
│  VirtoCommerce.BackupRestore.Data                             │
│  • BackupRestoreManager : IBackupRestoreManager,              │
│                           IPlatformExportImportManager        │
│  • SharpZipBackupArchive / SharpZipBackupArchiveFactory       │
│    (SharpZipLib, AES-256)                                     │
├───────────────────────────────────────────────────────────────┤
│  VirtoCommerce.BackupRestore.Core                             │
│  • IBackupRestoreManager                                      │
│  • IZipBackupArchive / IZipBackupArchiveFactory               │
│  • ModuleConstants (permissions)                              │
└───────────────────────────────────────────────────────────────┘
                 │ consumes contracts from
                 ▼
   VirtoCommerce.Platform.Core.ExportImport
   PlatformExportManifest · ExportImportOptions · IExportSupport ·
   IImportSupport · export/import push notifications
```

### Export flow

1. An administrator starts a backup from the UI → `POST api/platform/export`.
2. The controller enqueues a Hangfire job and returns a push notification (plus the one-time password when encryption is requested).
3. `BackupRestoreManager.ExportAsync` writes platform entries (roles, users, API keys, settings, dynamic properties) to `PlatformEntries.json`.
4. For each installed module implementing `IExportSupport`, its data is streamed into `{ModuleId}.json`.
5. `Manifest.json` is written last (always unencrypted) describing the contents and the `IsEncrypted` flag.
6. When a password is supplied, every entry except the manifest is AES-256 encrypted.
7. The finished archive is offered for download via `api/platform/export/download/{fileName}`.

Restore reverses the process: the manifest is read first to detect encryption, then platform entries and each module's data are imported through `IImportSupport`, with progress and errors surfaced per section/module.

## Components

### Projects

| Project | Layer | Purpose |
| --- | --- | --- |
| `VirtoCommerce.BackupRestore.Core` | Core | Module contracts (`IBackupRestoreManager`, `IZipBackupArchive`, `IZipBackupArchiveFactory`) and `ModuleConstants`. |
| `VirtoCommerce.BackupRestore.Data` | Data | `BackupRestoreManager` orchestration and the SharpZipLib-backed archive implementation. |
| `VirtoCommerce.BackupRestore.Web` | Web | REST controllers, AngularJS admin UI, localizations, and module registration. |
| `VirtoCommerce.BackupRestore.Tests` | Tests | Unit tests. |

### Key services

| Service | Interface | Responsibility |
| --- | --- | --- |
| `BackupRestoreManager` | `IBackupRestoreManager` (+ legacy `IPlatformExportImportManager`) | Orchestrates export/import of platform and module data, encryption, and progress reporting. |
| `SharpZipBackupArchiveFactory` | `IZipBackupArchiveFactory` | Opens a backup archive for reading or writing (AES-256 when a password is provided). |
| `SharpZipBackupArchive` | `IZipBackupArchive` | Per-entry ZIP read/write stream wrapper over SharpZipLib. |

### REST API

Base route: `api/platform`

| Method | Path | Description |
| --- | --- | --- |
| `POST` | `/export` | Start a platform backup job. |
| `POST` | `/import` | Start a platform restore job. |
| `GET` | `/export/download/{fileName}` | Download a completed backup file. |
| `POST` | `/exortimport/tasks/{jobId}/cancel` | Cancel a running backup/restore job. |
| `GET` | `/export/manifest/new` | Build a new export manifest listing installed modules. |
| `GET` | `/export/manifest/load` | Read the manifest from an uploaded backup. |
| `GET` | `/sampledata/discover` | List available sample data packages. |
| `POST` | `/sampledata/import` | Import sample data by name or URL. |
| `POST` | `/sampledata/autoinstall` | Auto-install the first available sample data package. |
| `GET` | `/sampledata/state` | Get the current sample-data import state. |

## Documentation

* [Backup and restore user documentation](https://docs.virtocommerce.org/platform/user-guide/)
* [REST API](https://virtostart-demo-admin.govirto.com/docs/index.html?urls.primaryName=VirtoCommerce.BackupRestore)
* [View on GitHub](https://github.com/VirtoCommerce/vc-module-backup-restore/)

## References

* [Deployment](https://docs.virtocommerce.org/platform/developer-guide/Tutorials-and-How-tos/Tutorials/deploy-module-from-source-code/)
* [Installation](https://docs.virtocommerce.org/platform/user-guide/modules-installation/)
* [Home](https://virtocommerce.com)
* [Community](https://www.virtocommerce.org)
* [Download latest release](https://github.com/VirtoCommerce/vc-module-backup-restore/releases/latest)

## License

Copyright (c) Virto Solutions LTD.  All rights reserved.

This software is licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at http://virtocommerce.com/opensourcelicense.

Unless required by the written form, the software
distributed under the License is provided on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
