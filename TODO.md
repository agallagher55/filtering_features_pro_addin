# TODO

# Optimization
## Clean up cache folder of json files


# New Functionality
## Re-order metadata in detail view

- [ ] Move the description field to appear before the summary in the detail view metadata section

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
## Consider grouping results by feature dataset when the "Feature Datasets" filter is active

## SeedCachce
### Explore better ways to show SeedCache.json creation

Creating the seedCache.json file takes a long time to index. Users need visibility into the progress of this operation.
A progress bar shows users that something is happening, but only a basic number of feature datasets, features, tables, etc. is given, but no context out of how many feature datasets, features, tables, etc. It would be nice to have an indication of a true sense of progress

- [ ] Add a progress indicator or metric to show seedCache.json creation status
- [ ] Investigate faster indexing methods or optimizations

### Create window for cache information
- [ ] Show last updated
- [ ] From what connection the cache was generated from

