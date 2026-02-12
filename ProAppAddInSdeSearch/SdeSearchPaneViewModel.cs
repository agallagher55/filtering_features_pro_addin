using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProAppAddInSdeSearch
{
    internal class SdeSearchPaneViewModel : DockPane
    {
        private const string _dockPaneID = "ProAppAddInSdeSearch_Dockpane1";

        // Search state
        private string _searchText = "";
        private bool _searchByName = true;
        private bool _searchByMetadata = false; // OFF by default
        private bool _isSearching;
        private string _statusText = "Select a connection to browse SDE items";
        private string _progressText = "";
        private int _resultCount;

        // Connection state
        private ObservableCollection<SdeConnectionItem> _connections = new ObservableCollection<SdeConnectionItem>();
        private SdeConnectionItem _selectedConnection;
        private string _manualSdePath = "";

        // Full unfiltered dataset cache (loaded once per connection)
        private List<SdeDatasetItem> _allDatasets = new List<SdeDatasetItem>();

        // Results
        private ObservableCollection<SdeDatasetItem> _searchResults = new ObservableCollection<SdeDatasetItem>();
        private SdeDatasetItem _selectedResult;

        // Detail view
        private bool _showDetails;
        private ObservableCollection<FieldInfo> _detailFields = new ObservableCollection<FieldInfo>();
        private string _detailMetadata = "";
        private bool _isLoadingDetails;

        // Filter
        private bool _showFeatureClasses = true;
        private bool _showTables = true;
        private bool _showFeatureDatasets = true;

        protected SdeSearchPaneViewModel()
        {
            SearchCommand = new RelayCommand(() => ApplyFilterAndSearch(), () => !IsSearching);
            ClearCommand = new RelayCommand(() => ClearSearch(), () => true);
            RefreshConnectionsCommand = new RelayCommand(() => _ = LoadConnections(), () => !IsSearching);
            ReloadDataCommand = new RelayCommand(() => _ = LoadAllDatasets(), () => !IsSearching && SelectedConnection != null);
            AddToMapCommand = new RelayCommand(() => _ = AddSelectedToMap(), () => SelectedResult != null && SelectedResult.CanAddToMap);
            BackToResultsCommand = new RelayCommand(() => ShowDetails = false, () => ShowDetails);
            CopyPathCommand = new RelayCommand(() => CopySelectedPath(), () => SelectedResult != null);
            BrowseSdeCommand = new RelayCommand(() => BrowseForSdeFile(), () => !IsSearching);
            AddManualPathCommand = new RelayCommand(() => AddManualSdePath(), () => !IsSearching && !string.IsNullOrWhiteSpace(ManualSdePath));
        }

        #region Commands
        public ICommand SearchCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand RefreshConnectionsCommand { get; }
        public ICommand ReloadDataCommand { get; }
        public ICommand AddToMapCommand { get; }
        public ICommand BackToResultsCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand BrowseSdeCommand { get; }
        public ICommand AddManualPathCommand { get; }
        #endregion

        #region Properties

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public bool SearchByName
        {
            get => _searchByName;
            set
            {
                if (SetProperty(ref _searchByName, value))
                    ApplyFilterAndSearch();
            }
        }

        public bool SearchByMetadata
        {
            get => _searchByMetadata;
            set
            {
                if (SetProperty(ref _searchByMetadata, value))
                    ApplyFilterAndSearch();
            }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                if (SetProperty(ref _isSearching, value))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        CommandManager.InvalidateRequerySuggested());
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public int ResultCount
        {
            get => _resultCount;
            set => SetProperty(ref _resultCount, value);
        }

        public ObservableCollection<SdeConnectionItem> Connections
        {
            get => _connections;
            set => SetProperty(ref _connections, value);
        }

        public SdeConnectionItem SelectedConnection
        {
            get => _selectedConnection;
            set
            {
                if (SetProperty(ref _selectedConnection, value))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        CommandManager.InvalidateRequerySuggested());

                    // Auto-load datasets when connection is selected
                    if (_selectedConnection != null)
                        _ = LoadAllDatasets();
                }
            }
        }

        public string ManualSdePath
        {
            get => _manualSdePath;
            set
            {
                if (SetProperty(ref _manualSdePath, value))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        CommandManager.InvalidateRequerySuggested());
                }
            }
        }

        public ObservableCollection<SdeDatasetItem> SearchResults
        {
            get => _searchResults;
            set => SetProperty(ref _searchResults, value);
        }

        public SdeDatasetItem SelectedResult
        {
            get => _selectedResult;
            set
            {
                if (SetProperty(ref _selectedResult, value))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        CommandManager.InvalidateRequerySuggested());
                }
            }
        }

        public bool ShowDetails
        {
            get => _showDetails;
            set
            {
                if (SetProperty(ref _showDetails, value))
                    NotifyPropertyChanged(nameof(ShowResultsList));
            }
        }

        public bool ShowResultsList => !ShowDetails;

        public ObservableCollection<FieldInfo> DetailFields
        {
            get => _detailFields;
            set => SetProperty(ref _detailFields, value);
        }

        public string DetailMetadata
        {
            get => _detailMetadata;
            set => SetProperty(ref _detailMetadata, value);
        }

        public bool IsLoadingDetails
        {
            get => _isLoadingDetails;
            set => SetProperty(ref _isLoadingDetails, value);
        }

        public bool ShowFeatureClasses
        {
            get => _showFeatureClasses;
            set
            {
                if (SetProperty(ref _showFeatureClasses, value))
                    ApplyFilterAndSearch();
            }
        }

        public bool ShowTables
        {
            get => _showTables;
            set
            {
                if (SetProperty(ref _showTables, value))
                    ApplyFilterAndSearch();
            }
        }

        public bool ShowFeatureDatasets
        {
            get => _showFeatureDatasets;
            set
            {
                if (SetProperty(ref _showFeatureDatasets, value))
                    ApplyFilterAndSearch();
            }
        }

        #endregion

        #region Connection Loading

        /// <summary>
        /// Discover enterprise geodatabase connections from the project + common folders
        /// </summary>
        private async Task LoadConnections()
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    var connectionItems = new List<SdeConnectionItem>();

                    // 1) Project GDB items
                    ReportProgress("Scanning project connections...");
                    var projectItems = Project.Current.GetItems<GDBProjectItem>();
                    foreach (var item in projectItems)
                    {
                        try
                        {
                            var path = item.Path;
                            if (!string.IsNullOrEmpty(path) &&
                                (path.EndsWith(".sde", StringComparison.OrdinalIgnoreCase) ||
                                 path.Contains("DatabaseConnections", StringComparison.OrdinalIgnoreCase)))
                            {
                                connectionItems.Add(new SdeConnectionItem
                                {
                                    Name = item.Name,
                                    Path = path,
                                    ConnectionType = "Project"
                                });
                            }
                        }
                        catch { }
                    }

                    // 2) Project home DatabaseConnections subfolder
                    ScanFolderForSde(connectionItems,
                        System.IO.Path.Combine(Project.Current.HomeFolderPath, "DatabaseConnections"),
                        "Project Folder");

                    // 3) ArcGIS Pro roaming profile DatabaseConnections
                    ScanFolderForSde(connectionItems,
                        System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "Esri", "ArcGISPro", "DatabaseConnections"),
                        "ArcGIS Pro");

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Preserve any manually-added connections
                        var manualConns = Connections
                            .Where(c => c.ConnectionType == "Manual")
                            .ToList();

                        Connections.Clear();
                        foreach (var conn in connectionItems.OrderBy(c => c.Name))
                            Connections.Add(conn);
                        foreach (var mc in manualConns)
                        {
                            if (!Connections.Any(c => c.Path.Equals(mc.Path, StringComparison.OrdinalIgnoreCase)))
                                Connections.Add(mc);
                        }

                        StatusText = Connections.Count > 0
                            ? $"{Connections.Count} connection(s) found — select one to browse"
                            : "No connections found. Browse for an .sde file below.";

                        ProgressText = "";

                        if (Connections.Count == 1)
                            SelectedConnection = Connections.First();
                    });
                }
                catch (Exception ex)
                {
                    ReportStatus($"Error loading connections: {ex.Message}");
                }
            });
        }

        private void ScanFolderForSde(List<SdeConnectionItem> items, string folder, string source)
        {
            try
            {
                if (!System.IO.Directory.Exists(folder)) return;
                foreach (var sdeFile in System.IO.Directory.GetFiles(folder, "*.sde"))
                {
                    var name = System.IO.Path.GetFileNameWithoutExtension(sdeFile);
                    if (!items.Any(c => c.Path.Equals(sdeFile, StringComparison.OrdinalIgnoreCase)))
                    {
                        items.Add(new SdeConnectionItem
                        {
                            Name = name,
                            Path = sdeFile,
                            ConnectionType = source
                        });
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Open a file browse dialog for .sde files
        /// </summary>
        private void BrowseForSdeFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Enterprise Geodatabase Connection",
                Filter = "SDE Connection Files (*.sde)|*.sde|All Files (*.*)|*.*",
                DefaultExt = ".sde",
                CheckFileExists = true
            };

            // Start in common ArcGIS folder if available
            var arcgisDb = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Esri", "ArcGISPro", "DatabaseConnections");
            if (System.IO.Directory.Exists(arcgisDb))
                dialog.InitialDirectory = arcgisDb;

            if (dialog.ShowDialog() == true)
            {
                AddConnectionFromPath(dialog.FileName);
            }
        }

        /// <summary>
        /// Add a manually-typed .sde path
        /// </summary>
        private void AddManualSdePath()
        {
            var path = ManualSdePath?.Trim().Trim('"'); // strip quotes if pasted
            if (string.IsNullOrWhiteSpace(path)) return;

            if (!System.IO.File.Exists(path))
            {
                StatusText = "File not found: " + path;
                return;
            }

            if (!path.EndsWith(".sde", StringComparison.OrdinalIgnoreCase))
            {
                StatusText = "Please select a .sde connection file";
                return;
            }

            AddConnectionFromPath(path);
            ManualSdePath = "";
        }

        private void AddConnectionFromPath(string path)
        {
            // Don't duplicate
            var existing = Connections.FirstOrDefault(c => c.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                SelectedConnection = existing;
                return;
            }

            var conn = new SdeConnectionItem
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(path),
                Path = path,
                ConnectionType = "Manual"
            };
            Connections.Add(conn);
            SelectedConnection = conn;
        }

        #endregion

        #region Dataset Loading (fast — no metadata)

        /// <summary>
        /// Load all datasets from the selected connection.
        /// Skips metadata for speed — metadata loaded lazily on detail click.
        /// </summary>
        private async Task LoadAllDatasets()
        {
            if (SelectedConnection == null) return;

            IsSearching = true;
            ShowDetails = false;
            _allDatasets.Clear();

            var connectionPath = SelectedConnection.Path;
            var connectionName = SelectedConnection.Name;

            ReportProgress("Connecting to " + connectionName + "...");
            ReportStatus("Connecting...");

            await QueuedTask.Run(() =>
            {
                try
                {
                    using (var geodatabase = new Geodatabase(new DatabaseConnectionFile(new Uri(connectionPath))))
                    {
                        int total = 0;

                        // ── Feature Datasets ──────────────────────────
                        ReportProgress("Enumerating feature datasets...");
                        try
                        {
                            var fdDefs = geodatabase.GetDefinitions<FeatureDatasetDefinition>();
                            foreach (var fdDef in fdDefs)
                            {
                                try
                                {
                                    _allDatasets.Add(new SdeDatasetItem
                                    {
                                        Name = fdDef.GetName(),
                                        SimpleName = GetSimpleName(fdDef.GetName()),
                                        DatasetType = "Feature Dataset",
                                        TypeIcon = "📁",
                                        CanAddToMap = false,
                                        ConnectionPath = connectionPath
                                    });
                                    total++;
                                    if (total % 20 == 0)
                                        ReportProgress($"Found {total} items so far (feature datasets)...");
                                }
                                catch { }
                            }
                        }
                        catch { }

                        ReportProgress($"Found {total} feature dataset(s). Enumerating feature classes...");

                        // ── Feature Classes ───────────────────────────
                        try
                        {
                            var fcDefs = geodatabase.GetDefinitions<FeatureClassDefinition>();
                            int fcCount = 0;
                            foreach (var fcDef in fcDefs)
                            {
                                try
                                {
                                    var item = new SdeDatasetItem
                                    {
                                        Name = fcDef.GetName(),
                                        SimpleName = GetSimpleName(fcDef.GetName()),
                                        DatasetType = "Feature Class",
                                        TypeIcon = GetGeometryTypeIcon(fcDef.GetShapeType()),
                                        CanAddToMap = true,
                                        ConnectionPath = connectionPath,
                                        FieldCount = fcDef.GetFields().Count
                                    };

                                    try
                                    {
                                        item.GeometryType = fcDef.GetShapeType().ToString();
                                        var sr = fcDef.GetSpatialReference();
                                        if (sr != null) item.SpatialReference = sr.Name;
                                    }
                                    catch { }

                                    try { item.AliasName = fcDef.GetAliasName(); } catch { }

                                    _allDatasets.Add(item);
                                    total++;
                                    fcCount++;
                                    if (fcCount % 25 == 0)
                                        ReportProgress($"Found {total} items ({fcCount} feature classes)...");
                                }
                                catch { }
                            }
                        }
                        catch { }

                        ReportProgress($"Found {total} items. Enumerating tables...");

                        // ── Tables (exclude FCs) ──────────────────────
                        try
                        {
                            var fcNames = new HashSet<string>(
                                _allDatasets
                                    .Where(d => d.DatasetType == "Feature Class")
                                    .Select(d => d.Name),
                                StringComparer.OrdinalIgnoreCase);

                            var tableDefs = geodatabase.GetDefinitions<TableDefinition>();
                            int tblCount = 0;
                            foreach (var tableDef in tableDefs)
                            {
                                try
                                {
                                    if (fcNames.Contains(tableDef.GetName()))
                                        continue;

                                    var item = new SdeDatasetItem
                                    {
                                        Name = tableDef.GetName(),
                                        SimpleName = GetSimpleName(tableDef.GetName()),
                                        DatasetType = "Table",
                                        TypeIcon = "📋",
                                        CanAddToMap = true,
                                        ConnectionPath = connectionPath,
                                        FieldCount = tableDef.GetFields().Count
                                    };

                                    try { item.AliasName = tableDef.GetAliasName(); } catch { }

                                    _allDatasets.Add(item);
                                    total++;
                                    tblCount++;
                                    if (tblCount % 25 == 0)
                                        ReportProgress($"Found {total} items ({tblCount} tables)...");
                                }
                                catch { }
                            }
                        }
                        catch { }

                        // ── Relationship Classes ──────────────────────
                        ReportProgress($"Found {total} items. Enumerating relationship classes...");
                        try
                        {
                            var relDefs = geodatabase.GetDefinitions<RelationshipClassDefinition>();
                            foreach (var relDef in relDefs)
                            {
                                try
                                {
                                    _allDatasets.Add(new SdeDatasetItem
                                    {
                                        Name = relDef.GetName(),
                                        SimpleName = GetSimpleName(relDef.GetName()),
                                        DatasetType = "Relationship Class",
                                        TypeIcon = "🔗",
                                        CanAddToMap = false,
                                        ConnectionPath = connectionPath,
                                        MetadataSnippet = $"Origin: {GetSimpleName(relDef.GetOriginClass())} → Dest: {GetSimpleName(relDef.GetDestinationClass())}"
                                    });
                                    total++;
                                }
                                catch { }
                            }
                        }
                        catch { }

                        ReportProgress($"Loaded {total} items. Sorting...");

                        // Sort
                        _allDatasets = _allDatasets
                            .OrderBy(r => r.DatasetType switch
                            {
                                "Feature Class" => 0,
                                "Table" => 1,
                                "Feature Dataset" => 2,
                                "Relationship Class" => 3,
                                _ => 4
                            })
                            .ThenBy(r => r.SimpleName)
                            .ToList();

                        // Tally
                        int fcs = _allDatasets.Count(d => d.DatasetType == "Feature Class");
                        int tbls = _allDatasets.Count(d => d.DatasetType == "Table");
                        int fds = _allDatasets.Count(d => d.DatasetType == "Feature Dataset");
                        int rels = _allDatasets.Count(d => d.DatasetType == "Relationship Class");

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ApplyFilterAndSearch();
                            IsSearching = false;
                            ProgressText = "";
                            StatusText = $"{connectionName}: {fcs} feature classes, {tbls} tables, {fds} datasets, {rels} relationships";
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Load error: {ex.Message}");
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsSearching = false;
                        ProgressText = "";
                        StatusText = $"Connection error: {ex.Message}";
                    });
                }
            });
        }

        #endregion

        #region Search / Filter (instant — works on cached data)

        /// <summary>
        /// Filter the cached dataset list by search text + type toggles.
        /// This is instant since all data is already in memory.
        /// </summary>
        private void ApplyFilterAndSearch()
        {
            string searchTerm = (SearchText ?? "").Trim();
            bool isWildcard = string.IsNullOrEmpty(searchTerm);

            var filtered = _allDatasets.Where(item =>
            {
                // Type filter
                if (!ShowFeatureClasses && item.DatasetType == "Feature Class") return false;
                if (!ShowTables && item.DatasetType == "Table") return false;
                if (!ShowFeatureDatasets && item.DatasetType == "Feature Dataset") return false;

                // Text search
                if (isWildcard) return true;
                return MatchesSearch(item, searchTerm);
            }).ToList();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SearchResults.Clear();
                foreach (var r in filtered)
                    SearchResults.Add(r);
                ResultCount = filtered.Count;

                if (!isWildcard)
                    StatusText = $"Found {filtered.Count} of {_allDatasets.Count} items matching \"{searchTerm}\"";
            });
        }

        /// <summary>
        /// Check if a dataset matches the search terms (AND logic for multiple words)
        /// </summary>
        private bool MatchesSearch(SdeDatasetItem item, string searchTerm)
        {
            var terms = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var term in terms)
            {
                bool termMatched = false;

                if (SearchByName)
                {
                    if (item.Name?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.SimpleName?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.AliasName?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        termMatched = true;
                    }
                }

                if (!termMatched && SearchByMetadata)
                {
                    // Only match against metadata that has already been loaded
                    if (item.Description?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.Summary?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.Tags?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.Purpose?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.Credits?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.MetadataSnippet?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.DatasetType?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        termMatched = true;
                    }
                }

                if (!termMatched) return false;
            }
            return true;
        }

        #endregion

        #region Item Details (lazy metadata load)

        /// <summary>
        /// Load detailed info for a dataset: fields + metadata (loaded on demand)
        /// </summary>
        internal async Task LoadItemDetails(SdeDatasetItem item)
        {
            if (item == null) return;

            SelectedResult = item;
            IsLoadingDetails = true;
            ShowDetails = true;

            await QueuedTask.Run(() =>
            {
                try
                {
                    var fields = new List<FieldInfo>();

                    using (var geodatabase = new Geodatabase(new DatabaseConnectionFile(new Uri(item.ConnectionPath))))
                    {
                        TableDefinition tableDef = null;

                        if (item.DatasetType == "Feature Class")
                        {
                            try { tableDef = geodatabase.GetDefinition<FeatureClassDefinition>(item.Name); } catch { }
                        }
                        else if (item.DatasetType == "Table")
                        {
                            try { tableDef = geodatabase.GetDefinition<TableDefinition>(item.Name); } catch { }
                        }

                        if (tableDef != null)
                        {
                            // ── Load metadata lazily ──────────────────
                            if (!item.HasMetadata)
                            {
                                TryLoadMetadata(item, tableDef);
                            }

                            // ── Load fields ───────────────────────────
                            foreach (var field in tableDef.GetFields())
                            {
                                var fi = new FieldInfo
                                {
                                    Name = field.Name,
                                    AliasName = field.AliasName ?? field.Name,
                                    FieldType = field.FieldType.ToString(),
                                    Length = field.Length,
                                    IsNullable = field.IsNullable,
                                    IsEditable = field.IsEditable,
                                    TypeIcon = GetFieldTypeIcon(field.FieldType)
                                };

                                var domain = field.GetDomain();
                                if (domain != null)
                                {
                                    fi.DomainName = domain.GetName();
                                    if (domain is CodedValueDomain cvd)
                                    {
                                        fi.DomainType = "Coded Value";
                                        var pairs = cvd.GetCodedValuePairs();
                                        fi.DomainInfo = string.Join(", ",
                                            pairs.Take(5).Select(kv => $"{kv.Key}={kv.Value}"));
                                        if (pairs.Count > 5)
                                            fi.DomainInfo += $" (+{pairs.Count - 5} more)";
                                    }
                                    else if (domain is RangeDomain rd)
                                    {
                                        fi.DomainType = "Range";
                                        fi.DomainInfo = $"{rd.GetMinValue()} – {rd.GetMaxValue()}";
                                    }
                                }

                                try
                                {
                                    var defaultVal = field.GetDefaultValue();
                                    if (defaultVal != null)
                                        fi.DefaultValue = defaultVal.ToString();
                                }
                                catch { }

                                fields.Add(fi);
                            }
                        }
                        else if (item.DatasetType == "Feature Dataset")
                        {
                            // Load feature dataset metadata
                            try
                            {
                                var fdDef = geodatabase.GetDefinition<FeatureDatasetDefinition>(item.Name);
                                if (!item.HasMetadata)
                                    TryLoadMetadata(item, fdDef);
                            }
                            catch { }
                        }
                    }

                    var metadataDisplay = BuildMetadataDisplay(item);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        DetailFields.Clear();
                        foreach (var f in fields)
                            DetailFields.Add(f);

                        DetailMetadata = metadataDisplay;
                        IsLoadingDetails = false;
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading details: {ex.Message}");
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        DetailMetadata = $"Error loading details: {ex.Message}";
                        IsLoadingDetails = false;
                    });
                }
            });
        }

        private string BuildMetadataDisplay(SdeDatasetItem item)
        {
            var parts = new List<string>();

            parts.Add($"Full Name: {item.Name}");
            parts.Add($"Type: {item.DatasetType}");

            if (!string.IsNullOrWhiteSpace(item.AliasName) && item.AliasName != item.SimpleName)
                parts.Add($"Alias: {item.AliasName}");

            if (!string.IsNullOrWhiteSpace(item.GeometryType))
                parts.Add($"Geometry: {item.GeometryType}");

            if (!string.IsNullOrWhiteSpace(item.SpatialReference))
                parts.Add($"Spatial Reference: {item.SpatialReference}");

            if (item.FieldCount > 0)
                parts.Add($"Fields: {item.FieldCount}");

            if (item.HasMetadata)
            {
                if (!string.IsNullOrWhiteSpace(item.Description))
                    parts.Add($"\nDescription:\n{item.Description}");

                if (!string.IsNullOrWhiteSpace(item.Summary) && item.Summary != item.Description)
                    parts.Add($"\nSummary:\n{item.Summary}");

                if (!string.IsNullOrWhiteSpace(item.Purpose) && item.Purpose != item.Summary && item.Purpose != item.Description)
                    parts.Add($"\nPurpose:\n{item.Purpose}");

                if (!string.IsNullOrWhiteSpace(item.Tags))
                    parts.Add($"\nTags: {item.Tags}");

                if (!string.IsNullOrWhiteSpace(item.Credits))
                    parts.Add($"\nCredits: {item.Credits}");

                if (!string.IsNullOrWhiteSpace(item.UseConstraints))
                    parts.Add($"\nUse Constraints: {item.UseConstraints}");
            }
            else
            {
                parts.Add("\n(No metadata available for this item)");
            }

            return string.Join("\n", parts);
        }

        /// <summary>
        /// Parse XML metadata from a definition
        /// </summary>

        private void TryLoadMetadata(SdeDatasetItem item, Definition definition)
        {
            try
            {
                // Build a catalog path like: C:\path\connection.sde\OWNER.DATASET
                var datasetPath = System.IO.Path.Combine(item.ConnectionPath, item.Name);

                // Item.GetXml() returns the full ArcGIS metadata XML for the item (call on the MCT).
                var catalogItem = ItemFactory.Instance.Create(datasetPath);
                var metadataXml = catalogItem?.GetXml();

                if (string.IsNullOrEmpty(metadataXml))
                    return;

                item.HasMetadata = true;
                item.RawMetadataXml = metadataXml;
                ParseMetadataXml(item, metadataXml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Metadata error for {item.Name}: {ex.Message}");
            }
        }


        private void ParseMetadataXml(SdeDatasetItem item, string xml)
        {
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(xml);

                item.Description = GetXmlText(doc, "//dataIdInfo/idAbs") ??
                                   GetXmlText(doc, "//dataIdInfo/idPurp") ??
                                   GetXmlText(doc, "//idinfo/descript/abstract") ?? "";

                item.Summary = GetXmlText(doc, "//dataIdInfo/idPurp") ??
                               GetXmlText(doc, "//idinfo/descript/purpose") ?? "";

                item.Purpose = GetXmlText(doc, "//dataIdInfo/idPurp") ?? "";

                item.Credits = GetXmlText(doc, "//dataIdInfo/idCredit") ??
                               GetXmlText(doc, "//idinfo/datacred") ?? "";

                var tags = new List<string>();
                var themeNodes = doc.SelectNodes("//dataIdInfo/searchKeys/keyword");
                if (themeNodes != null)
                    foreach (System.Xml.XmlNode node in themeNodes)
                        if (!string.IsNullOrWhiteSpace(node.InnerText))
                            tags.Add(node.InnerText.Trim());

                var fgdcTheme = doc.SelectNodes("//idinfo/keywords/theme/themekey");
                if (fgdcTheme != null)
                    foreach (System.Xml.XmlNode node in fgdcTheme)
                        if (!string.IsNullOrWhiteSpace(node.InnerText) &&
                            !tags.Contains(node.InnerText.Trim(), StringComparer.OrdinalIgnoreCase))
                            tags.Add(node.InnerText.Trim());

                item.Tags = string.Join(", ", tags);

                item.UseConstraints = GetXmlText(doc, "//dataIdInfo/resConst/Consts/useLimit") ??
                                      GetXmlText(doc, "//idinfo/useconst") ?? "";

                // Build snippet for list display
                var snippetParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(item.Description))
                {
                    var desc = item.Description.Length > 120
                        ? item.Description.Substring(0, 120) + "…"
                        : item.Description;
                    snippetParts.Add(desc);
                }
                if (!string.IsNullOrWhiteSpace(item.Tags))
                    snippetParts.Add($"Tags: {item.Tags}");

                if (snippetParts.Count > 0)
                    item.MetadataSnippet = string.Join(" | ", snippetParts);
            }
            catch { }
        }

        private string GetXmlText(System.Xml.XmlDocument doc, string xpath)
        {
            try
            {
                var node = doc.SelectSingleNode(xpath);
                if (node != null && !string.IsNullOrWhiteSpace(node.InnerText))
                    return node.InnerText.Trim();
            }
            catch { }
            return null;
        }

        #endregion

        #region Map Operations

        private async Task AddSelectedToMap()
        {
            if (SelectedResult == null || !SelectedResult.CanAddToMap) return;

            var item = SelectedResult;
            StatusText = $"Adding {item.SimpleName} to map...";

            await QueuedTask.Run(() =>
            {
                try
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                    {
                        ReportStatus("No active map view. Open a map first.");
                        return;
                    }

                    if (item.DatasetType == "Feature Class")
                    {
                        LayerFactory.Instance.CreateLayer(
                            new Uri(item.ConnectionPath + "\\" + item.Name), map);
                    }
                    else if (item.DatasetType == "Table")
                    {
                        StandaloneTableFactory.Instance.CreateStandaloneTable(
                            new Uri(item.ConnectionPath + "\\" + item.Name), map);
                    }

                    ReportStatus($"✓ Added \"{item.SimpleName}\" to map");
                }
                catch (Exception ex)
                {
                    ReportStatus($"Error adding to map: {ex.Message}");
                }
            });
        }

        private void CopySelectedPath()
        {
            if (SelectedResult == null) return;
            try
            {
                System.Windows.Clipboard.SetText($"{SelectedResult.ConnectionPath}\\{SelectedResult.Name}");
                StatusText = "Copied path to clipboard";
            }
            catch (Exception ex)
            {
                StatusText = $"Error copying: {ex.Message}";
            }
        }

        #endregion

        #region Helpers

        private void ClearSearch()
        {
            SearchText = "";
            ApplyFilterAndSearch();
            ShowDetails = false;
            StatusText = _allDatasets.Count > 0
                ? $"Showing all {_allDatasets.Count} items"
                : "Select a connection to browse SDE items";
        }

        private void ReportProgress(string text)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressText = text;
            });
        }

        private void ReportStatus(string text)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = text;
            });
        }

        private string GetSimpleName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return fullName;
            var lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
        }

        private string GetGeometryTypeIcon(ArcGIS.Core.Geometry.GeometryType geomType)
        {
            return geomType switch
            {
                ArcGIS.Core.Geometry.GeometryType.Point => "📍",
                ArcGIS.Core.Geometry.GeometryType.Multipoint => "📍",
                ArcGIS.Core.Geometry.GeometryType.Polyline => "〰️",
                ArcGIS.Core.Geometry.GeometryType.Polygon => "⬡",
                ArcGIS.Core.Geometry.GeometryType.Multipatch => "🔶",
                _ => "🗺️"
            };
        }

        private string GetFieldTypeIcon(FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.String => "Abc",
                FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger => "123",
                FieldType.Double or FieldType.Single => "1.2",
                FieldType.Date or FieldType.DateOnly or FieldType.TimeOnly or FieldType.TimestampOffset => "📅",
                FieldType.Geometry => "📐",
                FieldType.OID => "🔑",
                FieldType.GlobalID or FieldType.GUID => "🆔",
                FieldType.Blob => "📦",
                FieldType.Raster => "🖼",
                _ => "•"
            };
        }

        #endregion

        #region Lifecycle

        protected override Task InitializeAsync()
        {
            _ = LoadConnections();
            return base.InitializeAsync();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null) return;
            pane.Activate();
        }

        #endregion
    }

    #region Data Models

    public class SdeConnectionItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string ConnectionType { get; set; }

        public string DisplayName => ConnectionType == "Manual"
            ? $"{Name}  (manual)"
            : Name;

        public override string ToString() => DisplayName;

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class SdeDatasetItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; }
        public string SimpleName { get; set; }
        public string AliasName { get; set; }
        public string DatasetType { get; set; }
        public string TypeIcon { get; set; }
        public string ConnectionPath { get; set; }
        public bool CanAddToMap { get; set; }
        public int FieldCount { get; set; }
        public string GeometryType { get; set; }
        public string SpatialReference { get; set; }

        // Metadata (lazy-loaded)
        public bool HasMetadata { get; set; }
        public string Description { get; set; }
        public string Summary { get; set; }
        public string Purpose { get; set; }
        public string Tags { get; set; }
        public string Credits { get; set; }
        public string UseConstraints { get; set; }
        public string MetadataSnippet { get; set; }
        public string RawMetadataXml { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public string Subtitle
        {
            get
            {
                var parts = new List<string> { DatasetType };
                if (!string.IsNullOrWhiteSpace(GeometryType))
                    parts.Add(GeometryType);
                if (FieldCount > 0)
                    parts.Add($"{FieldCount} fields");
                return string.Join(" · ", parts);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class FieldInfo : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string AliasName { get; set; }
        public string FieldType { get; set; }
        public string TypeIcon { get; set; }
        public int Length { get; set; }
        public bool IsNullable { get; set; }
        public bool IsEditable { get; set; }
        public string DomainName { get; set; }
        public string DomainType { get; set; }
        public string DomainInfo { get; set; }
        public string DefaultValue { get; set; }

        public bool HasDomain => !string.IsNullOrEmpty(DomainName);

        public string FieldSummary
        {
            get
            {
                var parts = new List<string> { FieldType };
                if (Length > 0 && FieldType == "String")
                    parts.Add($"({Length})");
                if (!IsNullable)
                    parts.Add("NOT NULL");
                if (HasDomain)
                    parts.Add($"[{DomainName}]");
                return string.Join(" ", parts);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    #endregion

    #region Commands

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();
    }

    internal class Dockpane1_ShowButton : Button
    {
        protected override void OnClick()
        {
            SdeSearchPaneViewModel.Show();
        }
    }

    #endregion
}
