# TODO

## Export results

## pull sde files from favorites
## clean up cache folder of json files

## Show created/modified dates in the results list (partially done)

Dates are already parsed from metadata XML (`CreaDate`, `ModDate`) and displayed in the
detail view. Created date is now shown in the results list.

- [ ] Show `ModifiedDate` in the result list item (e.g. "Modified: 2025-03-14")
- [x] Show `CreatedDate` in the result list item
- [x] Handle missing dates gracefully (hide label when null)
- [x] Consider a relative format for recent dates ("2 days ago") and absolute for older ones
- [x] Parse dates during enumeration (not just detail view) so they appear in results list

## ~~Make feature dataset membership more obvious~~ ✅

~~`FeatureDatasetName` is tracked internally and used for path construction, but it is
**not surfaced in the results list UI**. Users can't tell whether an item belongs to a
feature dataset without opening the detail view.~~

- [x] Display the parent feature dataset name in the result list item (e.g. a row or badge below the subtitle)
- [x] Visually distinguish items inside a feature dataset from standalone items (indent, icon, color tag, etc.)
- [ ] Consider grouping results by feature dataset when the "Feature Datasets" filter is active

## ~~Make flags (Editor Tracking / Archiving) more visible~~ ✅

~~Flags currently display as abbreviated text ("ET", "Arch") appended to the end of the
subtitle line, which is easy to miss.~~

- [x] Replace abbreviations with small visual badges or colored tags (e.g. pill-shaped labels)
- [x] Use distinct colors or icons for each flag type
- [x] Show flags on their own row or as a `WrapPanel` of badges beneath the subtitle instead of inline text
- [x] Keep the full descriptions ("Editor Tracking", "Archiving") — avoid abbreviations

## Improve seedCache.json creation with progress indicator

Creating the seedCache.json file takes a long time to index. Users need visibility into the progress of this operation.

- [ ] Add a progress indicator or metric to show seedCache.json creation status
- [ ] Investigate faster indexing methods or optimizations

## Make design more compact (partially done)

The current design layout could be more space-efficient.

- [x] Compact the result list items (reduced padding, font sizes, icon sizes; removed metadata snippet preview)
- [ ] Audit remaining spacing, padding, and margins across other UI sections

## Expand search and filtering capabilities

Add more granular search options to help users find exactly what they need.

- [x] Search by tags only
- [ ] Search by field name
- [ ] Search by field value
- [ ] Search all features created within a date range
- [ ] Search feature classes for data created within date range

## Indicator for attribute rules on a feature

Detect if a feature class or table has attribute rules defined.

- [ ] Detect if a feature class or table has attribute rules defined
- [ ] Show an indicator/badge in the result list (similar to editor tracking icon)

