# Release Notes — SDE Search ArcGIS Pro Add-In

Changes are listed newest-first. Each entry covers a logical batch of related work.
Minor internal fixes, seed cache data updates, and documentation-only commits are omitted.

---

## 2026-02-25

### Cache info panel and add-in footer
- The **ⓘ** button (now a filled circle-i icon) in the status bar is visible whenever a cache is loaded — not only when template data is in use
- Clicking it opens a **Cache Info** panel showing where the cache was generated from (connection file path) and when it was last cached, plus a dataset count breakdown
- For template/seed caches the panel still notes that the data is pre-generated and provides guidance to refresh
- A fine-print footer at the bottom of the pane shows when the add-in was installed on the current machine

### Dynamic item-type dropdown filter
- Replaced the "Show:" checkboxes (Feature Classes / Tables / Feature Datasets) with a **Type** dropdown populated dynamically from the types actually present in the loaded connection
- Each option shows the type name and count (e.g. `Polygon (142)`, `Table (38)`, `All (500)`)
- Selecting a type immediately filters the results list; resets to "All" when a new connection is loaded
- Dropdown respects the existing light/dark theme via the `ThemedComboBox` style

---

## 2026-02-24

### Configurable metadata field order (`metadata_settings.json`)
- Added `metadata_settings.json` to control which metadata fields appear in the detail view and in what order
- File is placed at `%LocalAppData%\ProAppAddInSdeSearch\Cache\metadata_settings.json` on first launch; user can edit it to reorder, hide, or relabel any field
- Default order now shows Description before Summary

### Fix: raw HTML in metadata display
- ArcGIS metadata XML can embed HTML markup (`<DIV>`, `<P>`, `<SPAN>`, inline styles) inside description and summary text
- Added `StripHtml()` helper that strips all tags, decodes HTML entities, and preserves paragraph breaks — metadata now reads as plain text

### Seed cache info panel
- Added an **ℹ** button in the status bar (visible when template/seed data is in use)
- Clicking it opens a panel showing the connection the cache was generated from, the generation date, and a count breakdown (Feature Classes, Tables, Feature Datasets, Relationship Classes)

### Determinate loading progress bar
- During live database enumeration, the progress bar now shows a real fraction (e.g. `Feature Classes: 47 / 200`) instead of an indefinite spinner
- Total item count is pre-fetched before enumeration begins so the fraction is accurate from the start

### Fix: seed cache not updating on deployment
- Previously, deploying an updated `SeedCache.json` had no effect for users who already had a local copy — the add-in never replaced it
- Fixed by storing a `SeedCacheTimestamp` in the cache file; when the bundled seed is newer than the installed copy the stale file is automatically replaced on next launch

### Lazy field loading
- Field details (field list, types, domain values) are now loaded on demand when an item is selected, rather than being fetched and cached for every item up front
- Significantly reduces initial load time for large connections

### Inaccessible item notice
- When a cached item cannot be opened with the current connection (e.g. seed cache generated with schema-owner access, used by an OAuth user with narrower visibility), the detail view now shows an explanatory notice instead of silently showing no fields

---

## 2026-02-23

### Performance: filtering responsiveness
- Results list converted from `ScrollViewer > ItemsControl` to a `ListView` with `VirtualizingStackPanel` — only visible rows are rendered, eliminating layout cost for large result sets
- Type filtering and text search are now executed on a background thread with cancellation; rapid checkbox toggles or keystrokes no longer block the UI
- Pre-computed `NameText`, `MetaText`, `TagsText` index strings on each item replace repeated string concatenation during filtering

### Subtypes filter
- Added **Subtypes** checkbox to the Filters row
- Items with one or more subtypes defined show a **Subtypes** badge in the result list
- Cache version bumped to accommodate the new `HasSubtypes` field

### Fix: fields not showing in detail view
- Field definitions were not populating in the detail panel after the lazy-load refactor; fixed the loading path

---

## 2026-02-19 – 2026-02-20

