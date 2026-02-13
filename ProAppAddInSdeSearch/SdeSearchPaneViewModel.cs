using ArcGIS.Core.Data;
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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        // ── Full dataset cache ────────────────────────
        private List<SdeDatasetItem> _allDatasets = new List<SdeDatasetItem>();

        // ── Visible results ───────────────────────────
        private ObservableCollection<SdeDatasetItem> _searchResults = new ObservableCollection<SdeDatasetItem>();
        private SdeDatasetItem _selectedResult;

        // ── Detail view ───────────────────────────────
        private bool _showDetails;
        private ObservableCollection<FieldInfo> _detailFields = new ObservableCollection<FieldInfo>();
        private string _detailMetadata = "";
        private bool _isLoadingDetails;

        // ── Filters ───────────────────────────────────
        private bool _showFeatureClasses = true;
        private bool _showTables = true;
        private bool _showFeatureDatasets = true;

        protected SdeSearchPaneViewModel()
        {
            SearchCommand = new RelayCommand(() => ApplyFilterAndSearch(), () => !IsSearching);
            ClearCommand = new RelayCommand(() => ClearSearch(), () => true);
            RefreshConnectionsCommand = new RelayCommand(() => _ = LoadConnections(), () => !IsSearching);
            ReloadDataCommand = new RelayCommand(() => _ = LoadAllDatasets(forceRefresh: true), () => !IsSearching && SelectedConnection != null);
            AddToMapCommand = new RelayCommand(() => _ = AddSelectedToMap(), () => SelectedResult != null && SelectedResult.CanAddToMap);
            BackToResultsCommand = new RelayCommand(() => ShowDetails = false, () => ShowDetails);
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
            set { if (SetProperty(ref _showFeatureClasses, value)) ApplyFilterAndSearch(); }
        }

        public bool ShowTables
        {
            get => _showTables;
            set { if (SetProperty(ref _showTables, value)) ApplyFilterAndSearch(); }
        }

        public bool ShowFeatureDatasets
        {
            get => _showFeatureDatasets;
            set { if (SetProperty(ref _showFeatureDatasets, value)) ApplyFilterAndSearch(); }
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

                    ReportProgress("Scanning project connections...");
                    foreach (var pi in Project.Current.GetItems<GDBProjectItem>())
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

                    ScanFolder(items, Path.Combine(Project.Current.HomeFolderPath, "DatabaseConnections"), "Project Folder");
                    ScanFolder(items, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Esri", "ArcGISPro", "DatabaseConnections"), "ArcGIS Pro");

                    RunOnUI(() =>
                    {
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
                        if (Connections.Count == 1) SelectedConnection = Connections.First();
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

            IsSearching = true;
            ShowDetails = false;
            _allDatasets.Clear();

            var connPath = SelectedConnection.Path;
            var connName = SelectedConnection.Name;

            // ── Try loading from disk cache ──
            if (!forceRefresh)
            {
                ReportProgress("Checking cache...");
                var cached = SdeSearchCache.Load(connPath);
                if (cached != null && cached.Count > 0)
                {
                    _allDatasets = cached;
                    int fcs = cached.Count(d => d.DatasetType == "Feature Class");
                    int tbls = cached.Count(d => d.DatasetType == "Table");
                    int fds = cached.Count(d => d.DatasetType == "Feature Dataset");
                    int rels = cached.Count(d => d.DatasetType == "Relationship Class");

                    RunOnUI(() =>
                    {
                        ApplyFilterAndSearch();
                        IsSearching = false;
                        ProgressText = "";
                        StatusText = $"{connName} (cached): {fcs} FCs, {tbls} tables, {fds} datasets, {rels} relationships";
                    });
                    return;
                }
            }

            ReportProgress("Connecting to " + connName + "...");
            ReportStatus("Connecting...");

            await QueuedTask.Run(() =>
            {
                try
                {
                    using (var gdb = new Geodatabase(new DatabaseConnectionFile(new Uri(connPath))))
                    {
                        int total = 0;

                        // ── Feature Datasets ─────────────────────
                        ReportProgress("Enumerating feature datasets...");
                        try
                        {
                            foreach (var def in gdb.GetDefinitions<FeatureDatasetDefinition>())
                            {
                                try
                                {
                                    _allDatasets.Add(new SdeDatasetItem
                                    {
                                        Name = def.GetName(),
                                        SimpleName = GetSimpleName(def.GetName()),
                                        DatasetType = "Feature Dataset",
                                        GeometryIconType = "Dataset",
                                        CanAddToMap = false,
                                        ConnectionPath = connPath
                                    });
                                    total++;
                                }
                                catch { }
                            }
                        }
                        catch { }

                        ReportProgress($"{total} feature dataset(s). Enumerating feature classes...");

                        // ── Feature Classes (FAST: skip field count) ─
                        try
                        {
                            int fc = 0;
                            foreach (var def in gdb.GetDefinitions<FeatureClassDefinition>())
                            {
                                try
                                {
                                    var geomType = "Unknown";
                                    try { geomType = def.GetShapeType().ToString(); } catch { }

                                    var item = new SdeDatasetItem
                                    {
                                        Name = def.GetName(),
                                        SimpleName = GetSimpleName(def.GetName()),
                                        DatasetType = "Feature Class",
                                        GeometryType = geomType,
                                        GeometryIconType = MapGeometryIcon(geomType),
                                        CanAddToMap = true,
                                        ConnectionPath = connPath
                                    };

                                    try { item.SpatialReference = def.GetSpatialReference()?.Name; } catch { }
                                    try { item.AliasName = def.GetAliasName(); } catch { }

                                    _allDatasets.Add(item);
                                    total++; fc++;
                                    if (fc % 50 == 0) ReportProgress($"{total} items ({fc} feature classes)...");
                                }
                                catch { }
                            }
                        }
                        catch { }

                        ReportProgress($"{total} items. Enumerating tables...");

                        // ── Tables ───────────────────────────────
                        try
                        {
                            var fcNames = new HashSet<string>(
                                _allDatasets.Where(d => d.DatasetType == "Feature Class").Select(d => d.Name),
                                StringComparer.OrdinalIgnoreCase);

                            int tc = 0;
                            foreach (var def in gdb.GetDefinitions<TableDefinition>())
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

                                    _allDatasets.Add(item);
                                    total++; tc++;
                                    if (tc % 50 == 0) ReportProgress($"{total} items ({tc} tables)...");
                                }
                                catch { }
                            }
                        }
                        catch { }

                        // ── Relationship Classes ─────────────────
                        ReportProgress($"{total} items. Enumerating relationships...");
                        try
                        {
                            foreach (var def in gdb.GetDefinitions<RelationshipClassDefinition>())
                            {
                                try
                                {
                                    _allDatasets.Add(new SdeDatasetItem
                                    {
                                        Name = def.GetName(),
                                        SimpleName = GetSimpleName(def.GetName()),
                                        DatasetType = "Relationship Class",
                                        GeometryIconType = "Relationship",
                                        CanAddToMap = false,
                                        ConnectionPath = connPath,
                                        MetadataSnippet = $"Origin: {GetSimpleName(def.GetOriginClass())} → Dest: {GetSimpleName(def.GetDestinationClass())}"
                                    });
                                    total++;
                                }
                                catch { }
                            }
                        }
                        catch { }

                        // ── Sort ─────────────────────────────────
                        ReportProgress($"Loaded {total} items. Sorting...");
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

                        // ── Save cache ───────────────────────────
                        ReportProgress("Saving to cache...");
                        SdeSearchCache.Save(connPath, _allDatasets);

                        int fcs = _allDatasets.Count(d => d.DatasetType == "Feature Class");
                        int tbls = _allDatasets.Count(d => d.DatasetType == "Table");
                        int fds = _allDatasets.Count(d => d.DatasetType == "Feature Dataset");
                        int rels = _allDatasets.Count(d => d.DatasetType == "Relationship Class");

                        RunOnUI(() =>
                        {
                            ApplyFilterAndSearch();
                            IsSearching = false;
                            ProgressText = "";
                            StatusText = $"{connName}: {fcs} FCs, {tbls} tables, {fds} datasets, {rels} relationships";
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

        private void ApplyFilterAndSearch()
        {
            string term = (SearchText ?? "").Trim();
            bool wildcard = string.IsNullOrEmpty(term);

            var filtered = _allDatasets.Where(item =>
            {
                if (!ShowFeatureClasses && item.DatasetType == "Feature Class") return false;
                if (!ShowTables && item.DatasetType == "Table") return false;
                if (!ShowFeatureDatasets && item.DatasetType == "Feature Dataset") return false;
                if (wildcard) return true;
                return MatchesSearch(item, term);
            }).ToList();

            RunOnUI(() =>
            {
                SearchResults.Clear();
                foreach (var r in filtered) SearchResults.Add(r);
                ResultCount = filtered.Count;
                if (!wildcard)
                    StatusText = $"Found {filtered.Count} of {_allDatasets.Count} matching \"{term}\"";
            });
        }

        private bool MatchesSearch(SdeDatasetItem item, string searchTerm)
        {
            var terms = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var term in terms)
            {
                bool matched = false;
                if (SearchByName)
                {
                    matched = Contains(item.Name, term) || Contains(item.SimpleName, term) || Contains(item.AliasName, term);
                }
                if (!matched && SearchByMetadata)
                {
                    matched = Contains(item.Description, term) || Contains(item.Summary, term) ||
                              Contains(item.Tags, term) || Contains(item.Purpose, term) ||
                              Contains(item.Credits, term) || Contains(item.MetadataSnippet, term) ||
                              Contains(item.DatasetType, term);
                }
                if (!matched) return false;
            }
            return true;
        }

        private static bool Contains(string source, string value) =>
            source?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

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

                            // ── Field count (update cached value) ─
                            try { item.FieldCount = tableDef.GetFields().Count; } catch { }

                            // ── Editor tracking detection ─────────
                            DetectEditorTracking(item, tableDef);

                            // ── Archiving detection ──────────────
                            DetectArchiving(item, tableDef, gdb);

                            // ── Created / Modified dates from metadata ─
                            DetectDates(item);

                            // ── Load all fields ──────────────────
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
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Field enumeration error: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Detail load error: {ex.Message}");
                    RunOnUI(() => { DetailMetadata = $"Error loading details: {ex.Message}"; });
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

        private void DetectEditorTracking(SdeDatasetItem item, TableDefinition tableDef)
        {
            try
            {
                var fieldNames = tableDef.GetFields().Select(f => f.Name.ToUpperInvariant()).ToHashSet();
                item.HasEditorTracking =
                    fieldNames.Contains("CREATED_USER") && fieldNames.Contains("CREATED_DATE") &&
                    fieldNames.Contains("LAST_EDITED_USER") && fieldNames.Contains("LAST_EDITED_DATE");
            }
            catch { }
        }

        private void DetectArchiving(SdeDatasetItem item, TableDefinition tableDef, Geodatabase gdb)
        {
            try
            {
                // Check for archiving fields
                var fieldNames = tableDef.GetFields().Select(f => f.Name.ToUpperInvariant()).ToHashSet();
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

            lines.Add($"Full Name: {item.Name}");
            lines.Add($"Type: {item.DatasetType}");

            if (!string.IsNullOrWhiteSpace(item.AliasName) && item.AliasName != item.SimpleName)
                lines.Add($"Alias: {item.AliasName}");
            if (!string.IsNullOrWhiteSpace(item.GeometryType))
                lines.Add($"Geometry: {item.GeometryType}");
            if (!string.IsNullOrWhiteSpace(item.SpatialReference))
                lines.Add($"Spatial Reference: {item.SpatialReference}");
            if (item.FieldCount > 0)
                lines.Add($"Fields: {item.FieldCount}");

            // ── Flags ────────────────────────────
            var flags = new List<string>();
            if (item.HasEditorTracking) flags.Add("✓ Editor Tracking");
            if (item.IsArchived) flags.Add("✓ Archiving");
            if (flags.Count > 0)
                lines.Add($"Flags: {string.Join("  |  ", flags)}");

            // ── Dates ────────────────────────────
            if (item.CreatedDate.HasValue)
                lines.Add($"Created: {item.CreatedDate.Value:yyyy-MM-dd}");
            if (item.ModifiedDate.HasValue)
                lines.Add($"Last Modified: {item.ModifiedDate.Value:yyyy-MM-dd}");

            // ── Metadata content ─────────────────
            if (item.HasMetadata)
            {
                if (!string.IsNullOrWhiteSpace(item.Description))
                    lines.Add($"\nDescription:\n{item.Description}");
                if (!string.IsNullOrWhiteSpace(item.Summary) && item.Summary != item.Description)
                    lines.Add($"\nSummary:\n{item.Summary}");
                if (!string.IsNullOrWhiteSpace(item.Tags))
                    lines.Add($"\nTags: {item.Tags}");
                if (!string.IsNullOrWhiteSpace(item.Credits))
                    lines.Add($"\nCredits: {item.Credits}");
                if (!string.IsNullOrWhiteSpace(item.UseConstraints))
                    lines.Add($"\nUse Constraints: {item.UseConstraints}");
            }
            else
            {
                lines.Add("\n(No metadata available)");
            }

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
                // TODO: GetDescription() doesn't exist on TableDefinition.
                // Need to use Item.GetXml() instead - requires getting the Item from catalog.
                // See: https://pro.arcgis.com/en/pro-app/latest/sdk/api-reference/topic17180.html
                // Temporarily disabled to fix build error.
                // string xml = definition.GetDescription();
                // if (string.IsNullOrEmpty(xml)) return;
                // item.HasMetadata = true;
                // item.RawMetadataXml = xml;
                // ParseMetadataXml(item, xml);
            }
            catch { }
        }

        private void TryLoadMetadata(SdeDatasetItem item, FeatureDatasetDefinition definition)
        {
            try
            {
                // TODO: GetDescription() doesn't exist on FeatureDatasetDefinition.
                // Need to use Item.GetXml() instead - requires getting the Item from catalog.
                // See: https://pro.arcgis.com/en/pro-app/latest/sdk/api-reference/topic17180.html
                // Temporarily disabled to fix build error.
                // string xml = definition.GetDescription();
                // if (string.IsNullOrEmpty(xml)) return;
                // item.HasMetadata = true;
                // item.RawMetadataXml = xml;
                // ParseMetadataXml(item, xml);
            }
            catch { }
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
            try { var n = doc.SelectSingleNode(xpath); return n != null && !string.IsNullOrWhiteSpace(n.InnerText) ? n.InnerText.Trim() : null; }
            catch { return null; }
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
                    if (map == null) { ReportStatus("No active map."); return; }
                    var uri = new Uri(item.ConnectionPath + "\\" + item.Name);
                    if (item.DatasetType == "Feature Class")
                        LayerFactory.Instance.CreateLayer(uri, map);
                    else if (item.DatasetType == "Table")
                        StandaloneTableFactory.Instance.CreateStandaloneTable(uri, map);
                    ReportStatus($"✓ Added \"{item.SimpleName}\" to map");
                }
                catch (Exception ex) { ReportStatus($"Error: {ex.Message}"); }
            });
        }

        private void CopySelectedPath()
        {
            if (SelectedResult == null) return;
            try { System.Windows.Clipboard.SetText($"{SelectedResult.ConnectionPath}\\{SelectedResult.Name}"); StatusText = "Copied path to clipboard"; }
            catch (Exception ex) { StatusText = $"Copy error: {ex.Message}"; }
        }

        #endregion

        // ═══════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════

        #region Helpers

        private void ClearSearch()
        {
            SearchText = "";
            ApplyFilterAndSearch();
            ShowDetails = false;
            StatusText = _allDatasets.Count > 0 ? $"Showing all {_allDatasets.Count} items" : "Select a connection";
        }

        private void ReportProgress(string t) => RunOnUI(() => ProgressText = t);
        private void ReportStatus(string t) => RunOnUI(() => StatusText = t);
        private void InvalidateCommands() => RunOnUI(() => CommandManager.InvalidateRequerySuggested());
        private static void RunOnUI(Action a) => System.Windows.Application.Current.Dispatcher.Invoke(a);

        private static string GetSimpleName(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            var i = n.LastIndexOf('.');
            return i >= 0 ? n[(i + 1)..] : n;
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
                    IsDarkMode = File.ReadAllText(file).Trim().Equals("dark", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
        }

        #endregion

        #region Lifecycle

        protected override Task InitializeAsync()
        {
            LoadThemePreference();
            _ = LoadConnections();
            return base.InitializeAsync();
        }

        internal static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
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

        private static string GetCacheFile(string connectionPath)
        {
            // Hash the connection path to create a stable filename
            var hash = connectionPath.GetHashCode().ToString("X8");
            var name = Path.GetFileNameWithoutExtension(connectionPath);
            return Path.Combine(GetCacheDir(), $"{name}_{hash}.json");
        }

        public static void Save(string connectionPath, List<SdeDatasetItem> items)
        {
            try
            {
                var wrapper = new CacheWrapper
                {
                    ConnectionPath = connectionPath,
                    CachedAt = DateTime.UtcNow,
                    Datasets = items
                };
                var json = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(GetCacheFile(connectionPath), json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cache save error: {ex.Message}");
            }
        }

        public static List<SdeDatasetItem> Load(string connectionPath)
        {
            try
            {
                var file = GetCacheFile(connectionPath);
                if (!File.Exists(file)) return null;

                var json = File.ReadAllText(file);
                var wrapper = JsonSerializer.Deserialize<CacheWrapper>(json);
                if (wrapper?.Datasets == null) return null;

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

        private class CacheWrapper
        {
            public string ConnectionPath { get; set; }
            public DateTime CachedAt { get; set; }
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
        public bool CanAddToMap { get; set; }
        public int FieldCount { get; set; }
        public string SpatialReference { get; set; }

        // Flags
        public bool HasEditorTracking { get; set; }
        public bool IsArchived { get; set; }

        // Dates
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
                var flags = new List<string>();
                if (HasEditorTracking) flags.Add("ET");
                if (IsArchived) flags.Add("Arch");
                if (flags.Count > 0) p.Add(string.Join("/", flags));
                return string.Join(" · ", p);
            }
        }

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
        public bool HasDomain => !string.IsNullOrEmpty(DomainName);

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
        protected override void OnClick() => SdeSearchPaneViewModel.Show();
    }
}