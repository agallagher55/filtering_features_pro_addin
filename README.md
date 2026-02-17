# SDE Search - ArcGIS Pro Add-In

A dockable search pane for ArcGIS Pro that lets you quickly find and add feature classes, tables, and feature datasets from enterprise SDE geodatabases.

## Requirements

- **ArcGIS Pro 3.3** or later
- **Windows** (x64)
- **.NET 8.0** runtime (included with ArcGIS Pro 3.3+)
- One or more `.sde` database connection files

## Installation

### From a built add-in file

1. Double-click the `.esriAddinX` file to install, or copy it to:
   ```
   %UserProfile%\Documents\ArcGIS\AddIns\ArcGISPro
   ```
2. Restart ArcGIS Pro if it's already running.

### Building from source

1. Ensure ArcGIS Pro 3.3+ is installed at the default location (`C:\Program Files\ArcGIS\Pro`).
2. Open `ProAppAddInSdeSearch.sln` in Visual Studio 2022.
3. Build the solution (Release configuration recommended).
4. The `.esriAddinX` file is generated in `bin\Release\`.

## Getting Started

### 1. Open the SDE Search pane

Go to the **Add-In** tab on the ArcGIS Pro ribbon and click the **SDE Search** button. The search pane will dock beside the catalog pane.

### 2. Select a database connection

The add-in automatically detects `.sde` connection files from:

| Source | Path |
|--------|------|
| Current project | Database connections registered in the open `.aprx` project |
| Project folder | `{ProjectHome}\DatabaseConnections\*.sde` |
| ArcGIS Pro default | `%AppData%\Esri\ArcGISPro\DatabaseConnections\*.sde` |

Select a connection from the dropdown to load its contents.

To add a connection that isn't auto-detected, expand **"Add connection manually..."** and either browse for the `.sde` file or paste its full path.

### 3. Search and filter

Type in the search box and click **Filter** (or press Enter). The search matches against:

- **Name** - Feature class, table, and dataset names (on by default)
- **Metadata / Tags** - Description, summary, purpose, and tags from ArcGIS metadata (enable via checkbox)

Use the **Show** checkboxes to filter by type:
- Feature Classes
- Tables
- Feature Datasets

### 4. View details

Click any item in the results list to see its detail view, which includes:

- Full qualified name, dataset type, and geometry type
- Spatial reference
- Metadata (description, summary, tags, credits, use constraints)
- Complete field list with types, nullability, and domain info

### 5. Add to map

Click the **+** button next to any feature class or table to add it to the active map. If no map is open, a new one is created automatically.

From the detail view, click **+ Map** to do the same.

### 6. Copy path

In the detail view, click the clipboard icon to copy the full geodatabase path for use in geoprocessing tools, scripts, or arcpy.

## Caching

The add-in caches dataset listings locally to avoid querying the database on every load.

- **Cache location**: `%LocalAppData%\ProAppAddInSdeSearch\Cache\`
- **Status bar** shows cache age (e.g., "cached 2h ago")
- Click the **Reload Data** button (↻ next to the connection dropdown) to force a fresh enumeration from the database

### Seed cache (for deployment)

A `SeedCache.json` file can be bundled with the add-in to provide instant first-load for new users. See [SEED_CACHE.md](SEED_CACHE.md) for details on how to create and deploy one.

When using a seed cache, the status bar displays *"template data - click ↻ to refresh from your database"*.

**Important**: After any add-in update, click the refresh button (↻) at least once to ensure the cache includes the latest schema information. Outdated caches are automatically invalidated when the cache format changes between versions.

## UI Features

- **Light / Dark mode** - Toggle with the theme button in the header. Follows ArcGIS Pro's theme by default.
- **Connection refresh** - The refresh button (⟳) in the header rescans for `.sde` files.
- **Keyboard** - Press Enter in the search box to filter, or in the manual path field to add.
- **Visual indicators** - Color-coded geometry type icons (points, lines, polygons, etc.) and feature dataset badges for quick visual scanning.

## Dates (Created / Modified)

The created and modified dates shown in the add-in come from **ArcGIS metadata XML**, not from the actual database.

- **CreatedDate** — parsed from `//CreaDate` or `//idinfo/citation/citeinfo/pubdate` in the metadata XML
- **ModifiedDate** — parsed from `//ModDate` or `//metainfo/metd` in the metadata XML

These are the **metadata authoring dates** (when someone last edited the item's metadata in the catalog), *not* the actual data creation/modification timestamps. If you want real data timestamps, they'd need to come from editor tracking fields (`CREATED_DATE`, `LAST_EDITED_DATE`) or the geodatabase system catalog tables instead.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| No connections appear in the dropdown | Ensure `.sde` files exist in one of the scanned locations listed above, or add one manually. Click ⟳ in the header to rescan. |
| "Error: Failed to add data, unsupported data type" | Click ↻ to refresh from the database. This typically means the local cache is outdated and missing feature dataset path information. |
| Metadata fields are empty | Metadata is loaded from the geodatabase's ArcGIS metadata. If no metadata has been authored for a dataset, these fields will be blank. |
| Slow initial load | The first live enumeration queries every dataset in the geodatabase. Subsequent loads use the local cache. Consider deploying a seed cache for large databases. |

## Project Structure

```
ProAppAddInSdeSearch/
├── Config.daml                    # Add-in registration and UI declarations
├── Module1.cs                     # Framework module entry point
├── SdeSearchPaneViewModel.cs      # All business logic, caching, and data models
├── SdeSearchPaneView.xaml         # WPF UI layout
├── SdeSearchPaneView.xaml.cs      # UI code-behind (click handlers, key events)
├── Converters.cs                  # WPF value converters for data binding
├── SeedCache.json                 # Bundled template cache for first-time users
└── Images/ & DarkImages/          # Toolbar icons
```
