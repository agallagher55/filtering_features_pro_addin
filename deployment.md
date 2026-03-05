Shared Folder: R:\HRM Common Directory\FileSharing\Alex Gallagher\ArcGIS Pro\Add-ins\SDE Search\Prod

Recommended shared-folder setup
1) Create a dedicated UNC folder

Example:

\\YourServer\ArcGISProAddIns\Prod\

\\YourServer\ArcGISProAddIns\Test\

Keep these folders add-ins only (no docs, no zips). Esri specifically recommends isolating add-ins to avoid slow startup and weirdness.

2) Folder layout and versioning

Inside Prod, I like:

MyAddin.esriAddInX (current)

archive\MyAddin_1.2.2.esriAddInX

archive\MyAddin_1.2.1.esriAddInX

For updates:

Copy new build into archive first

Replace MyAddin.esriAddInX (atomic swap if you can)

Users restart Pro

3) Users add the folder in Pro

ArcGIS Pro → Settings → Add-In Manager → Options → Add Folder → point to \\...\Prod\

Pro will load these as Shared Add-Ins.

4) Permissions

Users: Read + Execute only

A small “release managers” group: Modify

Everyone else: no write access (prevents “who uploaded this mystery add-in?”)

5) Add-in security setting

Decide your org policy:

For internal rollouts, many orgs start with “load all add-ins”

If you want tighter control, plan to digitally sign your add-in and configure Pro to trust that publisher (more work up front, fewer surprises later).

Optional: make it zero-touch for users (IT-managed)

If you want the folder pre-configured for everyone, IT can register shared add-in folders via:
HKEY_LOCAL_MACHINE\SOFTWARE\ESRI\ArcGISPro\Settings\Add-In Folders

That’s the “nobody has to click Add Folder” approach.
