using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProAppAddInSdeSearch
{
    internal class SdeSearchPaneViewModel : DockPane
    {
        private const string _dockPaneID = "ProAppAddInSdeSearch_Dockpane1";

        // ── Search state ──────────────────────────────
        private string _searchText = "";
        private bool _searchByName = true;
        private bool _searchByMetadata = false;
        private bool _searchByTags = false;
        private bool _isSearching;
        private string _statusText = "Select a connection to browse SDE items";
        private string _progressText = "";
        private int _resultCount;

        // ── Theme ─────────────────────────────────────
        private bool _isDarkMode = true;

        // ── Connection state ──────────────────────────
        private ObservableCollection<SdeConnectionItem> _connections = new ObservableCollection<SdeConnectionItem>();
        private SdeConnectionItem _selectedConnection;
        private string _manualSdePath = "";
        private DateTime? _cacheTimestamp;

        // ── Full dataset cache ────────────────────────
        private List<SdeDatasetItem> _allDatasets = new List<SdeDatasetItem>();
        private int _loadGeneration; // guards against concurrent LoadAllDatasets calls

        // ── Visible results ───────────────────────────
        private ObservableCollection<SdeDatasetItem> _searchResults = new ObservableCollection<SdeDatasetItem>();
        private SdeDatasetItem _selectedResult;

        // ── Detail view ───────────────────────────────
        private bool _showDetails;
        private ObservableCollection<FieldInfo> _detailFields = new ObservableCollection<FieldInfo>();
        private string _detailMetadata = "";
        private bool _isLoadingDetails;
        private bool _detailAccessError;

        // ── Seed cache info panel ─────────────────────
        private bool _showSeedCacheInfo;
        private bool _isUsingSeedCache;
        private string _seedCacheConnectionPath = "";
        private string _seedCacheCachedAt = "";
        private string _seedCacheSummary = "";

        // ── Determinate progress ──────────────────────
        private int _progressValue;
        private int _progressMax;

        // ── Item type dropdown ────────────────────────
        private ObservableCollection<ItemTypeOption> _availableItemTypes = new();
        private ItemTypeOption _selectedItemType;

        // ── Tag filters (when checked, only show items with that tag) ──
        private bool _filterEditorTracking;
        private bool _filterArchiving;
        private bool _filterSubtypes;
        private bool _filterAttributeRules;

        // ── Filter cancellation ───────────────────────
        private CancellationTokenSource _filterCts;

        // ── Metadata display settings ─────────────────
        private MetadataSettings _metadataSettings = new();

        protected SdeSearchPaneViewModel()
        {
            SearchCommand = new RelayCommand(() => ApplyFilterAndSearch(), () => !IsSearching);
            ClearCommand = new RelayCommand(() => ClearSearch(), () => true);
            RefreshConnectionsCommand = new RelayCommand(() => _ = LoadConnections(), () => !IsSearching);
            ReloadDataCommand = new RelayCommand(() => _ = LoadAllDatasets(forceRefresh: true), () => !IsSearching && SelectedConnection != null);
            AddToMapCommand = new RelayCommand(() => _ = AddSelectedToMap(), () => SelectedResult != null && SelectedResult.CanAddToMap);
            BackToResultsCommand = new RelayCommand(() => { ShowDetails = false; ShowSeedCacheInfo = false; }, () => ShowDetails || ShowSeedCacheInfo);
            ShowSeedCacheInfoCommand = new RelayCommand(() => ShowSeedCacheInfo = true);
            CopyPathCommand = new RelayCommand(() => CopySelectedPath(), () => SelectedResult != null);
            BrowseSdeCommand = new RelayCommand(() => BrowseForSdeFile(), () => !IsSearching);
            AddManualPathCommand = new RelayCommand(() => AddManualSdePath(), () => !IsSearching && !string.IsNullOrWhiteSpace(ManualSdePath));
            ToggleThemeCommand = new RelayCommand(() => IsDarkMode = !IsDarkMode, () => true);
        }

        #region Commands
        public ICommand SearchCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand RefreshConnectionsCommand { get; }
        public ICommand ReloadDataCommand { get; }
        public ICommand AddToMapCommand { get; }
        public ICommand BackToResultsCommand { get; }
        public ICommand ShowSeedCacheInfoCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand BrowseSdeCommand { get; }
        public ICommand AddManualPathCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        #endregion

        #region Properties

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    NotifyPropertyChanged(nameof(ThemeLabel));
                    SaveThemePreference();
                }
            }
        }

        public string ThemeLabel => IsDarkMode ? "☀" : "🌙";

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public bool SearchByName
        {
            get => _searchByName;
            set { if (SetProperty(ref _searchByName, value)) ApplyFilterAndSearch(); }
        }

        public bool SearchByMetadata
        {
            get => _searchByMetadata;
            set { if (SetProperty(ref _searchByMetadata, value)) ApplyFilterAndSearch(); }
        }

        public bool SearchByTags
        {
            get => _searchByTags;
            set { if (SetProperty(ref _searchByTags, value)) ApplyFilterAndSearch(); }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set { if (SetProperty(ref _isSearching, value)) InvalidateCommands(); }
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
                    InvalidateCommands();
                    if (_selectedConnection != null)
                        _ = LoadAllDatasets(forceRefresh: false);
                }
            }
        }

        public string ManualSdePath
        {
            get => _manualSdePath;
            set { if (SetProperty(ref _manualSdePath, value)) InvalidateCommands(); }
        }

        public ObservableCollection<SdeDatasetItem> SearchResults
        {
            get => _searchResults;
            set => SetProperty(ref _searchResults, value);
        }

        public SdeDatasetItem SelectedResult
        {
            get => _selectedResult;
            set { if (SetProperty(ref _selectedResult, value)) InvalidateCommands(); }
        }

        public bool ShowDetails
        {
            get => _showDetails;
            set { if (SetProperty(ref _showDetails, value)) NotifyPropertyChanged(nameof(ShowResultsList)); }
        }

        public bool ShowResultsList => !ShowDetails && !ShowSeedCacheInfo;

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

        public bool DetailAccessError
        {
            get => _detailAccessError;
            set => SetProperty(ref _detailAccessError, value);
        }

        public bool ShowSeedCacheInfo
        {
            get => _showSeedCacheInfo;
            set { if (SetProperty(ref _showSeedCacheInfo, value)) NotifyPropertyChanged(nameof(ShowResultsList)); }
        }

        public bool IsUsingSeedCache
        {
            get => _isUsingSeedCache;
            set { if (SetProperty(ref _isUsingSeedCache, value)) NotifyPropertyChanged(nameof(HasCacheInfo)); }
        }

        /// <summary>True when any cache (seed or user-generated) has been loaded for the current connection.</summary>
        public bool HasCacheInfo => _isUsingSeedCache || _cacheTimestamp.HasValue;

        // ── Computed cache info panel properties (drive both seed and user cache views) ──

        public string CacheInfoTitle => _isUsingSeedCache ? "TEMPLATE CACHE INFO" : "CACHE INFO";

        public string CacheFromLabel => _isUsingSeedCache ? "GENERATED FROM" : "CACHED FROM";

        public string CacheFromValue => _isUsingSeedCache
            ? (_seedCacheConnectionPath.Length > 0 ? _seedCacheConnectionPath : "(not recorded)")
            : (_selectedConnection?.Path ?? "(unknown)");

        public string CacheDateLabel => _isUsingSeedCache ? "GENERATED ON" : "LAST CACHED";

        public string CacheDateValue => _isUsingSeedCache
            ? (_seedCacheCachedAt.Length > 0 ? _seedCacheCachedAt : "(unknown)")
            : (_cacheTimestamp.HasValue ? _cacheTimestamp.Value.ToLocalTime().ToString("yyyy-MM-dd  HH:mm") : "(unknown)");

        public string CacheContentsSummary
        {
            get
            {
                if (_isUsingSeedCache) return _seedCacheSummary;
                if (_allDatasets.Count == 0) return "";
                int fcs  = _allDatasets.Count(d => d.DatasetType == "Feature Class");
                int tbls = _allDatasets.Count(d => d.DatasetType == "Table");
                int fds  = _allDatasets.Count(d => d.DatasetType == "Feature Dataset");
                int rels = _allDatasets.Count(d => d.DatasetType == "Relationship Class");
                return $"Feature Classes:        {fcs}\nTables:                     {tbls}\nFeature Datasets:       {fds}\nRelationship Classes:  {rels}\n\nTotal:  {fcs + tbls + fds + rels}";
            }
        }

        public string AddinInstallDate
        {
            get
            {
                try
                {
                    var loc = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    var dt  = File.GetCreationTime(loc);
                    return dt > DateTime.MinValue ? dt.ToString("yyyy-MM-dd") : "";
                }
                catch { return ""; }
            }
        }

        public string SeedCacheConnectionPath
        {
            get => _seedCacheConnectionPath;
            set => SetProperty(ref _seedCacheConnectionPath, value);
        }

        public string SeedCacheCachedAt
        {
            get => _seedCacheCachedAt;
            set => SetProperty(ref _seedCacheCachedAt, value);
        }

        public string SeedCacheSummary
        {
            get => _seedCacheSummary;
            set => SetProperty(ref _seedCacheSummary, value);
        }

        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public int ProgressMax
        {
            get => _progressMax;
            set { if (SetProperty(ref _progressMax, value)) NotifyPropertyChanged(nameof(IsProgressDeterminate)); }
        }

        public bool IsProgressDeterminate => _progressMax > 0;


        public ObservableCollection<ItemTypeOption> AvailableItemTypes
        {
            get => _availableItemTypes;
            set => SetProperty(ref _availableItemTypes, value);
        }

        public ItemTypeOption SelectedItemType
        {
            get => _selectedItemType;
            set { if (SetProperty(ref _selectedItemType, value)) ApplyFilterAndSearch(); }
        }

        public bool FilterEditorTracking
        {
            get => _filterEditorTracking;
            set { if (SetProperty(ref _filterEditorTracking, value)) ApplyFilterAndSearch(); }
        }

        public bool FilterArchiving
        {
            get => _filterArchiving;
            set { if (SetProperty(ref _filterArchiving, value)) ApplyFilterAndSearch(); }
        }

        public bool FilterAttributeRules
        {
            get => _filterAttributeRules;
            set { if (SetProperty(ref _filterAttributeRules, value)) ApplyFilterAndSearch(); }
        }

        public bool FilterSubtypes
        {
            get => _filterSubtypes;
            set { if (SetProperty(ref _filterSubtypes, value)) ApplyFilterAndSearch(); }
        }

        #endregion

        // ═══════════════════════════════════════════════
        //  CONNECTION LOADING
        // ═══════════════════════════════════════════════

        #region Connection Loading

        private async Task LoadConnections()
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    var items = new List<SdeConnectionItem>();
                    var project = Project.Current;

                    if (project != null)
                    {
                        ReportProgress("Scanning project connections...");
                        foreach (var pi in project.GetItems<GDBProjectItem>())
                        {
                            try
                            {
                                var p = pi.Path;
                                if (!string.IsNullOrEmpty(p) &&
                                    (p.EndsWith(".sde", StringComparison.OrdinalIgnoreCase) ||
                                     p.Contains("DatabaseConnections", StringComparison.OrdinalIgnoreCase)))
                                {
                                    items.Add(new SdeConnectionItem { Name = pi.Name, Path = p, ConnectionType = "Project" });
                                }
                            }
                            catch { }
                        }

                        ScanFolder(items, Path.Combine(project.HomeFolderPath, "DatabaseConnections"), "Project Folder");
                    }

                    ScanFolder(items, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Esri", "ArcGISPro", "DatabaseConnections"), "ArcGIS Pro");

                    ScanFolder(items, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Esri", "ArcGISPro", "Favorites"), "Favorites");

                    RunOnUI(() =>
                    {
                        var previousPath = _selectedConnection?.Path;

                        var manual = Connections.Where(c => c.ConnectionType == "Manual").ToList();
                        Connections.Clear();
                        foreach (var c in items.OrderBy(c => c.Name)) Connections.Add(c);
                        foreach (var m in manual)
                            if (!Connections.Any(c => c.Path.Equals(m.Path, StringComparison.OrdinalIgnoreCase)))
                                Connections.Add(m);

                        StatusText = Connections.Count > 0
                            ? $"{Connections.Count} connection(s) found — select one to browse"
                            : "No connections found. Browse for an .sde file below.";
                        ProgressText = "";

                        // Restore previous selection, or auto-select if only one connection
                        if (previousPath != null)
                        {
                            var match = Connections.FirstOrDefault(c => c.Path.Equals(previousPath, StringComparison.OrdinalIgnoreCase));
                            if (match != null) SelectedConnection = match;
                        }
                        else if (Connections.Count == 1)
                        {
                            SelectedConnection = Connections.First();
                        }
                    });
                }
                catch (Exception ex) { ReportStatus($"Error: {ex.Message}"); }
            });
        }

        private void ScanFolder(List<SdeConnectionItem> items, string folder, string source)
        {
            try
            {
                if (!Directory.Exists(folder)) return;
                foreach (var f in Directory.GetFiles(folder, "*.sde"))
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    if (!items.Any(c => c.Path.Equals(f, StringComparison.OrdinalIgnoreCase)))
                        items.Add(new SdeConnectionItem { Name = name, Path = f, ConnectionType = source });
                }
            }
            catch { }
        }

        private void BrowseForSdeFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Enterprise Geodatabase Connection",
                Filter = "SDE Connection Files (*.sde)|*.sde|All Files (*.*)|*.*",
                DefaultExt = ".sde",
                CheckFileExists = true
            };
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Esri", "ArcGISPro", "DatabaseConnections");
            if (Directory.Exists(dir)) dlg.InitialDirectory = dir;
            if (dlg.ShowDialog() == true) AddConnectionFromPath(dlg.FileName);
        }

        private void AddManualSdePath()
        {
            var path = ManualSdePath?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!File.Exists(path)) { StatusText = "File not found: " + path; return; }
            if (!path.EndsWith(".sde", StringComparison.OrdinalIgnoreCase)) { StatusText = "Please select a .sde file"; return; }
            AddConnectionFromPath(path);
            ManualSdePath = "";
        }

        private void AddConnectionFromPath(string path)
        {
            var existing = Connections.FirstOrDefault(c => c.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (existing != null) { SelectedConnection = existing; return; }
            var conn = new SdeConnectionItem
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Path = path,
                ConnectionType = "Manual"
            };
            Connections.Add(conn);
            SelectedConnection = conn;
        }

        #endregion

        // ═══════════════════════════════════════════════
        //  DATASET LOADING + DISK CACHE
        // ═══════════════════════════════════════════════

        #region Dataset Loading

        /// <summary>
        /// Load datasets: tries disk cache first, falls back to live enumeration.
        /// </summary>
        private async Task LoadAllDatasets(bool forceRefresh)
        {
            if (SelectedConnection == null) return;

            // Increment generation so any earlier in-flight call knows to discard its results
            var myGeneration = ++_loadGeneration;

            IsSearching = true;
            ShowDetails = false;
            // Assign a new list rather than clearing the existing one so that any
            // concurrently-running ApplyFilterAndSearch Task.Run that captured the
            // old reference can finish iterating without an InvalidOperationException.
            _allDatasets = new List<SdeDatasetItem>();

            var connPath = SelectedConnection.Path;
            var connName = SelectedConnection.Name;

            // ── Try loading from disk cache ──
            if (!forceRefresh)
            {
                ReportProgress("Checking cache...");

                // SdeSearchCache.Load() reads and deserialises a potentially large JSON
                // file (e.g. ~2 MB SeedCache.json) synchronously.  Doing this on the
                // WPF Dispatcher thread blocks UI message processing long enough for
                // ArcGIS Pro to detect an unresponsive UI and terminate the process.
                // Move all disk I/O and CPU-bound search-text building to a thread-pool
                // thread; the continuation re-joins the Dispatcher thread automatically
                // because WPF's DispatcherSynchronizationContext is captured at the await.
                List<SdeDatasetItem> cached = null;
                DateTime? cachedAt = null;
                bool usedSeedCache = false;
                (string ConnectionPath, DateTime? CachedAt, int FCs, int Tables, int FDs, int Rels) seedInfo = default;

                await Task.Run(() =>
                {
                    cached = SdeSearchCache.Load(connPath, out cachedAt, out usedSeedCache);
                    if (cached != null)
                        foreach (var d in cached) BuildSearchTexts(d);
                    if (usedSeedCache)
                        seedInfo = SdeSearchCache.GetSeedCacheInfo();
                });

                if (cached != null && cached.Count > 0)
                {
                    // A newer call may have started; let it win
                    if (myGeneration != _loadGeneration) return;

                    _allDatasets = cached;
                    _cacheTimestamp = cachedAt;
                    int fcs = cached.Count(d => d.DatasetType == "Feature Class");
                    int tbls = cached.Count(d => d.DatasetType == "Table");
                    int fds = cached.Count(d => d.DatasetType == "Feature Dataset");
                    int rels = cached.Count(d => d.DatasetType == "Relationship Class");

                    string cacheInfo;
                    if (usedSeedCache)
                    {
                        cacheInfo = "template data - click ↻ to refresh from your database";
                    }
                    else
                    {
                        string cacheAge = FormatCacheAge(cachedAt);
                        cacheInfo = $"cached {cacheAge}";
                    }

                    if (usedSeedCache)
                    {
                        var (seedConn, seedDate, seedFCs, seedTbls, seedFDs, seedRels) = seedInfo;
                        RunOnUI(() =>
                        {
                            IsUsingSeedCache = true;
                            SeedCacheConnectionPath = string.IsNullOrEmpty(seedConn) ? "(not recorded)" : seedConn;
                            SeedCacheCachedAt = seedDate.HasValue
                                ? seedDate.Value.ToLocalTime().ToString("yyyy-MM-dd  HH:mm")
                                : "(unknown)";
                            SeedCacheSummary = $"Feature Classes:        {seedFCs}\nTables:                     {seedTbls}\nFeature Datasets:       {seedFDs}\nRelationship Classes:  {seedRels}\n\nTotal:  {seedFCs + seedTbls + seedFDs + seedRels}";
                        });
                    }
                    else
                    {
                        RunOnUI(() => IsUsingSeedCache = false);
                    }

                    // BuildAvailableItemTypes sets SelectedItemType which triggers ApplyFilterAndSearch
                    BuildAvailableItemTypes();
                    RunOnUI(() =>
                    {
                        NotifyPropertyChanged(nameof(HasCacheInfo));
                        NotifyPropertyChanged(nameof(CacheInfoTitle));
                        NotifyPropertyChanged(nameof(CacheFromLabel));
                        NotifyPropertyChanged(nameof(CacheFromValue));
                        NotifyPropertyChanged(nameof(CacheDateLabel));
                        NotifyPropertyChanged(nameof(CacheDateValue));
                        NotifyPropertyChanged(nameof(CacheContentsSummary));
                        IsSearching = false;
                        ProgressText = "";
                        StatusText = $"{connName} ({cacheInfo}): {fcs} FCs, {tbls} tables, {fds} datasets, {rels} relationships";
                    });
                    return;
                }
            }

            RunOnUI(() => { IsUsingSeedCache = false; ProgressMax = 0; ProgressValue = 0; });
            ReportProgress("Connecting to " + connName + "...");
            ReportStatus("Connecting...");

            await QueuedTask.Run(() =>
            {
                try
                {
                    using (var gdb = new Geodatabase(new DatabaseConnectionFile(new Uri(connPath))))
                    {
                        // Use a local list to avoid cross-thread corruption when a
                        // concurrent LoadAllDatasets call clears _allDatasets.
                        var localDatasets = new List<SdeDatasetItem>();
                        int total = 0;
                        int processed = 0;

                        // Pre-fetch all definition lists so the total is known upfront
                        // and the progress bar can show a real fraction instead of spinning.
                        ReportProgress("Counting datasets...");
                        var allFDDefs  = gdb.GetDefinitions<FeatureDatasetDefinition>().ToList();
                        var allFCDefs  = gdb.GetDefinitions<FeatureClassDefinition>().ToList();
                        var allTblDefs = gdb.GetDefinitions<TableDefinition>().ToList();
                        var allRelDefs = gdb.GetDefinitions<RelationshipClassDefinition>().ToList();
                        int totalExpected = allFDDefs.Count + allFCDefs.Count + allTblDefs.Count + allRelDefs.Count;
                        RunOnUI(() => { ProgressMax = totalExpected; ProgressValue = 0; });

                        // ── Feature Datasets + their contained feature classes ─────────────────────
                        ReportProgress($"Feature Datasets: 0 / {allFDDefs.Count}...");
                        var featureDatasetMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        try
                        {
                            foreach (var def in allFDDefs)
                            {
                                try
                                {
                                    var fdName = def.GetName();
                                    var item = new SdeDatasetItem
                                    {
                                        Name = fdName,
                                        SimpleName = GetSimpleName(fdName),
                                        DatasetType = "Feature Dataset",
                                        GeometryIconType = "Dataset",
                                        CanAddToMap = false,
                                        ConnectionPath = connPath
                                    };

                                    // Load metadata for searching
                                    TryLoadMetadata(item, def);
                                    DetectDates(item);
                                    BuildSearchTexts(item);

                                    localDatasets.Add(item);
                                    total++;
                                    processed++;
                                    { int snap = processed; RunOnUI(() => ProgressValue = snap); }

                                    // Enumerate datasets within this feature dataset
                                    try
                                    {
                                        var relatedDefs = gdb.GetRelatedDefinitions(def, DefinitionRelationshipType.DatasetInFeatureDataset);
                                        foreach (var relDef in relatedDefs)
                                        {
                                            try
                                            {
                                                var childName = relDef.GetName();
                                                // Store by both full name and simple name to handle naming mismatches
                                                featureDatasetMap[childName] = fdName;
                                                var simpleChild = GetSimpleName(childName);
                                                if (!string.Equals(simpleChild, childName, StringComparison.OrdinalIgnoreCase))
                                                    featureDatasetMap[simpleChild] = fdName;
                                            }
                                            catch { }
                                        }
                                    }
                                    catch { }
                                }
                                catch { }
                            }
                        }
                        catch { }

                        ReportProgress($"Feature Datasets: {allFDDefs.Count} / {allFDDefs.Count}. Loading feature classes...");

                        // ── Feature Classes (FAST: skip field count) ─
                        try
                        {
                            int fc = 0;
                            foreach (var def in allFCDefs)
                            {
                                try
                                {
                                    var geomType = "Unknown";
                                    try { geomType = def.GetShapeType().ToString(); } catch { }

                                    var fcName = def.GetName();
                                    var item = new SdeDatasetItem
                                    {
                                        Name = fcName,
                                        SimpleName = GetSimpleName(fcName),
                                        DatasetType = "Feature Class",
                                        GeometryType = geomType,
                                        GeometryIconType = MapGeometryIcon(geomType),
                                        CanAddToMap = true,
                                        ConnectionPath = connPath
                                    };

                                    // Check if this feature class belongs to a feature dataset
                                    // Try full name first, then simple name as fallback for naming mismatches
                                    if (featureDatasetMap.TryGetValue(fcName, out string fdName) ||
                                        featureDatasetMap.TryGetValue(GetSimpleName(fcName), out fdName))
                                    {
                                        item.FeatureDatasetName = fdName;
                                    }

                                    try { item.SpatialReference = def.GetSpatialReference()?.Name; } catch { }
                                    try { item.AliasName = def.GetAliasName(); } catch { }

                                    // Load metadata for searching
                                    TryLoadMetadata(item, def);
                                    DetectDates(item);
                                    DetectEditorTracking(item, def);
                                    DetectArchiving(item, def, gdb);
                                    DetectSubtypes(item, def);
                                    DetectAttributeRules(item, def);
                                    // Field details are lazy-loaded on demand; only store the count for display
                                    try { item.FieldCount = def.GetFields().Count; } catch { }
                                    BuildSearchTexts(item);

                                    localDatasets.Add(item);
                                    total++; fc++; processed++;
                                    if (fc % 10 == 0)
                                    {
                                        int snap = processed;
                                        RunOnUI(() => ProgressValue = snap);
                                        ReportProgress($"Feature Classes: {fc} / {allFCDefs.Count}");
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }

                        ReportProgress($"Feature Classes: {allFCDefs.Count} / {allFCDefs.Count}. Loading tables...");

                        // ── Tables ───────────────────────────────
                        try
                        {
                            var fcNames = new HashSet<string>(
                                localDatasets.Where(d => d.DatasetType == "Feature Class").Select(d => d.Name),
                                StringComparer.OrdinalIgnoreCase);

                            int tc = 0;
                            foreach (var def in allTblDefs)
                            {
                                try
                                {
                                    if (fcNames.Contains(def.GetName())) continue;

                                    var item = new SdeDatasetItem
                                    {
                                        Name = def.GetName(),
                                        SimpleName = GetSimpleName(def.GetName()),
                                        DatasetType = "Table",
                                        GeometryIconType = "Table",
                                        CanAddToMap = true,
                                        ConnectionPath = connPath
                                    };
                                    try { item.AliasName = def.GetAliasName(); } catch { }

                                    // Load metadata for searching
                                    TryLoadMetadata(item, def);
                                    DetectDates(item);
                                    DetectEditorTracking(item, def);
                                    DetectArchiving(item, def, gdb);
                                    DetectSubtypes(item, def);
                                    DetectAttributeRules(item, def);
                                    // Field details are lazy-loaded on demand; only store the count for display
                                    try { item.FieldCount = def.GetFields().Count; } catch { }
                                    BuildSearchTexts(item);

                                    localDatasets.Add(item);
                                    total++; tc++; processed++;
                                    if (tc % 10 == 0)
                                    {
                                        int snap = processed;
                                        RunOnUI(() => ProgressValue = snap);
                                        ReportProgress($"Tables: {tc} / {allTblDefs.Count}");
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }

                        // ── Relationship Classes ─────────────────
                        ReportProgress($"Tables: {total - allFCDefs.Count - allFDDefs.Count} / {allTblDefs.Count}. Loading relationships...");
                        try
                        {
                            foreach (var def in allRelDefs)
                            {
                                try
                                {
                                    var relItem = new SdeDatasetItem
                                    {
                                        Name = def.GetName(),
                                        SimpleName = GetSimpleName(def.GetName()),
                                        DatasetType = "Relationship Class",
                                        GeometryIconType = "Relationship",
                                        CanAddToMap = false,
                                        ConnectionPath = connPath,
                                        MetadataSnippet = $"Origin: {GetSimpleName(def.GetOriginClass())} → Dest: {GetSimpleName(def.GetDestinationClass())}"
                                    };
                                    BuildSearchTexts(relItem);
                                    localDatasets.Add(relItem);
                                    total++; processed++;
                                }
                                catch { }
                            }
                        }
                        catch { }

                        // ── Sort ─────────────────────────────────
                        ReportProgress($"Loaded {total} items. Sorting...");
                        localDatasets = localDatasets
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

                        // ── Save cache ───────────────────────────
                        ReportProgress("Saving to cache...");
                        SdeSearchCache.Save(connPath, localDatasets);

                        // If a newer LoadAllDatasets call started while we were
                        // enumerating, discard our results — the newer call wins.
                        if (myGeneration != _loadGeneration) return;

                        _allDatasets = localDatasets;
                        _cacheTimestamp = DateTime.UtcNow;

                        int fcs = localDatasets.Count(d => d.DatasetType == "Feature Class");
                        int tbls = localDatasets.Count(d => d.DatasetType == "Table");
                        int fds = localDatasets.Count(d => d.DatasetType == "Feature Dataset");
                        int rels = localDatasets.Count(d => d.DatasetType == "Relationship Class");

                        // BuildAvailableItemTypes sets SelectedItemType which triggers ApplyFilterAndSearch
                        BuildAvailableItemTypes();
                        RunOnUI(() =>
                        {
                            NotifyPropertyChanged(nameof(HasCacheInfo));
                            NotifyPropertyChanged(nameof(CacheInfoTitle));
                            NotifyPropertyChanged(nameof(CacheFromLabel));
                            NotifyPropertyChanged(nameof(CacheFromValue));
                            NotifyPropertyChanged(nameof(CacheDateLabel));
                            NotifyPropertyChanged(nameof(CacheDateValue));
                            NotifyPropertyChanged(nameof(CacheContentsSummary));
                            IsSearching = false;
                            ProgressText = "";
                            ProgressMax = 0;
                            ProgressValue = 0;
                            StatusText = $"{connName} (just refreshed): {fcs} FCs, {tbls} tables, {fds} datasets, {rels} relationships";
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Load error: {ex.Message}");
                    RunOnUI(() =>
                    {
                        IsSearching = false;
                        ProgressText = "";
                        StatusText = $"Connection error: {ex.Message}";
                    });
                }
            });
        }

        private string MapGeometryIcon(string geomType)
        {
            if (string.IsNullOrEmpty(geomType)) return "Unknown";
            var g = geomType.ToLowerInvariant();
            if (g.Contains("point")) return "Point";
            if (g.Contains("line") || g.Contains("polyline")) return "Polyline";
            if (g.Contains("polygon")) return "Polygon";
            if (g.Contains("multipatch")) return "Multipatch";
            return "Unknown";
        }

        #endregion

        // ═══════════════════════════════════════════════
        //  FILTER / SEARCH (in-memory, instant)
        // ═══════════════════════════════════════════════

        #region Filter

        private void BuildAvailableItemTypes()
        {
            var types = _allDatasets
                .GroupBy(d => d.GeometryIconType ?? "Unknown")
                .OrderBy(g => TypeSortOrder(g.Key))
                .Select(g => new ItemTypeOption
                {
                    Key = g.Key,
                    Display = $"{TypeDisplayName(g.Key)} ({g.Count()})"
                })
                .ToList();

            types.Insert(0, new ItemTypeOption
            {
                Key = "All",
                Display = $"All ({_allDatasets.Count})"
            });

            RunOnUI(() =>
            {
                AvailableItemTypes.Clear();
                foreach (var t in types) AvailableItemTypes.Add(t);
                // Setting SelectedItemType triggers ApplyFilterAndSearch via its setter
                SelectedItemType = AvailableItemTypes.FirstOrDefault();
            });
        }

        private static string TypeDisplayName(string iconType) => iconType switch
        {
            "Dataset" => "Feature Dataset",
            "Relationship" => "Relationship",
            _ => iconType
        };

        private static int TypeSortOrder(string iconType) => iconType switch
        {
            "Point" => 0, "Polyline" => 1, "Polygon" => 2, "Multipatch" => 3,
            "Table" => 4, "Dataset" => 5, "Relationship" => 6, _ => 7
        };

        private void ApplyFilterAndSearch()
        {
            // Auto-exit detail view so the user sees updated results immediately
            if (ShowDetails) ShowDetails = false;

            // Cancel any in-flight filter — a new one supersedes it
            _filterCts?.Cancel();
            _filterCts = new CancellationTokenSource();
            var token = _filterCts.Token;

            // Capture all state on the UI thread before handing off to Task.Run,
            // so the background thread never touches live ViewModel properties.
            string term = (SearchText ?? "").Trim();
            bool wildcard = string.IsNullOrEmpty(term);
            string[] upperTerms = wildcard
                ? Array.Empty<string>()
                : term.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(t => t.ToUpperInvariant()).ToArray();

            bool byName = _searchByName;
            bool byMeta = _searchByMetadata;
            bool byTags = _searchByTags;
            string typeFilter = _selectedItemType?.Key ?? "All";
            bool filtET  = _filterEditorTracking;
            bool filtArch = _filterArchiving;
            bool filtSub  = _filterSubtypes;
            bool filtAttrRules = _filterAttributeRules;
            var datasets = _allDatasets; // capture reference; List<T> is thread-safe for concurrent reads

            _ = Task.Run(() =>
            {
                var filtered = datasets.Where(item =>
                {
                    if (typeFilter != "All" && item.GeometryIconType != typeFilter) return false;

                    if (filtET       && !item.HasEditorTracking) return false;
                    if (filtArch     && !item.IsArchived) return false;
                    if (filtSub      && !item.HasSubtypes) return false;
                    if (filtAttrRules && !item.HasAttributeRules) return false;

                    if (wildcard) return true;
                    return MatchesSearch(item, upperTerms, byName, byMeta, byTags);
                }).ToList();

                if (token.IsCancellationRequested) return;

                RunOnUI(() =>
                {
                    if (token.IsCancellationRequested) return;
                    SearchResults.Clear();
                    foreach (var r in filtered) SearchResults.Add(r);
                    ResultCount = filtered.Count;
                    if (!wildcard)
                        StatusText = $"Found {filtered.Count} of {datasets.Count} matching \"{term}\"";
                });
            }, token);
        }

        /// <summary>
        /// Match an item against pre-split, pre-uppercased search terms using the pre-computed
        /// NameText / MetaText index strings. Ordinal comparison on already-uppercased strings
        /// is significantly faster than OrdinalIgnoreCase on raw strings.
        /// </summary>
        private static bool MatchesSearch(SdeDatasetItem item, string[] upperTerms, bool byName, bool byMeta, bool byTags)
        {
            foreach (var term in upperTerms)
            {
                bool matched = false;
                if (byName)
                    matched = item.NameText != null && item.NameText.Contains(term, StringComparison.Ordinal);
                if (!matched && byMeta)
                    matched = item.MetaText != null && item.MetaText.Contains(term, StringComparison.Ordinal);
                if (!matched && byTags)
                    matched = item.TagsText != null && item.TagsText.Contains(term, StringComparison.Ordinal);
                if (!matched) return false;
            }
            return true;
        }

        #endregion

        // ═══════════════════════════════════════════════
        //  DETAIL VIEW (lazy metadata + fields + flags)
        // ═══════════════════════════════════════════════

        #region Detail View

        internal async Task LoadItemDetails(SdeDatasetItem item)
        {
            if (item == null) return;

            SelectedResult = item;
            IsLoadingDetails = true;
            ShowDetails = true;

            // Clear previous details immediately for visual feedback
            RunOnUI(() =>
            {
                DetailFields.Clear();
                DetailMetadata = "Loading...";
                DetailAccessError = false;
            });

            await QueuedTask.Run(() =>
            {
                var fields = new List<FieldInfo>();
                try
                {
                    using (var gdb = new Geodatabase(new DatabaseConnectionFile(new Uri(item.ConnectionPath))))
                    {
                        TableDefinition tableDef = null;

                        if (item.DatasetType == "Feature Class")
                        {
                            try { tableDef = gdb.GetDefinition<FeatureClassDefinition>(item.Name); } catch { }
                        }
                        else if (item.DatasetType == "Table")
                        {
                            try { tableDef = gdb.GetDefinition<TableDefinition>(item.Name); } catch { }
                        }
                        else if (item.DatasetType == "Feature Dataset")
                        {
                            try
                            {
                                var fdDef = gdb.GetDefinition<FeatureDatasetDefinition>(item.Name);
                                if (!item.HasMetadata) TryLoadMetadata(item, fdDef);
                            }
                            catch { }
                        }

                        if (tableDef != null)
                        {
                            // ── Metadata (lazy) ──────────────────
                            if (!item.HasMetadata)
                                TryLoadMetadata(item, tableDef);

                            // ── Editor tracking detection ─────────
                            DetectEditorTracking(item, tableDef);

                            // ── Archiving detection ──────────────
                            DetectArchiving(item, tableDef, gdb);

                            // ── Subtype detection ─────────────────
                            DetectSubtypes(item, tableDef);

                            // ── Attribute rules detection ──────────
                            DetectAttributeRules(item, tableDef);

                            // ── Created / Modified dates from metadata ─
                            DetectDates(item);

                            // ── Load all fields ──────────────────
                            fields = EnumerateFields(tableDef);
                            item.Fields = fields;
                            item.FieldCount = fields.Count;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Detail load error: {ex.Message}");
                    // fields remains empty — connection failure, nothing to fall back to
                    RunOnUI(() => DetailAccessError = true);
                }

                var meta = BuildMetadataDisplay(item);

                RunOnUI(() =>
                {
                    DetailFields.Clear();
                    foreach (var f in fields) DetailFields.Add(f);
                    DetailMetadata = meta;
                    IsLoadingDetails = false;
                });
            });
        }

        private static void DetectSubtypes(SdeDatasetItem item, TableDefinition tableDef)
        {
            try
            {
                item.HasSubtypes = tableDef.GetSubtypes().Count > 0;
            }
            catch { }
        }

        private void DetectEditorTracking(SdeDatasetItem item, TableDefinition tableDef)
        {
            try
            {
                // Primary: use the SDK API — works with custom field names
                item.HasEditorTracking = tableDef.IsEditorTrackingEnabled();
            }
            catch
            {
                // Fallback: check for default editor tracking field names
                try
                {
                    var fieldNames = tableDef.GetFields()
                        .Select(f => GetSimpleName(f.Name).ToUpperInvariant())
                        .ToHashSet();
                    item.HasEditorTracking =
                        fieldNames.Contains("CREATED_USER") && fieldNames.Contains("CREATED_DATE") &&
                        fieldNames.Contains("LAST_EDITED_USER") && fieldNames.Contains("LAST_EDITED_DATE");
                }
                catch { }
            }
        }

        private void DetectArchiving(SdeDatasetItem item, TableDefinition tableDef, Geodatabase gdb)
        {
            try
            {
                // Check for archiving fields (use simple names for SDE-qualified fields)
                var fieldNames = tableDef.GetFields()
                    .Select(f => GetSimpleName(f.Name).ToUpperInvariant())
                    .ToHashSet();
                item.IsArchived = fieldNames.Contains("GDB_FROM_DATE") && fieldNames.Contains("GDB_TO_DATE");

                // Also try the API approach
                if (!item.IsArchived)
                {
                    try
                    {
                        using (var table = gdb.OpenDataset<Table>(item.Name))
                        {
                            item.IsArchived = table.IsArchiveEnabled();
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void DetectAttributeRules(SdeDatasetItem item, TableDefinition tableDef)
        {
            try
            {
                var rules = tableDef.GetAttributeRules();
                item.HasAttributeRules = rules != null && rules.Count > 0;
            }
            catch { }
        }

        private void DetectDates(SdeDatasetItem item)
        {
            if (item.RawMetadataXml == null) return;
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(item.RawMetadataXml);

                // Creation date
                var createStr = GetXmlText(doc, "//CreaDate") ?? GetXmlText(doc, "//idinfo/citation/citeinfo/pubdate");
                if (!string.IsNullOrEmpty(createStr))
                    item.CreatedDate = TryParseDate(createStr);

                // Modified date
                var modStr = GetXmlText(doc, "//ModDate") ?? GetXmlText(doc, "//metainfo/metd");
                if (!string.IsNullOrEmpty(modStr))
                    item.ModifiedDate = TryParseDate(modStr);
            }
            catch { }
        }

        private DateTime? TryParseDate(string s)
        {
            if (DateTime.TryParse(s, out var dt)) return dt;
            // ArcGIS metadata often uses YYYYMMDD format
            if (s.Length == 8 && DateTime.TryParseExact(s, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt2)) return dt2;
            return null;
        }

        private string BuildMetadataDisplay(SdeDatasetItem item)
        {
            var lines = new List<string>();

            // Pre-build flags once (referenced by the "Flags" key)
            var flagParts = new List<string>();
            if (item.HasEditorTracking) flagParts.Add("✓ Editor Tracking");
            if (item.IsArchived)        flagParts.Add("✓ Archiving");
            if (item.HasSubtypes)       flagParts.Add("✓ Subtypes");
            if (item.HasAttributeRules) flagParts.Add("✓ Attribute Rules");

            bool hasAnyMetadataContent = false;

            foreach (var field in _metadataSettings.Fields)
            {
                if (!field.Visible) continue;

                switch (field.Key)
                {
                    case "FullName":
                        lines.Add($"{field.Label}: {item.Name}");
                        break;
                    case "Type":
                        lines.Add($"{field.Label}: {item.DatasetType}");
                        break;
                    case "Alias":
                        if (!string.IsNullOrWhiteSpace(item.AliasName) && item.AliasName != item.SimpleName)
                            lines.Add($"{field.Label}: {item.AliasName}");
                        break;
                    case "Geometry":
                        if (!string.IsNullOrWhiteSpace(item.GeometryType))
                            lines.Add($"{field.Label}: {item.GeometryType}");
                        break;
                    case "SpatialReference":
                        if (!string.IsNullOrWhiteSpace(item.SpatialReference))
                            lines.Add($"{field.Label}: {item.SpatialReference}");
                        break;
                    case "FieldCount":
                        if (item.FieldCount > 0)
                            lines.Add($"{field.Label}: {item.FieldCount}");
                        break;
                    case "Flags":
                        if (flagParts.Count > 0)
                            lines.Add($"{field.Label}: {string.Join("  |  ", flagParts)}");
                        break;
                    case "CreatedDate":
                        if (item.CreatedDate.HasValue)
                            lines.Add($"{field.Label}: {item.CreatedDate.Value:yyyy-MM-dd}");
                        break;
                    case "ModifiedDate":
                        if (item.ModifiedDate.HasValue)
                            lines.Add($"{field.Label}: {item.ModifiedDate.Value:yyyy-MM-dd}");
                        break;
                    case "Description":
                        if (item.HasMetadata && !string.IsNullOrWhiteSpace(item.Description))
                        { lines.Add($"\n{field.Label}:\n{item.Description}"); hasAnyMetadataContent = true; }
                        break;
                    case "Summary":
                        if (item.HasMetadata && !string.IsNullOrWhiteSpace(item.Summary)
                            && item.Summary != item.Description)
                        { lines.Add($"\n{field.Label}:\n{item.Summary}"); hasAnyMetadataContent = true; }
                        break;
                    case "Tags":
                        if (item.HasMetadata && !string.IsNullOrWhiteSpace(item.Tags))
                        { lines.Add($"\n{field.Label}: {item.Tags}"); hasAnyMetadataContent = true; }
                        break;
                    case "Credits":
                        if (item.HasMetadata && !string.IsNullOrWhiteSpace(item.Credits))
                        { lines.Add($"\n{field.Label}: {item.Credits}"); hasAnyMetadataContent = true; }
                        break;
                    case "UseConstraints":
                        if (item.HasMetadata && !string.IsNullOrWhiteSpace(item.UseConstraints))
                        { lines.Add($"\n{field.Label}: {item.UseConstraints}"); hasAnyMetadataContent = true; }
                        break;
                }
            }

            if (!item.HasMetadata || !hasAnyMetadataContent)
                lines.Add("\n(No metadata available)");

            return string.Join("\n", lines);
        }

        #endregion

        // ═══════════════════════════════════════════════
        //  METADATA PARSING
        // ═══════════════════════════════════════════════

        #region Metadata

        private void TryLoadMetadata(SdeDatasetItem item, TableDefinition definition)
        {
            try
            {
                // Construct the path for the dataset in the geodatabase
                // Include feature dataset name in path if present
                string itemPath = !string.IsNullOrEmpty(item.FeatureDatasetName)
                    ? System.IO.Path.Combine(item.ConnectionPath, item.FeatureDatasetName, item.Name)
                    : System.IO.Path.Combine(item.ConnectionPath, item.Name);
                var catalogItem = ItemFactory.Instance.Create(itemPath);

                if (catalogItem != null)
                {
                    string xml = catalogItem.GetXml();
                    if (!string.IsNullOrEmpty(xml))
                    {
                        item.HasMetadata = true;
                        item.RawMetadataXml = xml;
                        ParseMetadataXml(item, xml);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Metadata load error for {item.Name}: {ex.Message}");
            }
        }

        private void TryLoadMetadata(SdeDatasetItem item, FeatureDatasetDefinition definition)
        {
            try
            {
                // Construct the path for the dataset in the geodatabase
                string itemPath = System.IO.Path.Combine(item.ConnectionPath, item.Name);
                var catalogItem = ItemFactory.Instance.Create(itemPath);

                if (catalogItem != null)
                {
                    string xml = catalogItem.GetXml();
                    if (!string.IsNullOrEmpty(xml))
                    {
                        item.HasMetadata = true;
                        item.RawMetadataXml = xml;
                        ParseMetadataXml(item, xml);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Metadata load error for {item.Name}: {ex.Message}");
            }
        }

        private void ParseMetadataXml(SdeDatasetItem item, string xml)
        {
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(xml);

                item.Description = GetXmlText(doc, "//dataIdInfo/idAbs") ??
                                   GetXmlText(doc, "//idinfo/descript/abstract") ?? "";
                item.Summary = GetXmlText(doc, "//dataIdInfo/idPurp") ??
                               GetXmlText(doc, "//idinfo/descript/purpose") ?? "";
                item.Purpose = GetXmlText(doc, "//dataIdInfo/idPurp") ?? "";
                item.Credits = GetXmlText(doc, "//dataIdInfo/idCredit") ??
                               GetXmlText(doc, "//idinfo/datacred") ?? "";

                var tags = new List<string>();
                AddXmlNodes(tags, doc, "//dataIdInfo/searchKeys/keyword");
                AddXmlNodes(tags, doc, "//idinfo/keywords/theme/themekey");
                item.Tags = string.Join(", ", tags);

                item.UseConstraints = GetXmlText(doc, "//dataIdInfo/resConst/Consts/useLimit") ??
                                      GetXmlText(doc, "//idinfo/useconst") ?? "";

                // Snippet for list view
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(item.Description))
                    parts.Add(item.Description.Length > 100 ? item.Description[..100] + "…" : item.Description);
                if (!string.IsNullOrWhiteSpace(item.Tags))
                    parts.Add($"Tags: {item.Tags}");
                if (parts.Count > 0)
                    item.MetadataSnippet = string.Join(" | ", parts);
            }
            catch { }
        }

        private string GetXmlText(System.Xml.XmlDocument doc, string xpath)
        {
            try { var n = doc.SelectSingleNode(xpath); return n != null && !string.IsNullOrWhiteSpace(n.InnerText) ? StripHtml(n.InnerText) : null; }
            catch { return null; }
        }

        // Strips HTML tags and decodes entities left behind by ArcGIS metadata editors
        // which store description/summary fields as embedded HTML fragments.
        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return html;
            // Replace block-level closing tags with a newline so paragraphs are preserved
            var text = System.Text.RegularExpressions.Regex.Replace(
                html, @"</?(div|p|br|li|h\d)[^>]*>", "\n",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // Strip all remaining tags
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");
            // Decode HTML entities (&amp; &lt; &#160; etc.)
            text = System.Net.WebUtility.HtmlDecode(text);
            // Collapse runs of blank lines to a single blank line and trim
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n").Trim();
            return text;
        }

        private void AddXmlNodes(List<string> tags, System.Xml.XmlDocument doc, string xpath)
        {
            try
            {
                var nodes = doc.SelectNodes(xpath);
                if (nodes == null) return;
                foreach (System.Xml.XmlNode n in nodes)
                    if (!string.IsNullOrWhiteSpace(n.InnerText) &&
                        !tags.Contains(n.InnerText.Trim(), StringComparer.OrdinalIgnoreCase))
                        tags.Add(n.InnerText.Trim());
            }
            catch { }
        }

        #endregion

        // ═══════════════════════════════════════════════
        //  MAP OPERATIONS
        // ═══════════════════════════════════════════════

        #region Map

        private async Task AddSelectedToMap()
        {
            if (SelectedResult == null || !SelectedResult.CanAddToMap) return;
            var item = SelectedResult;
            StatusText = $"Adding {item.SimpleName}...";
            await QueuedTask.Run(() =>
            {
                try
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                    {
                        // No active map - create a new one
                        ReportStatus("No active map found. Creating new map...");
                        map = MapFactory.Instance.CreateMap(item.SimpleName + " Map", ArcGIS.Core.CIM.MapType.Map, ArcGIS.Core.CIM.MapViewingMode.Map);
                        if (map == null)
                        {
                            ReportStatus("Error: Failed to create new map.");
                            return;
                        }
                        // Open the newly created map in a map view
                        _ = FrameworkApplication.Panes.CreateMapPaneAsync(map);
                    }
                    // Construct the correct path - include feature dataset if present
                    string path;
                    if (!string.IsNullOrEmpty(item.FeatureDatasetName))
                    {
                        // Feature class inside a feature dataset
                        path = item.ConnectionPath + "\\" + item.FeatureDatasetName + "\\" + item.Name;
                    }
                    else
                    {
                        // Standalone feature class or table
                        path = item.ConnectionPath + "\\" + item.Name;
                    }
                    var uri = new Uri(path);

                    if (item.DatasetType == "Feature Class")
                        LayerFactory.Instance.CreateLayer(uri, map);
                    else if (item.DatasetType == "Table")
                        StandaloneTableFactory.Instance.CreateStandaloneTable(uri, map);
                    ReportStatus($"✓ Added \"{item.SimpleName}\" to map");
                }
                catch (Exception ex)
                {
                    var msg = ex.Message;
                    if (msg.Contains("ORA-") || msg.Contains("TNS") || msg.Contains("DBMS") || msg.Contains("connect", StringComparison.OrdinalIgnoreCase))
                        ReportStatus("Connection error — unable to reach the database. Check your network/VPN.");
                    else
                        ReportStatus($"Error adding to map: {msg}");
                }
            });
        }

        private void CopySelectedPath()
        {
            if (SelectedResult == null) return;
            try {
                var copyPath = !string.IsNullOrEmpty(SelectedResult.FeatureDatasetName)
                    ? $"{SelectedResult.ConnectionPath}\\{SelectedResult.FeatureDatasetName}\\{SelectedResult.Name}"
                    : $"{SelectedResult.ConnectionPath}\\{SelectedResult.Name}";
                System.Windows.Clipboard.SetText(copyPath); StatusText = "Copied path to clipboard"; }
            catch (Exception ex) { StatusText = $"Copy error: {ex.Message}"; }
        }

        #endregion

        // ═══════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════

        #region Helpers

        /// <summary>
        /// Builds the pre-computed uppercase search index strings on an item.
        /// Called once per item after enumeration or cache load so the hot filter
        /// loop can use fast Ordinal string comparisons instead of OrdinalIgnoreCase.
        /// </summary>
        private static void BuildSearchTexts(SdeDatasetItem item)
        {
            item.NameText = string.Concat(
                item.Name ?? "", " ", item.SimpleName ?? "", " ", item.AliasName ?? ""
            ).ToUpperInvariant();

            item.TagsText = (item.Tags ?? "").ToUpperInvariant();

            item.MetaText = string.Concat(
                item.Name ?? "", " ", item.SimpleName ?? "", " ", item.AliasName ?? "", " ",
                item.Description ?? "", " ", item.Summary ?? "", " ",
                item.Tags ?? "", " ", item.Purpose ?? "", " ",
                item.Credits ?? "", " ", item.MetadataSnippet ?? "", " ",
                item.DatasetType ?? ""
            ).ToUpperInvariant();
        }

        private void ClearSearch()
        {
            SearchText = "";
            FilterEditorTracking = false;
            FilterArchiving = false;
            FilterSubtypes = false;
            FilterAttributeRules = false;
            ApplyFilterAndSearch();
            ShowDetails = false;
            StatusText = _allDatasets.Count > 0 ? $"Showing all {_allDatasets.Count} items" : "Select a connection";
        }

        private void ReportProgress(string t) => RunOnUI(() => ProgressText = t);
        private void ReportStatus(string t) => RunOnUI(() => StatusText = t);
        private void InvalidateCommands() => RunOnUI(() => CommandManager.InvalidateRequerySuggested());

        /// <summary>
        /// Safely execute work on the UI thread without crashing if the dispatcher is unavailable
        /// during startup/shutdown edge-cases.
        /// </summary>
        private static void RunOnUI(Action a)
        {
            if (a == null) return;

            try
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;

                // During ArcGIS Pro startup/shutdown, Application.Current/Dispatcher can be unavailable.
                // These updates are best-effort only, so safely skip if the dispatcher is not ready.
                if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                    return;

                if (dispatcher.CheckAccess())
                {
                    a();
                }
                else
                {
                    // Use async dispatch to avoid potential deadlocks during startup sequencing.
                    dispatcher.BeginInvoke(a);
                }
            }
            catch (TaskCanceledException)
            {
                // UI is shutting down; ignore best-effort updates
            }
            catch (ObjectDisposedException)
            {
                // Dispatcher disposed during shutdown; ignore best-effort updates
            }
            catch (InvalidOperationException)
            {
                // Dispatcher is not available or is shutting down
            }
            catch (Exception ex)
            {
                LogNonFatal("RunOnUI", ex);
            }
        }

        private static void LogNonFatal(string context, Exception ex)
        {
            try
            {
                var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex}\n";
                System.Diagnostics.Debug.WriteLine($"SDE Search non-fatal error {context}: {ex}");

                var cacheDir = SdeSearchCache.GetCacheDir();
                var logFile = Path.Combine(cacheDir, "addin.log");
                File.AppendAllText(logFile, message);
            }
            catch
            {
                // Never throw from logging
            }
        }

        private static string GetSimpleName(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            var i = n.LastIndexOf('.');
            return i >= 0 ? n[(i + 1)..] : n;
        }

        private static string FormatCacheAge(DateTime? cachedAt)
        {
            if (!cachedAt.HasValue) return "unknown";

            var age = DateTime.UtcNow - cachedAt.Value;

            if (age.TotalMinutes < 1) return "just now";
            if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes}m ago";
            if (age.TotalHours < 24) return $"{(int)age.TotalHours}h ago";
            if (age.TotalDays < 7) return $"{(int)age.TotalDays}d ago";
            if (age.TotalDays < 30) return $"{(int)(age.TotalDays / 7)}w ago";

            return cachedAt.Value.ToLocalTime().ToString("MMM d");
        }

        private static string GetFieldTypeIcon(FieldType ft) => ft switch
        {
            FieldType.String => "Abc",
            FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger => "123",
            FieldType.Double or FieldType.Single => "1.2",
            FieldType.Date or FieldType.DateOnly or FieldType.TimeOnly or FieldType.TimestampOffset => "Cal",
            FieldType.OID => "Key",
            FieldType.GlobalID or FieldType.GUID => "ID",
            FieldType.Geometry => "Geo",
            _ => "•"
        };

        private static List<FieldInfo> EnumerateFields(TableDefinition tableDef)
        {
            var fields = new List<FieldInfo>();
            try
            {
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

                    try
                    {
                        var domain = field.GetDomain();
                        if (domain != null)
                        {
                            fi.DomainName = domain.GetName();
                            if (domain is CodedValueDomain cvd)
                            {
                                fi.DomainType = "Coded Value";
                                var pairs = cvd.GetCodedValuePairs();
                                fi.DomainInfo = $"Coded Value ({pairs.Count} values)";
                                foreach (var kv in pairs)
                                    fi.DomainValues.Add(new DomainCodeValue { Code = kv.Key?.ToString() ?? "", Value = kv.Value?.ToString() ?? "" });
                            }
                            else if (domain is RangeDomain rd)
                            {
                                fi.DomainType = "Range";
                                fi.DomainInfo = $"Range: {rd.GetMinValue()} – {rd.GetMaxValue()}";
                            }
                        }
                    }
                    catch { }

                    try
                    {
                        var dv = field.GetDefaultValue();
                        if (dv != null) fi.DefaultValue = dv.ToString();
                    }
                    catch { }

                    fields.Add(fi);
                }
            }
            catch { }
            return fields;
        }

        private void SaveThemePreference()
        {
            try
            {
                var dir = SdeSearchCache.GetCacheDir();
                File.WriteAllText(Path.Combine(dir, "theme.txt"), IsDarkMode ? "dark" : "light");
            }
            catch { }
        }

        private void LoadThemePreference()
        {
            try
            {
                var file = Path.Combine(SdeSearchCache.GetCacheDir(), "theme.txt");
                if (File.Exists(file))
                {
                    IsDarkMode = File.ReadAllText(file).Trim().Equals("dark", StringComparison.OrdinalIgnoreCase);
                    return;
                }
            }
            catch { }

            // No saved preference — auto-detect from ArcGIS Pro / Windows
            IsDarkMode = DetectSystemDarkMode();
        }

        private void LoadMetadataSettings()
        {
            string cacheDir;
            try { cacheDir = SdeSearchCache.GetCacheDir(); }
            catch { _metadataSettings = HardcodedDefaultMetadataSettings(); return; }

            var userFile = Path.Combine(cacheDir, "metadata_settings.json");

            // Seed the user-editable copy from the bundled default on first run
            if (!File.Exists(userFile))
            {
                try
                {
                    var bundled = Path.Combine(
                        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                        "metadata_settings.json");
                    if (File.Exists(bundled))
                        File.Copy(bundled, userFile);
                }
                catch { }
            }

            // Load from the user copy, falling back to the bundled file
            var toLoad = File.Exists(userFile) ? userFile
                : Path.Combine(
                    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                    "metadata_settings.json");

            try
            {
                if (File.Exists(toLoad))
                {
                    var json = File.ReadAllText(toLoad);
                    _metadataSettings = JsonSerializer.Deserialize<MetadataSettings>(json)
                                        ?? new MetadataSettings();
                }
            }
            catch { }

            // Fall back to hardcoded defaults if file was missing or empty
            if (_metadataSettings.Fields.Count == 0)
                _metadataSettings = HardcodedDefaultMetadataSettings();
        }

        private static MetadataSettings HardcodedDefaultMetadataSettings() => new MetadataSettings
        {
            Fields = new List<MetadataFieldConfig>
            {
                new() { Key = "FullName",         Label = "Full Name",         Visible = true },
                new() { Key = "Type",             Label = "Type",              Visible = true },
                new() { Key = "Alias",            Label = "Alias",             Visible = true },
                new() { Key = "Geometry",         Label = "Geometry",          Visible = true },
                new() { Key = "SpatialReference", Label = "Spatial Reference", Visible = true },
                new() { Key = "FieldCount",       Label = "Fields",            Visible = true },
                new() { Key = "Flags",            Label = "Flags",             Visible = true },
                new() { Key = "CreatedDate",      Label = "Metadata Created",  Visible = true },
                new() { Key = "ModifiedDate",     Label = "Metadata Modified", Visible = true },
                new() { Key = "Description",      Label = "Description",       Visible = true },
                new() { Key = "Summary",          Label = "Summary",           Visible = true },
                new() { Key = "Tags",             Label = "Tags",              Visible = true },
                new() { Key = "Credits",          Label = "Credits",           Visible = true },
                new() { Key = "UseConstraints",   Label = "Use Constraints",   Visible = true },
            }
        };

        private static bool DetectSystemDarkMode()
        {
            // 1. Try ArcGIS Pro's application theme
            try
            {
                var theme = FrameworkApplication.ApplicationTheme;
                return theme == ApplicationTheme.Dark || theme == ApplicationTheme.HighContrast;
            }
            catch { }

            // 2. Fallback: read the Windows "AppsUseLightTheme" registry value
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    var val = key.GetValue("AppsUseLightTheme");
                    if (val is int i) return i == 0; // 0 = dark, 1 = light
                }
            }
            catch { }

            return true; // default to dark if detection fails
        }

        #endregion

        #region Lifecycle

        protected override async Task InitializeAsync()
        {
            try
            {
                LoadThemePreference();
                LoadMetadataSettings();

                // Re-load connections when a project is opened, since the DockPane may
                // initialize before the project is fully loaded (e.g. restored from a
                // previous session), causing Project.Current to be null or its items
                // to be unavailable during the initial LoadConnections() call.
                ProjectOpenedAsyncEvent.Subscribe(OnProjectOpened);

                await LoadConnections();
            }
            catch (Exception ex)
            {
                // Catch any unexpected initialisation error so that ArcGIS Pro is not
                // brought down by an unhandled exception from our override.  Surface the
                // problem as a status message the user can report.
                try { StatusText = $"Initialisation error — please report: {ex.GetType().Name}: {ex.Message}"; } catch { }
                System.Diagnostics.Debug.WriteLine($"[SDE Search] InitializeAsync error: {ex}");
            }

            await base.InitializeAsync();
        }

        private Task OnProjectOpened(ProjectEventArgs args)
        {
            _ = LoadConnections();
            return Task.CompletedTask;
        }

        internal static void Show()
        {
            try
            {
                var pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
                if (pane == null)
                {
                    MessageBox.Show("SDE Search pane could not be created.", "SDE Search");
                    return;
                }

                pane.Activate();
            }
            catch (Exception ex)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"SDE Search Show() error: {ex}");
                    MessageBox.Show($"Failed to open SDE Search pane.\n\n{ex.Message}", "SDE Search");
                }
                catch
                {
                    // Last-resort: never let show-button exceptions crash ArcGIS Pro.
                }
            }
        }

        #endregion
    }

    // ═══════════════════════════════════════════════════
    //  DISK CACHE
    // ═══════════════════════════════════════════════════

    public static class SdeSearchCache
    {
        public static string GetCacheDir()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProAppAddInSdeSearch", "Cache");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string GetSeedCacheFile()
        {
            // Look for seed cache in the add-in installation directory
            try
            {
                var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var addInDir = Path.GetDirectoryName(assemblyPath);
                return Path.Combine(addInDir, "SeedCache.json");
            }
            catch
            {
                return null;
            }
        }

        private static string GetCacheFile(string connectionPath)
        {
            // Use MD5 to create a stable, deterministic filename hash.
            // string.GetHashCode() is randomized per process in .NET 8
            // and produces different values on each app launch.
            using var md5 = System.Security.Cryptography.MD5.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(connectionPath.ToUpperInvariant());
            var hashBytes = md5.ComputeHash(bytes);
            var hash = BitConverter.ToString(hashBytes).Replace("-", "");
            var name = Path.GetFileNameWithoutExtension(connectionPath);
            return Path.Combine(GetCacheDir(), $"{name}_{hash}.json");
        }

        public static void Save(string connectionPath, List<SdeDatasetItem> items)
        {
            try
            {
                var wrapper = new CacheWrapper
                {
                    CacheVersion       = CurrentCacheVersion,
                    ConnectionPath     = connectionPath,
                    CachedAt           = DateTime.UtcNow,
                    SeedCacheTimestamp = null,   // null = user-generated; seed will never replace this cache
                    Datasets           = items
                };
                var json = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(GetCacheFile(connectionPath), json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cache save error: {ex.Message}");
            }
        }

        public static List<SdeDatasetItem> Load(string connectionPath, out DateTime? cachedAt, out bool usedSeedCache)
        {
            cachedAt = null;
            usedSeedCache = false;

            try
            {
                var file     = GetCacheFile(connectionPath);
                var seedFile = GetSeedCacheFile();
                bool hasSeed = !string.IsNullOrEmpty(seedFile) && File.Exists(seedFile);

                // ── If a user cache exists, check whether it is an outdated seeded copy ──
                // A seeded cache stores SeedCacheTimestamp = the seed's CachedAt.
                // If the deployed seed now has a different CachedAt, the seed was updated
                // since this user last loaded — replace their copy with the new seed.
                if (File.Exists(file) && hasSeed)
                {
                    try
                    {
                        var existingWrapper = JsonSerializer.Deserialize<CacheWrapper>(File.ReadAllText(file));
                        if (existingWrapper?.SeedCacheTimestamp != null) // was seeded, not a manual refresh
                        {
                            var seedWrapper = JsonSerializer.Deserialize<CacheWrapper>(File.ReadAllText(seedFile));
                            if (seedWrapper != null && seedWrapper.CachedAt != existingWrapper.SeedCacheTimestamp)
                            {
                                // New seed deployed — discard the stale seeded copy
                                System.Diagnostics.Debug.WriteLine($"Seed cache updated ({existingWrapper.SeedCacheTimestamp} → {seedWrapper.CachedAt}); replacing user cache for {connectionPath}");
                                File.Delete(file);
                            }
                        }
                    }
                    catch { /* non-fatal: fall through and use existing cache */ }
                }

                // ── If no user cache (new user, or just deleted above), apply seed ──
                if (!File.Exists(file))
                {
                    if (!hasSeed) return null;
                    try
                    {
                        // Deserialize the seed, stamp SeedCacheTimestamp so we can detect
                        // future seed updates, then write it as the user's cache file.
                        var seedWrapper = JsonSerializer.Deserialize<CacheWrapper>(File.ReadAllText(seedFile));
                        if (seedWrapper?.Datasets == null) return null;
                        seedWrapper.SeedCacheTimestamp = seedWrapper.CachedAt;
                        File.WriteAllText(file, JsonSerializer.Serialize(seedWrapper,
                            new JsonSerializerOptions { WriteIndented = false }));
                        usedSeedCache = true;
                        System.Diagnostics.Debug.WriteLine($"Initialized cache from seed file for {connectionPath}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Seed cache copy error: {ex.Message}");
                        return null;
                    }
                }

                var json = File.ReadAllText(file);
                var wrapper = JsonSerializer.Deserialize<CacheWrapper>(json);
                if (wrapper?.Datasets == null) return null;

                // Reject stale cache from older schema versions
                if (wrapper.CacheVersion < CurrentCacheVersion)
                {
                    try { File.Delete(file); } catch { }
                    return null;
                }

                cachedAt = wrapper.CachedAt;

                // Restore connection path on each item (not serialized to save space)
                foreach (var d in wrapper.Datasets)
                    d.ConnectionPath = connectionPath;

                return wrapper.Datasets;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cache load error: {ex.Message}");
                return null;
            }
        }

        public static DateTime? GetCachedAt(string connectionPath)
        {
            try
            {
                var file = GetCacheFile(connectionPath);
                if (!File.Exists(file)) return null;

                var json = File.ReadAllText(file);
                var wrapper = JsonSerializer.Deserialize<CacheWrapper>(json);
                return wrapper?.CachedAt;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Returns the ConnectionPath, CachedAt, and per-type counts stored in the bundled SeedCache.json.</summary>
        public static (string ConnectionPath, DateTime? CachedAt, int FCs, int Tables, int FDs, int Rels) GetSeedCacheInfo()
        {
            try
            {
                var seedFile = GetSeedCacheFile();
                if (string.IsNullOrEmpty(seedFile) || !File.Exists(seedFile))
                    return (null, null, 0, 0, 0, 0);
                var json = File.ReadAllText(seedFile);
                var wrapper = JsonSerializer.Deserialize<CacheWrapper>(json);
                if (wrapper?.Datasets == null) return (null, null, 0, 0, 0, 0);
                int fcs   = wrapper.Datasets.Count(d => d.DatasetType == "Feature Class");
                int tbls  = wrapper.Datasets.Count(d => d.DatasetType == "Table");
                int fds   = wrapper.Datasets.Count(d => d.DatasetType == "Feature Dataset");
                int rels  = wrapper.Datasets.Count(d => d.DatasetType == "Relationship Class");
                return (wrapper.ConnectionPath, wrapper.CachedAt, fcs, tbls, fds, rels);
            }
            catch { return (null, null, 0, 0, 0, 0); }
        }

        // Increment when the cache schema changes (e.g. new properties on SdeDatasetItem)
        // v6: Fields removed from cache — lazy-loaded on demand instead
        // v7: SeedCacheTimestamp added to detect updated seed deployments
        private const int CurrentCacheVersion = 7;

        private class CacheWrapper
        {
            public int CacheVersion { get; set; }
            public string ConnectionPath { get; set; }
            public DateTime CachedAt { get; set; }
            /// <summary>
            /// Set to the seed's CachedAt when this file was populated from SeedCache.json.
            /// Null when the user generated this cache via a live database refresh.
            /// Used to detect when a new seed deployment should replace this file.
            /// </summary>
            public DateTime? SeedCacheTimestamp { get; set; }
            public List<SdeDatasetItem> Datasets { get; set; }
        }
    }

    // ═══════════════════════════════════════════════════
    //  DATA MODELS
    // ═══════════════════════════════════════════════════

    public class SdeConnectionItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string ConnectionType { get; set; }
        public string DisplayName => ConnectionType == "Manual" ? $"{Name}  (manual)" : Name;
        public override string ToString() => DisplayName;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class SdeDatasetItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string SimpleName { get; set; }
        public string AliasName { get; set; }
        public string DatasetType { get; set; }
        public string GeometryType { get; set; }

        /// <summary>
        /// Icon key used by XAML to select the vector geometry icon.
        /// Values: Point, Polyline, Polygon, Multipatch, Table, Dataset, Relationship, Unknown
        /// </summary>
        public string GeometryIconType { get; set; }

        [JsonIgnore] public string ConnectionPath { get; set; }
        public string FeatureDatasetName { get; set; }
        public bool CanAddToMap { get; set; }
        public int FieldCount { get; set; }
        public string SpatialReference { get; set; }

        // Flags
        public bool HasEditorTracking { get; set; }
        public bool IsArchived { get; set; }
        public bool HasSubtypes { get; set; }
        public bool HasAttributeRules { get; set; }

        // Fields are lazy-loaded on demand when the user opens the detail view;
        // they are never written to the cache JSON.
        [JsonIgnore]
        public List<FieldInfo> Fields { get; set; } = new List<FieldInfo>();

        // Pre-computed uppercase search index strings — built after load, never cached.
        // Using already-uppercased strings + Ordinal comparison is faster than OrdinalIgnoreCase
        // on raw strings, and avoids repeated null checks inside the hot filter loop.
        [JsonIgnore] public string NameText { get; set; }  // Name + SimpleName + AliasName
        [JsonIgnore] public string MetaText { get; set; }  // all searchable fields combined
        [JsonIgnore] public string TagsText { get; set; }  // Tags only

        // Dates (from ArcGIS metadata XML)
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }

        // Metadata (lazy)
        public bool HasMetadata { get; set; }
        public string Description { get; set; }
        public string Summary { get; set; }
        public string Purpose { get; set; }
        public string Tags { get; set; }
        public string Credits { get; set; }
        public string UseConstraints { get; set; }
        public string MetadataSnippet { get; set; }
        [JsonIgnore] public string RawMetadataXml { get; set; }

        [JsonIgnore]
        public string Subtitle
        {
            get
            {
                var p = new List<string> { DatasetType };
                if (!string.IsNullOrWhiteSpace(GeometryType)) p.Add(GeometryType);
                if (FieldCount > 0) p.Add($"{FieldCount} fields");
                return string.Join(" · ", p);
            }
        }

        [JsonIgnore]
        public bool HasFlags => HasEditorTracking || IsArchived || HasSubtypes || HasAttributeRules;

        [JsonIgnore]
        public bool HasBadges => HasFlags || !string.IsNullOrEmpty(FeatureDatasetName);

        [JsonIgnore]
        public string FeatureDatasetSimpleName
        {
            get
            {
                if (string.IsNullOrEmpty(FeatureDatasetName)) return null;
                var i = FeatureDatasetName.LastIndexOf('.');
                return i >= 0 ? FeatureDatasetName[(i + 1)..] : FeatureDatasetName;
            }
        }

        [JsonIgnore]
        public string CreatedDateDisplay
        {
            get
            {
                if (!CreatedDate.HasValue) return null;
                var age = DateTime.UtcNow - CreatedDate.Value;
                if (age.TotalDays < 1) return "Created today";
                if (age.TotalDays < 2) return "Created yesterday";
                if (age.TotalDays < 30) return $"Created {(int)age.TotalDays}d ago";
                return $"Created {CreatedDate.Value:yyyy-MM-dd}";
            }
        }

        [JsonIgnore]
        public bool HasCreatedDate => CreatedDate.HasValue;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
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
        [JsonIgnore] public bool HasDomain => !string.IsNullOrEmpty(DomainName);
        public ObservableCollection<DomainCodeValue> DomainValues { get; set; } = new ObservableCollection<DomainCodeValue>();
        [JsonIgnore] public bool HasDomainValues => DomainValues != null && DomainValues.Count > 0;

        [JsonIgnore]
        public string FieldSummary
        {
            get
            {
                var p = new List<string> { FieldType };
                if (Length > 0 && FieldType == "String") p.Add($"({Length})");
                if (!IsNullable) p.Add("NOT NULL");
                if (HasDomain) p.Add($"[{DomainName}]");
                return string.Join(" ", p);
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class DomainCodeValue
    {
        public string Code { get; set; }
        public string Value { get; set; }
    }

    // ═══════════════════════════════════════════════════
    //  METADATA SETTINGS
    // ═══════════════════════════════════════════════════

    internal class MetadataFieldConfig
    {
        [JsonInclude] public string Key     { get; set; } = "";
        [JsonInclude] public string Label   { get; set; } = "";
        [JsonInclude] public bool   Visible { get; set; } = true;
    }

    internal class MetadataSettings
    {
        [JsonInclude] public List<MetadataFieldConfig> Fields { get; set; } = new();
    }

    // ═══════════════════════════════════════════════════
    //  ITEM TYPE FILTER OPTION
    // ═══════════════════════════════════════════════════

    internal class ItemTypeOption
    {
        public string Key { get; set; }      // GeometryIconType value, or "All"
        public string Display { get; set; }  // "All (500)", "Polygon (200)"
        public override string ToString() => Display;
    }

    // ═══════════════════════════════════════════════════
    //  COMMANDS
    // ═══════════════════════════════════════════════════

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        { _execute = execute ?? throw new ArgumentNullException(nameof(execute)); _canExecute = canExecute; }
        public event EventHandler CanExecuteChanged
        { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        public bool CanExecute(object p) => _canExecute?.Invoke() ?? true;
        public void Execute(object p) => _execute();
    }

    internal class Dockpane1_ShowButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                SdeSearchPaneViewModel.Show();
            }
            catch (Exception ex)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"SDE Search show button error: {ex}");
                    MessageBox.Show($"SDE Search failed to open.\n\n{ex.Message}", "SDE Search");
                }
                catch
                {
                    // Never throw from button click handlers.
                }
            }
        }
    }
}