# CLAUDE.md — SDE Search ArcGIS Pro Add-In

This file documents the codebase structure, development conventions, and key workflows for AI assistants working on this project.

---

## Project Overview

**SDE Search** is a dockable ArcGIS Pro add-in that lets users search, filter, and browse feature classes, tables, and feature datasets from enterprise SDE geodatabases. It surfaces ArcGIS metadata, field definitions, domain values, and flags (Editor Tracking, Archiving) in a WPF dockpane.

- **Language**: C# with .NET 8.0 (Windows x64 only)
- **UI Framework**: WPF (XAML)
- **SDK**: ArcGIS Pro SDK 3.3+
- **Pattern**: MVVM (Model–View–ViewModel)
- **Build tool**: MSBuild via Visual Studio 2022

---

## Repository Structure

```
filtering_features_pro_addin/
├── ProAppAddInSdeSearch.sln                  # Visual Studio solution
├── README.md                                 # End-user documentation
├── RELEASE_NOTES.md                          # User-facing changelog (update with every feature/fix)
├── SEED_CACHE.md                             # Seed cache deployment guide
├── TODO.md                                   # Active feature backlog
├── BUGS.md                                   # Known bugs (currently empty)
└── ProAppAddInSdeSearch/                     # Main C# project
    ├── ProAppAddInSdeSearch.csproj           # MSBuild project file
    ├── Config.daml                           # Add-in registration (XML)
    ├── Module1.cs                            # ArcGIS Pro module entry point
    ├── SdeSearchPaneViewModel.cs             # Core business logic (~1,637 lines)
    ├── SdeSearchPaneView.xaml                # WPF UI layout (~522 lines)
    ├── SdeSearchPaneView.xaml.cs             # UI code-behind (key/click handlers)
    ├── Converters.cs                         # WPF IValueConverter implementations
    ├── SeedCache.json                        # Bundled template cache for first-time users
    ├── Properties/
    │   └── launchSettings.json              # Debug profile (launches ArcGISPro.exe)
    ├── Images/                               # Light-mode toolbar icons (16px, 32px)
    └── DarkImages/                           # Dark-mode toolbar icons (16px, 32px)
```

---

## Key Source Files

### `SdeSearchPaneViewModel.cs` — Core Business Logic

The single most important file. Contains all data models, commands, caching, search logic, and ArcGIS API calls. Key responsibilities:

- **Connection detection** — scans `.sde` files from three locations:
  1. Current `.aprx` project database connections
  2. `{ProjectHome}\DatabaseConnections\*.sde`
  3. `%AppData%\Esri\ArcGISPro\DatabaseConnections\*.sde`
- **Dataset enumeration** — queries geodatabases for Feature Datasets, Feature Classes, Tables, and their metadata XML
- **Caching** — serializes results to JSON in `%LocalAppData%\ProAppAddInSdeSearch\Cache\` (filename = MD5 hash of connection path)
- **Search and filtering** — in-memory filtering on name, metadata (description/summary/tags), tags-only, type (FC/Table/FD), and flags (Editor Tracking, Archiving)
- **Map operations** — adds layers to the active map or creates a new map
- **Commands** (all `ICommand` via `RelayCommand`):
  - `SearchCommand` / `ClearCommand`
  - `RefreshConnectionsCommand` — rescans for `.sde` files
  - `ReloadDataCommand` — force-refreshes from database (bypasses cache)
  - `AddToMapCommand` / `CopyPathCommand`
  - `BackToResultsCommand`
  - `BrowseSdeCommand` / `AddManualPathCommand`
  - `ToggleThemeCommand`

**Data models defined here:**
- `SdeConnectionItem` — represents a `.sde` connection file
- `SdeDatasetItem` — a dataset/table/feature dataset with metadata, flags, fields
- `FieldInfo` — field metadata (type, nullable, domain)
- `DomainCodeValue` — coded value domain entry

**Feature detection logic:**
- Editor Tracking: presence of `CREATED_USER`, `CREATED_DATE`, `LAST_EDITED_USER`, `LAST_EDITED_DATE` fields
- Archiving: `GDB_FROM_DATE` / `GDB_TO_DATE` fields, or `IsArchivingEnabled` API property

**Dates come from ArcGIS metadata XML** (`//CreaDate`, `//ModDate`), not from the database itself.

### `SdeSearchPaneView.xaml` — WPF UI Layout

