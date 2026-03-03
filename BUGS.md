# Known Bugs

## Blank startup dialog on first launch

**Symptom**: ArcGIS Pro shows a small empty dialog with a spinner and "More info" when the add-in starts.

**Likely cause**: An older `.esriAddinX` package is still installed (or cached) from before the DAML AddInInfo metadata fix.

**Resolution**:

1. Close ArcGIS Pro.
2. Remove the old add-in folder for this add-in ID:
   `%UserProfile%\Documents\ArcGIS\AddIns\ArcGISPro\{a73f5d21-b8e4-4c9a-a620-7e3d1f6c8b55}`
3. Install a freshly built `.esriAddinX` from the current source.
4. If the popup remains, clear `%LocalAppData%\ESRI\ArcGISPro\AssemblyCache` and relaunch ArcGIS Pro.

**Note on OneDrive**: ArcGIS Pro installs user add-ins under the current Windows **Documents** location. If Documents is redirected to OneDrive, the add-in folder will be under OneDrive as well.

<img width="504" height="773" alt="Blank startup dialog" src="https://github.com/user-attachments/assets/b2ff46f0-9540-4549-8fd4-024aa119f93e" />

