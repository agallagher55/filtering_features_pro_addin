# SDE Search — User Guide

A dockable ArcGIS Pro add-in for quickly finding and adding feature classes, tables, and feature datasets from enterprise SDE geodatabases.

---

## Table of Contents

1. [Installation](#1-installation)
2. [Opening the Pane](#2-opening-the-pane)
3. [Connecting to a Database](#3-connecting-to-a-database)
4. [Searching for Data](#4-searching-for-data)
5. [Filtering Results](#5-filtering-results)
6. [Viewing Item Details](#6-viewing-item-details)
7. [Adding Data to the Map](#7-adding-data-to-the-map)
8. [Copying a Dataset Path](#8-copying-a-dataset-path)
9. [Cache Management](#9-cache-management)
10. [UI Features](#10-ui-features)
11. [Troubleshooting](#11-troubleshooting)

---

## 1. Installation

### Requirements

- ArcGIS Pro **3.3** or later
- Windows (x64)

### Option A - Install from a shared folder (recommended for HRM users)

1. Open ArcGIS Pro and go to **Settings → Add-In Manager → Options**.
2. Click **Add Folder** and enter the following path:
   ```
   R:\HRM Common Directory\FileSharing\Alex Gallagher\ArcGIS Pro\Add-ins\SDE Search\Prod
   ```
   > If you cannot access this path, contact your GIS administrator.
3. Make sure **"Load all Add-Ins without restrictions"** is selected.
4. Restart ArcGIS Pro.

### Option B - Install from an `.esriAddinX` file

1. Double-click the `.esriAddinX` file to install it automatically, **or** copy it to:
   ```
   %UserProfile%\Documents\ArcGIS\AddIns\ArcGISPro
   ```
2. Restart ArcGIS Pro if it is already running.

> **OneDrive note:** If your Documents folder is redirected to OneDrive, the add-in path will appear under OneDrive. This is normal behavior in ArcGIS Pro and does not affect functionality.

---

## 2. Opening the Pane

Go to the **Add-In** tab on the ArcGIS Pro ribbon and click the **SDE Search** button. The search pane will open and dock beside the Catalog pane.

---

## 3. Connecting to a Database

### Auto-detected connections

When the pane opens, it automatically scans for `.sde` connection files in three locations:

| Source | Location |
|--------|----------|
| Current project | Database connections registered in the open `.aprx` |
| Project folder | `{ProjectHome}\DatabaseConnections\*.sde` |
| ArcGIS Pro default | `%AppData%\Esri\ArcGISPro\DatabaseConnections\*.sde` |
| ArcGIS Pro favorites | `%AppData%\Esri\ArcGISPro\Favorites\*.sde` |

Select a connection from the **DATABASE CONNECTION** dropdown to load its contents.

### Adding a connection manually

If a connection file is not auto-detected:

1. Select **"⊕ Add connection manually..."** from the bottom of the dropdown. A file browser will open automatically.
2. Browse to and select the `.sde` file, then click **Open**.

Alternatively, expand the manual path field, paste the full path to the `.sde` file, and press **Enter** or click **Add**.

### Refreshing the connection list

Click the **⟳** button in the top-right of the pane header to rescan all source locations for new or removed `.sde` files.

---

## 4. Searching for Data

Type a search term into the search box and press **Enter** or click **Search**. Click **✕** to clear the search and show all results.

### Search modes

Three checkboxes below the search box control what is searched. They can be combined freely.

| Checkbox | What it searches |
|----------|-----------------|
| **Name** | Feature class, table, and dataset names *(enabled by default)* |
| **Metadata** | Description, summary, purpose, credits, and tags from ArcGIS metadata |
| **Tags** | ArcGIS metadata tags/keywords only |

> Toggling a checkbox immediately re-runs the current search - no need to click Search again.

---

## 5. Filtering Results

The **FILTERS** section (collapsible) narrows results independently of the search term. Filters apply instantly whenever a checkbox or dropdown changes.

### Geometry type / item type

Use the **Geometry type** dropdown to show only a specific type of item:

- All Types
- Feature Class - Point
- Feature Class - Polyline
- Feature Class - Polygon
- Feature Class - Multipatch
- Table
- Feature Dataset
- Relationship Class

The dropdown is populated dynamically based on what is actually present in the loaded connection.

### Feature flags

Check one or more flag checkboxes to show only datasets that have that feature enabled:

| Checkbox | Shows datasets where… |
|----------|-----------------------|
| **Editor Tracking** | `CREATED_USER`, `CREATED_DATE`, `LAST_EDITED_USER`, or `LAST_EDITED_DATE` fields are present |
| **Archiving** | Geodatabase archiving is enabled (detected via `GDB_FROM_DATE`/`GDB_TO_DATE` fields or the ArcGIS archiving API) |
| **Subtypes** | One or more subtypes are defined on the dataset |
| **Attribute Rules** | One or more attribute rules are defined on the dataset |

Multiple flag filters are combined with AND (a dataset must match all checked filters to appear).

### Result badges

Each result row displays colour-coded badges for at-a-glance context:

| Badge | Meaning |
|-------|---------|
| 📂 *Dataset name* | The feature class belongs to this feature dataset |
| **Tracking** | Editor Tracking is enabled |
| **Archiving** | Archiving is enabled |
| **Subtypes** | Subtypes are defined |
| **Attr Rules** | Attribute Rules are defined |

---

## 6. Viewing Item Details

Click any row in the results list to open its detail view. Click **◀ Back** to return to the results list.

The detail view shows:

- **Full qualified name** — database, schema, and dataset name
- **Dataset type** and **geometry type**
- **Spatial reference**
- **Metadata** — description, summary, purpose, tags, credits, and use constraints (sourced from ArcGIS metadata XML)
- **Created / Modified dates** — these are the metadata authoring dates, not the actual data timestamps
- **Field list** — all fields with their data type, nullability, and coded-value domain entries

---

## 7. Adding Data to the Map

### From the results list

Hover over any feature class or table row and click the **map pin** button (🗺) that appears on the right side of the row.

### From the detail view

Click the **+ Map** button near the top of the detail view.

If no map is currently open, a new map is created automatically.

> Tables and relationship classes that cannot be rendered as layers will not show an add-to-map button.

---

## 8. Copying a Dataset Path

From the detail view, click the **clipboard icon** to copy the full geodatabase path to the clipboard. This path can be used directly in:

- Geoprocessing tool inputs
- ArcPy scripts (`arcpy.env.workspace`, `arcpy.management.*`, etc.)
- Model Builder parameters

---

## 9. Cache Management

The add-in caches dataset information locally to avoid re-querying the database on every load.

- **Cache location:** `%LocalAppData%\ProAppAddInSdeSearch\Cache\`
- **Status bar** shows how old the current cache is (e.g., *"cached 2h ago"*)

### Refreshing the cache

Click the **↻** button next to the connection dropdown to force a fresh enumeration directly from the database. This rebuilds the local cache and is useful after:

- New datasets have been added to the geodatabase
- Schema changes have been made
- You see stale or missing results

> After any add-in update, click ↻ at least once to ensure the cache matches the latest version.

### Template (seed) cache

On first launch, if no user cache exists, the add-in loads a pre-built **template cache** bundled with the installation. The status bar will show:

> *"template data - click ↻ to refresh from your database"*

Click ↻ whenever you are ready to load live data from your own database. Your personal cache will replace the template from that point on.

Click the cache info indicator in the status bar to view details about the active cache (source connection, generation date, dataset counts).

---

## 10. UI Features

| Feature | How to use |
|---------|------------|
| **Light / Dark mode** | Click the ☀ / 🌙 button in the pane header |
| **Collapse filters** | Click the **FILTERS** section header to hide or show the filter controls |
| **Keyboard shortcut** | Press **Enter** in the search box to run a search |
| **Geometry icons** | Each result displays a colour-coded vector icon: green = point, blue = line, orange = polygon, purple = multipatch, amber = feature dataset |
| **Result count** | Shown in the status bar after every search or filter change |

---

## 11. Troubleshooting

| Problem | Solution |
|---------|----------|
| No connections appear in the dropdown | Confirm `.sde` files exist in one of the scanned locations, or add one manually. Click **⟳** in the header to rescan. |
| Results look outdated or a dataset is missing | Click **↻** next to the connection dropdown to force a full refresh from the database. |
| "Error: Failed to add data, unsupported data type" when adding to map | Click **↻** to rebuild the cache. The cached path information is likely outdated. |
| Metadata fields are blank | No ArcGIS metadata has been authored for that dataset. The fields will remain empty until metadata is published in the geodatabase. |
| Slow initial load | The first live enumeration queries every dataset in the geodatabase. Subsequent loads use the local cache and are near-instant. |
| Blank ArcGIS Pro dialog with a spinner on launch | Remove the old add-in from `%UserProfile%\Documents\ArcGIS\AddIns\ArcGISPro\{a73f5d21-b8e4-4c9a-a620-7e3d1f6c8b55}` and reinstall the latest `.esriAddinX`. If it persists, clear `%LocalAppData%\ESRI\ArcGISPro\AssemblyCache`. |
| Add-in tab does not appear after installation | Confirm **"Load all Add-Ins without restrictions"** is enabled in **Settings → Add-In Manager → Options**, then restart ArcGIS Pro. |
