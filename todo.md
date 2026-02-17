# SDE Search Add-In - Feature Requests / TODO

## Pending

### Domain box (collapsible list of domain codes and values)
- In the detail view, add a collapsible section for each field that has a domain
- Show domain codes and their corresponding values in a list/table
- Should collapse by default to keep the detail view clean

### Review where created and modified dates are coming from
- Currently sourced from **ArcGIS metadata XML** (not from the database/editor tracking fields)
- `CreatedDate` is parsed from `//CreaDate` or `//idinfo/citation/citeinfo/pubdate` XPath in the metadata XML
- `ModifiedDate` is parsed from `//ModDate` or `//metainfo/metd` XPath in the metadata XML
- These are the **metadata authoring dates** (when someone last edited the metadata), NOT the actual data creation/modification timestamps
- Consider sourcing from editor tracking fields (`CREATED_DATE`, `LAST_EDITED_DATE`) or the geodatabase catalog tables instead for more accurate dates

### Indicator for attribute rules on a feature
- Detect if a feature class or table has attribute rules defined
- Show an indicator/badge in the result list (similar to editor tracking icon)

### Indicator for editor tracking
- Already partially implemented: pencil icon next to name, "Editor Tracking" badge tag in results
- Ensure detection works reliably during initial enumeration (field-based: `CREATED_USER`, `CREATED_DATE`, `LAST_EDITED_USER`, `LAST_EDITED_DATE`)
