# Seed Cache Feature

## Overview

The SDE Search add-in supports a **seed cache** feature that allows you to provide pre-loaded database metadata with your deployment. This eliminates the slow first-time loading experience for users.

## How It Works

1. **First Time Load** - If a user has no cache for a connection, the add-in looks for `SeedCache.json` in the add-in installation directory
2. **Automatic Copy** - The seed cache is automatically copied to the user's local cache directory
3. **User Notification** - Status bar shows: *"(template data - click ↻ to refresh from your database)"*
4. **Manual Refresh** - Users can click the "Reload Data" (↻) button to load fresh data from their actual database

## Creating a Seed Cache File

### Step 1: Generate the Cache

1. Open ArcGIS Pro and connect to your SDE database
2. Open the SDE Search dockpane
3. Select your connection and wait for all datasets to load
4. The cache will be automatically created at:
   ```
   %LocalAppData%\ProAppAddInSdeSearch\Cache\{ConnectionName}_{Hash}.json
   ```

### Step 2: Locate Your Cache File

Navigate to your cache directory:
```
C:\Users\{YourUser}\AppData\Local\ProAppAddInSdeSearch\Cache\
```

You'll see files like:
```
MyDatabase_A1B2C3D4.json
```

### Step 3: Rename and Deploy

1. **Copy** one of these cache files
2. **Rename** it to `SeedCache.json`
3. **Place** it in your add-in deployment directory (same folder as the .dll files)

## File Structure

The seed cache JSON contains:
```json
{
  "ConnectionPath": "path/to/connection.sde",
  "CachedAt": "2026-02-13T10:30:00Z",
  "Datasets": [
    {
      "Name": "Database.Schema.FeatureClass",
      "SimpleName": "FeatureClass",
      "DatasetType": "Feature Class",
      "GeometryType": "Polygon",
      "Description": "Metadata description...",
      "Tags": "tag1, tag2, tag3",
      ...
    }
  ]
}
```

## Deployment Structure

```
ProAppAddInSdeSearch/
├── ProAppAddInSdeSearch.dll
├── Config.daml
├── SeedCache.json          ← Place your seed cache here
└── ... other add-in files
```

## User Experience

### With Seed Cache:
- ✅ **Instant** first load (loads from template)
- ✅ Shows template notice in status bar
- ✅ User can refresh when ready to get their specific data

### Without Seed Cache:
- ⏱️ Slow first load (queries database)
- ✅ Subsequent loads are instant (uses generated cache)

## Best Practices

1. **Representative Data** - Use a cache from a typical production database
2. **Recent Data** - Update the seed cache periodically with your releases
3. **Metadata Completeness** - Generate the seed from a database with complete metadata/tags for best search results
4. **Documentation** - Let users know they're seeing template data and can refresh

## Notes

- Each connection gets its own cache file (based on connection path hash)
- The seed cache is only used if no user cache exists
- Once a user refreshes, their personal cache replaces the seed cache
- Users can always force refresh with the ↻ button
