# TODO

<img width="1131" height="531" alt="image" src="https://github.com/user-attachments/assets/1ae1b307-e163-411c-900a-fc4d126af2f0" />


# Plan for group deployment and then HRM deployment
- [ ] Create deployment.md file. Consider how add-in updates will be given.

# Add an info. icon button at the bottom of the main window that opens to the cache data
# Add some fine print in the main header that says when the last time the add-in was updated/installed
# Optimization
## Clean up cache folder of json files


# New Functionality
## Filter by geometry type
## Use of wildcards in search
## CREATE MetadataSettings.json md file
## Remove html tags from metadata

## Export results

## Expand search and filtering capabilities

Add more granular search options to help users find exactly what they need.

- [ ] Search by field name
- [ ] Search by field value
- [ ] Search all features created within a date range
- [ ] Search feature classes for data created within date range


# Data changes
## Explore adding created/modified dates in the results list (on the feature itself, not just the metadata)

## Indicator for attribute rules on a feature

Detect if a feature class or table has attribute rules defined.

- [ ] Detect if a feature class or table has attribute rules defined
- [ ] Show an indicator/badge in the result list (similar to editor tracking icon)

## Subtype support

Surface subtype information in the results list and detail view.

- [x] Add a flag/marker in the result list to indicate when an item uses subtypes
- [ ] In the fields/domains detail section, show the different domains assigned per subtype when subtypes are present

## Query edit dates

Surface actual first-created and last-edited dates by querying editor tracking fields directly from the database.

- [ ] Re-implement "Query edit dates" checkbox in the Filters row
- [ ] On enable, query `MIN(CREATED_DATE)` and `MAX(LAST_EDITED_DATE)` for each dataset that has editor tracking
- [ ] Show "Data First Created" and "Data Last Edited" in the detail view metadata section
- [ ] Show `DataLastEditedDisplay` (relative format) in the results list subtitle row
- [ ] Persist user preference across sessions (datadates.txt)
- [ ] Investigate performance — querying is slow on large databases; consider async batch loading with progress indication


# UI Changes
## Change the icon for point geometry to a pushpin and the icon for tables to be a grid.
## Consider grouping results by feature dataset when the "Feature Datasets" filter is active

## SeedCachce
### Explore better ways to show SeedCache.json creation

Show when caching started and ended.

- [ ] Investigate faster indexing methods or optimizations

### Create window for cache information
- [ ] Show last updated
- [ ] From what connection the cache was generated from