### Editor tracking detection via SDK API
- Switched from heuristic field-name matching (`CREATED_USER`, `LAST_EDITED_DATE`, etc.) to the ArcGIS Pro SDK's `IsEditorTrackingEnabled` API property for accurate detection
- Eliminated false positives on tables that coincidentally had similarly named fields

### ArcGIS Pro Favorites folder scan
- The add-in now also scans `%AppData%\Esri\ArcGISPro\Favorites` for `.sde` files in addition to the project and `DatabaseConnections` folders

### Refresh button label
- The reload button next to the connection dropdown now shows a text label ("Refresh") alongside the ↻ icon to make its purpose clear

### PowerShell seed cache automation script
- Added `Update-SeedCache.ps1` to automate the process of regenerating `SeedCache.json` from a server connection (see `SEED_CACHE.md`)

---

## 2026-02-17 – 2026-02-18

### Fix: cache persistence across sessions
- Cache files were being rebuilt on every ArcGIS Pro launch due to a path normalisation mismatch; fixed so the JSON cache persists correctly between sessions

### Auto dark mode detection
- On first launch the add-in reads the Windows system theme and defaults to light or dark mode accordingly

### Editor Tracking and Archiving filters
- Added **Editor Tracking** and **Archiving** checkboxes to the Filters row
- Items with these flags enabled show colour-coded badge pills in the result list
- Filters stack with text search (AND logic)

### Collapsible domain codes list
- In the detail view, coded-value domains now show a collapsible **Domain Codes** expander listing every code/value pair

### Tags-only search mode
- Added a **Tags** checkbox to the search bar options; when enabled, text search matches only against ArcGIS metadata keyword tags

### Auto-exit detail view on filter change
- Applying a new search or toggling a filter automatically returns the user to the results list

### Feature dataset badges in result list
- Items belonging to a feature dataset now show the dataset name as a yellow badge below the item name in the results list

### Fix: editor tracking / archiving detection for SDE-qualified field names
- Detection was failing for tables whose field names included the owner prefix (e.g. `SDE.CREATED_USER`); fixed with case-insensitive suffix matching

---

## 2026-02-16

### Feature dataset membership mapping
- Used `GetRelatedDefinitions(DefinitionRelationshipType.DatasetInFeatureDataset)` to correctly associate feature classes with their parent feature datasets
- Cache versioning automatically invalidates old entries that lack the `FeatureDatasetName` field

### Fix: database connections not loading on startup
- SDE connections from the active project and `%AppData%` folder were not being enumerated when the pane first opened; fixed the initialization order

### UI visibility improvements
- Result list items made more compact; feature dataset folder badges, Editor Tracking and Archiving flag pills introduced
- Subtler secondary text for the dataset type / subtitle line

---

## 2026-02-13 — Initial Release

### Core features at launch
- **Browse SDE connections** — scans `.sde` files from the active ArcGIS Pro project, `{ProjectHome}\DatabaseConnections`, and `%AppData%\Esri\ArcGISPro\DatabaseConnections`
- **Search by name** — real-time text filter across all dataset names
- **Metadata search** — search ArcGIS item description, summary, tags, and credits
- **Type filtering** — show/hide Feature Classes, Tables, Feature Datasets
- **Detail view** — click any item to see full metadata, spatial reference, field count, and flags
- **Add to map** — adds a layer to the active map, or creates a new map automatically when none is open
- **Copy path** — copies the full SDE connection path to clipboard
- **JSON cache** — dataset list is serialised to `%LocalAppData%\ProAppAddInSdeSearch\Cache\` keyed by a MD5 hash of the connection path; subsequent opens are instantaneous
- **SeedCache.json** — bundled template cache for first-time users or deployments where live DB access is unavailable
- **Light / dark theme** — toggled via button in the header; palette applied via WPF `DynamicResource` keys
- **Dockable pane** — registered on the ArcGIS Pro **Add-In** tab in the **SDE Search** group