Row-based layout (top to bottom):
1. Header — title, theme toggle, connection refresh
2. Connection row — `ComboBox` for `.sde` file selection, reload button, collapsible manual path entry
3. Search row — text input, Filter button, Clear button, search mode checkboxes (Name / Metadata / Tags)
4. Show row — type filter checkboxes (Feature Classes, Tables, Feature Datasets)
5. Filters row — flag checkboxes (Editor Tracking, Archiving)
6. Progress row — indeterminate `ProgressBar` + status text (visible during operations)
7. Content area — dual view: results `ItemsControl` ↔ detail `ScrollViewer`
8. Status bar — cache age indicator

Uses dynamic resources for theming (light/dark palettes switched at runtime).

### `SdeSearchPaneView.xaml.cs` — UI Code-Behind

- Theme switching: applies a `ResourceDictionary` with either light or dark color palette
- Keyboard handling: Enter in search box → Search; Enter in manual path field → Add
- Result click → navigate to detail view
- Add-to-map button click handler
- Syncs with `IsDarkMode` property changes from ViewModel

### `Converters.cs` — WPF Value Converters

| Converter | Input | Output |
|-----------|-------|--------|
| `GeometryIconPathConverter` | geometry type string | SVG path data for icon |
| `GeometryIconColorConverter` | geometry type string | hex color (`#RRGGBB`) |
| `InverseBoolConverter` | `bool` | `!bool` |
| `NonEmptyStringToVisibilityConverter` | `string` | `Visibility.Visible` or `Collapsed` |

**Geometry color mapping:**
- Point → `#4CAF50` (green)
- Polyline → `#2196F3` (blue)
- Polygon → `#FF9800` (orange)
- Multipatch → `#9C27B0` (purple)
- Table → `#789090C` (blue-gray)
- Feature Dataset → `#FFC107` (amber)
- Relationship → `#8D6E63` (brown)
- Unknown → `#9E9E9E` (gray)

### `Config.daml` — Add-In Registration

ArcGIS Pro add-in manifest XML. Key values:
- **Add-in ID**: `{a73f5d21-b8e4-4c9a-a620-7e3d1f6c8b55}`
- **Minimum Pro version**: 3.3.0.52636
- **Author**: gallaga | **Company**: HRM
- Registers the `SdeSearchPaneViewModel` as a dockpane that opens beside the catalog
- Registers the button on the **Add-In** tab in the **SDE Search** group

### `Module1.cs` — Framework Entry Point

Minimal boilerplate. Singleton `Current` property. `CanUnload()` returns `true`.

### `SeedCache.json` — Bundled Template Cache

Pre-populated cache file copied to `%LocalAppData%\ProAppAddInSdeSearch\Cache\` on first launch when no user cache exists. Status bar shows *"template data — click ↻ to refresh"* when seed cache is in use.

---

## Architecture

### MVVM

- **Model**: `SdeDatasetItem`, `SdeConnectionItem`, `FieldInfo`, `DomainCodeValue` — plain data classes with `[JsonInclude]` attributes for serialization
- **ViewModel**: `SdeSearchPaneViewModel` — all logic, properties with `NotifyPropertyChanged`, `ICommand` implementations
- **View**: `SdeSearchPaneView.xaml` + `.xaml.cs` — purely presentational; binds to ViewModel; code-behind limited to input events and theme resources

### ArcGIS Threading Rules

ArcGIS Pro requires all geodatabase and map operations to run on the MCT (Main CIM Thread). Always use `QueuedTask.Run(async () => { ... })` for:
- Opening geodatabases / reading datasets
- Adding layers to maps
- Any `ArcGIS.Core.Data.*` API calls

UI updates must return to the UI thread. Use `await` with `QueuedTask.Run(...)` and then update `ObservableCollection` / bindable properties from the calling context.

### Caching Strategy

```
User selects connection
        │
        ▼
Check %LocalAppData%\ProAppAddInSdeSearch\Cache\<md5-of-path>.json
        │
   Exists?─── Yes ──→ Load from JSON (fast)
        │
       No
        │
        ▼
Enumerate geodatabase via ArcGIS API (slow, shows progress)
        │
        ▼
