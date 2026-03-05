For updates:
Copy new build into archive first
Replace MyAddin.esriAddInX (atomic swap if you can)
Users restart Pro

3) Users add the folder in Pro

ArcGIS Pro → Settings → Add-In Manager → Options → Add Folder → point to \\...\Prod\

Pro will load these as Shared Add-Ins.

If you want the folder pre-configured for everyone, IT can register shared add-in folders via:
HKEY_LOCAL_MACHINE\SOFTWARE\ESRI\ArcGISPro\Settings\Add-In Folders