Serialize to JSON cache file
```

Cache is invalidated when the user clicks **Reload Data** (↻ next to connection dropdown). A version field in the cache JSON automatically invalidates caches between add-in releases that change the cache format.

---

## Build & Deployment

### Requirements (Development Machine)

- Windows x64
- ArcGIS Pro 3.3+ installed at `C:\Program Files\ArcGIS\Pro`
- Visual Studio 2022
- .NET 8.0 SDK

### Build Steps

1. Open `ProAppAddInSdeSearch.sln` in Visual Studio 2022
2. Select **Release** configuration
3. Build solution (`Ctrl+Shift+B`)
4. Output: `ProAppAddInSdeSearch/bin/Release/net8.0-windows/ProAppAddInSdeSearch.esriAddinX`

### Debug

- Set launch profile to `ProAppAddInSdeSearch` (configured in `launchSettings.json`)
- F5 launches ArcGIS Pro with the add-in loaded
- Debug configuration keeps ArcGIS assemblies referenced from the installed Pro location

### Installation (End Users)

Double-click the `.esriAddinX` file, or copy to:
```
%UserProfile%\Documents\ArcGIS\AddIns\ArcGISPro
```

---

## Development Conventions

### C# Style

- Use `private` fields with `_camelCase` prefix (e.g., `_searchText`, `_isDarkMode`)
- Public properties use `PascalCase` and call `NotifyPropertyChanged()` on set
- Commands are declared as public properties of type `ICommand`, initialized in the constructor via `RelayCommand`
- Group related fields and properties with `// ── Section ───` comment banners (existing style)
- Prefer `async`/`await` over raw `Task.ContinueWith`
- All geodatabase work must go through `QueuedTask.Run()`

### XAML Style

- All colors are defined as `DynamicResource` — never hardcode color values inline
- Geometry type icons are rendered as `Path` elements using `Data="{Binding ..., Converter={...}}"` — add new geometry types to `GeometryIconPathConverter` and `GeometryIconColorConverter`
- Prefer `Visibility` binding via converters (`NonEmptyStringToVisibilityConverter`, `InverseBoolConverter`) rather than code-behind visibility logic

### Data Models

- Keep data model classes inside `SdeSearchPaneViewModel.cs` (existing convention — do not split into separate files unless models grow significantly)
- Serialization uses `System.Text.Json` with `[JsonInclude]` on public properties
- Add `[JsonIgnore]` to computed/runtime-only properties to keep cache files clean

### Adding New Search/Filter Types

1. Add a backing field and public property to `SdeSearchPaneViewModel` (follow the `_filterEditorTracking` / `FilterEditorTracking` pattern)
2. Bind the new checkbox in `SdeSearchPaneView.xaml` inside the appropriate filter row
3. Add filtering logic in `ApplyFilterAndSearch()` method

### Adding New Detail View Sections

1. Add properties to `SdeDatasetItem` (with `[JsonInclude]` if they should be cached)
2. Populate during `LoadAllDatasets()` enumeration (or lazy-load in the detail view handler)
3. Add XAML to the detail `ScrollViewer` section in `SdeSearchPaneView.xaml`

---

## No Automated Tests

There is no automated test project. All testing is manual via ArcGIS Pro:

1. Build in Debug mode
2. Press F5 to launch ArcGIS Pro
3. Open a project with SDE connections
4. Open the SDE Search pane from the Add-In tab
5. Exercise the feature being tested

When making changes, test against:
- A connection with many datasets (performance / progress bar)
- A connection with no datasets (empty state)
- A dataset with full metadata (metadata parsing)
- A dataset with no metadata (null/empty handling)
- Datasets with editor tracking and archiving enabled
- Dark mode and light mode UI

---

## Known Backlog (TODO.md)

Open items as of the last README update:

| Item | Status |
|------|--------|
| Show `ModifiedDate` in results list | Pending |
| Group results by feature dataset | Pending |
| SeedCache.json creation progress indicator | Pending |
| Audit remaining UI spacing/padding | Pending |
| Search by field name | Pending |
| Search by field value | Pending |
| Search by date range | Pending |
| Attribute rules indicator/badge | Pending |
| Export results | Pending |

---

## File Paths Reference

| Path | Purpose |
|------|---------|
| `%LocalAppData%\ProAppAddInSdeSearch\Cache\` | Local JSON cache directory |
| `%AppData%\Esri\ArcGISPro\DatabaseConnections\` | Default ArcGIS Pro SDE connection directory |
| `{ProjectHome}\DatabaseConnections\` | Project-level SDE connections |
| `C:\Program Files\ArcGIS\Pro\bin\` | ArcGIS Pro SDK assemblies (reference only, not copied) |
| `%UserProfile%\Documents\ArcGIS\AddIns\ArcGISPro\` | Add-in installation directory |

---

## Git Workflow

- Default branch: `master`
- Claude-generated feature branches follow the pattern: `claude/<session-id>`
- Changes are developed on feature branches and merged to `master` via pull requests
- No CI/CD pipeline — builds and testing are manual

---

## Release Notes

**Always update `RELEASE_NOTES.md` when completing a task that adds, changes, or fixes user-visible behaviour.**

- Add a new dated section at the top of the file for the current date if one does not already exist
- Write entries from the user's perspective — what changed and why it matters, not implementation details
- Group related changes under a short heading (e.g. `### Dynamic item-type dropdown filter`)
- Minor internal refactors, documentation-only commits, and seed cache data updates do not need entries
- Keep entries concise — two to five bullet points per feature is typical
