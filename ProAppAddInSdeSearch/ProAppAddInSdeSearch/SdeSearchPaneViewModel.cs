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
        private bool _searchByMetadata = true;
        private bool _isSearching;
        private string _statusText = "Select a connection and search";
        private int _resultCount;

        // Connection state
        private ObservableCollection<SdeConnectionItem> _connections = new ObservableCollection<SdeConnectionItem>();
        private SdeConnectionItem _selectedConnection;

        // Results
        private ObservableCollection<SdeDatasetItem> _searchResults = new ObservableCollection<SdeDatasetItem>();
        private SdeDatasetItem _selectedResult;

        // Detail view
        private bool _showDetails;
        private ObservableCollection<FieldInfo> _detailFields = new ObservableCollection<FieldInfo>();
        private string _detailMetadata = "";

        // Filter
        private bool _showFeatureClasses = true;
        private bool _showTables = true;
        private bool _showRasterDatasets = true;
        private bool _showFeatureDatasets = true;

        protected SdeSearchPaneViewModel()
        {
            SearchCommand = new RelayCommand(() => _ = ExecuteSearch(), () => !IsSearching && SelectedConnection != null);
            ClearCommand = new RelayCommand(() => ClearSearch(), () => true);
            RefreshConnectionsCommand = new RelayCommand(() => _ = LoadConnections(), () => !IsSearching);
            AddToMapCommand = new RelayCommand(() => _ = AddSelectedToMap(), () => SelectedResult != null && SelectedResult.CanAddToMap);
            BackToResultsCommand = new RelayCommand(() => ShowDetails = false, () => ShowDetails);
            CopyPathCommand = new RelayCommand(() => CopySelectedPath(), () => SelectedResult != null);
        }

        #region Commands
        public ICommand SearchCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand RefreshConnectionsCommand { get; }
        public ICommand AddToMapCommand { get; }
        public ICommand BackToResultsCommand { get; }
        public ICommand CopyPathCommand { get; }
        #endregion

        #region Properties

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    // Allow Enter key to trigger search
                }
            }
        }

        public bool SearchByName
        {
            get => _searchByName;
            set => SetProperty(ref _searchByName, value);
        }

        public bool SearchByMetadata
        {
            get => _searchByMetadata;
            set => SetProperty(ref _searchByMetadata, value);
        }

        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                if (SetProperty(ref _isSearching, value))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CommandManager.InvalidateRequerySuggested();
                    });
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
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
                    {
                        CommandManager.InvalidateRequerySuggested();
                    });
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
                    {
                        CommandManager.InvalidateRequerySuggested();
                    });
                }
            }
        }

        public bool ShowDetails
        {
            get => _showDetails;
            set
            {
                if (SetProperty(ref _showDetails, value))
                {
                    NotifyPropertyChanged(nameof(ShowResultsList));
                }
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

        public bool ShowFeatureClasses
        {
            get => _showFeatureClasses;
            set => SetProperty(ref _showFeatureClasses, value);
        }

        public bool ShowTables
        {
            get => _showTables;
            set => SetProperty(ref _showTables, value);
        }

        public bool ShowRasterDatasets
        {
            get => _showRasterDatasets;
            set => SetProperty(ref _showRasterDatasets, value);
        }

        public bool ShowFeatureDatasets
        {
            get => _showFeatureDatasets;
            set => SetProperty(ref _showFeatureDatasets, value);
        }

        #endregion

        #region Connection Loading

        /// <summary>
        /// Discover enterprise geodatabase connections from the project
        /// </summary>
        private async Task LoadConnections()
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    var connectionItems = new List<SdeConnectionItem>();

                    // Get database connection files from the project
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
                                    ConnectionType = "Enterprise Geodatabase"
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error reading project item: {ex.Message}");
                        }
                    }

                    // Also look for .sde files in the DatabaseConnections folder
                    try
                    {
                        var dbConnectionsFolder = System.IO.Path.Combine(
                            Project.Current.HomeFolderPath, "DatabaseConnections");

                        if (System.IO.Directory.Exists(dbConnectionsFolder))
                        {
                            foreach (var sdeFile in System.IO.Directory.GetFiles(dbConnectionsFolder, "*.sde"))
                            {
                                var name = System.IO.Path.GetFileNameWithoutExtension(sdeFile);
                                if (!connectionItems.Any(c => c.Path.Equals(sdeFile, StringComparison.OrdinalIgnoreCase)))
                                {
                                    connectionItems.Add(new SdeConnectionItem
                                    {
                                        Name = name,
                                        Path = sdeFile,
                                        ConnectionType = "Enterprise Geodatabase"
                                    });
                                }
                            }
                        }
                    }
                    catch { }

                    // Check the common ArcGIS database connections folder
                    try
                    {
                        var commonDbFolder = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "Esri", "ArcGISPro", "DatabaseConnections");

                        if (System.IO.Directory.Exists(commonDbFolder))
                        {
                            foreach (var sdeFile in System.IO.Directory.GetFiles(commonDbFolder, "*.sde"))
                            {
                                var name = System.IO.Path.GetFileNameWithoutExtension(sdeFile);
                                if (!connectionItems.Any(c => c.Path.Equals(sdeFile, StringComparison.OrdinalIgnoreCase)))
                                {
                                    connectionItems.Add(new SdeConnectionItem
                                    {
                                        Name = name,
                                        Path = sdeFile,
                                        ConnectionType = "Enterprise Geodatabase"
                                    });
                                }
                            }
                        }
                    }
                    catch { }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Connections.Clear();
                        foreach (var conn in connectionItems.OrderBy(c => c.Name))
                        {
                            Connections.Add(conn);
                        }

                        StatusText = connectionItems.Count > 0
                            ? $"Found {connectionItems.Count} connection(s) — select one and search"
                            : "No enterprise connections found. Add .sde connections to your project.";

                        if (Connections.Count == 1)
                            SelectedConnection = Connections.First();
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading connections: {ex.Message}");
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = $"Error loading connections: {ex.Message}";
                    });
                }
            });
        }

        #endregion

        #region Search Logic

        /// <summary>
        /// Execute search against the selected enterprise geodatabase
        /// </summary>
        private async Task ExecuteSearch()
        {
            if (SelectedConnection == null)
            {
                StatusText = "Please select a database connection first";
                return;
            }

            if (!SearchByName && !SearchByMetadata)
            {
                StatusText = "Please enable at least one search mode (Name or Metadata)";
                return;
            }

            IsSearching = true;
            StatusText = "Searching...";
            ShowDetails = false;

            await QueuedTask.Run(() =>
            {
                try
                {
                    var results = new List<SdeDatasetItem>();
                    string searchTerm = (SearchText ?? "").Trim();
                    bool isWildcard = string.IsNullOrEmpty(searchTerm);

                    using (var geodatabase = new Geodatabase(new DatabaseConnectionFile(new Uri(SelectedConnection.Path))))
                    {
                        // Search Feature Classes
                        if (ShowFeatureClasses)
                        {
                            try
                            {
                                var fcDefs = geodatabase.GetDefinitions<FeatureClassDefinition>();
                                foreach (var fcDef in fcDefs)
                                {
                                    try
                                    {
                                        var item = CreateDatasetItem(fcDef, "Feature Class", GetGeometryTypeIcon(fcDef.GetShapeType()));
                                        if (isWildcard || MatchesSearch(item, searchTerm))
                                        {
                                            item.CanAddToMap = true;
                                            results.Add(item);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error processing FC: {ex.Message}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error enumerating feature classes: {ex.Message}");
                            }
                        }

                        // Search Tables
                        if (ShowTables)
                        {
                            try
                            {
                                var tableDefs = geodatabase.GetDefinitions<TableDefinition>();
                                // Exclude feature classes (they also appear as TableDefinitions)
                                var fcNames = new HashSet<string>(
                                    geodatabase.GetDefinitions<FeatureClassDefinition>()
                                        .Select(f => f.GetName()),
                                    StringComparer.OrdinalIgnoreCase);

                                foreach (var tableDef in tableDefs)
                                {
                                    try
                                    {
                                        if (fcNames.Contains(tableDef.GetName()))
                                            continue;

                                        var item = CreateDatasetItem(tableDef, "Table", "📋");
                                        if (isWildcard || MatchesSearch(item, searchTerm))
                                        {
                                            item.CanAddToMap = true;
                                            results.Add(item);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error processing table: {ex.Message}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error enumerating tables: {ex.Message}");
                            }
                        }

                        // Search Feature Datasets
                        if (ShowFeatureDatasets)
                        {
                            try
                            {
                                var fdDefs = geodatabase.GetDefinitions<FeatureDatasetDefinition>();
                                foreach (var fdDef in fdDefs)
                                {
                                    try
                                    {
                                        var item = new SdeDatasetItem
                                        {
                                            Name = fdDef.GetName(),
                                            SimpleName = GetSimpleName(fdDef.GetName()),
                                            DatasetType = "Feature Dataset",
                                            TypeIcon = "📁",
                                            CanAddToMap = false,
                                            ConnectionPath = SelectedConnection.Path
                                        };

                                        // Try to get metadata
                                        TryLoadMetadata(item, fdDef);

                                        if (isWildcard || MatchesSearch(item, searchTerm))
                                        {
                                            results.Add(item);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error processing feature dataset: {ex.Message}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error enumerating feature datasets: {ex.Message}");
                            }
                        }

                        // Search Relationship Classes (shown as metadata info)
                        try
                        {
                            var relDefs = geodatabase.GetDefinitions<RelationshipClassDefinition>();
                            foreach (var relDef in relDefs)
                            {
                                try
                                {
                                    var item = new SdeDatasetItem
                                    {
                                        Name = relDef.GetName(),
                                        SimpleName = GetSimpleName(relDef.GetName()),
                                        DatasetType = "Relationship Class",
                                        TypeIcon = "🔗",
                                        CanAddToMap = false,
                                        ConnectionPath = SelectedConnection.Path,
                                        MetadataSnippet = $"Origin: {GetSimpleName(relDef.GetOriginClass())} → Dest: {GetSimpleName(relDef.GetDestinationClass())}"
                                    };

                                    if (isWildcard || MatchesSearch(item, searchTerm))
                                    {
                                        results.Add(item);
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }

                    // Sort results: feature classes first, then tables, then others
                    results = results
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

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        SearchResults.Clear();
                        foreach (var result in results)
                        {
                            SearchResults.Add(result);
                        }
                        ResultCount = results.Count;
                        StatusText = isWildcard
                            ? $"Showing all {results.Count} item(s)"
                            : $"Found {results.Count} item(s) matching \"{searchTerm}\"";
                        IsSearching = false;
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = $"Error: {ex.Message}";
                        IsSearching = false;
                    });
                }
            });
        }

        /// <summary>
        /// Create a dataset item from a table/feature class definition, including metadata
        /// </summary>
        private SdeDatasetItem CreateDatasetItem(TableDefinition tableDef, string datasetType, string icon)
        {
            var item = new SdeDatasetItem
            {
                Name = tableDef.GetName(),
                SimpleName = GetSimpleName(tableDef.GetName()),
                DatasetType = datasetType,
                TypeIcon = icon,
                ConnectionPath = SelectedConnection.Path,
                FieldCount = tableDef.GetFields().Count
            };

            // Get spatial reference info for feature classes
            if (tableDef is FeatureClassDefinition fcDef)
            {
                try
                {
                    var sr = fcDef.GetSpatialReference();
                    if (sr != null)
                    {
                        item.SpatialReference = sr.Name;
                    }
                    item.GeometryType = fcDef.GetShapeType().ToString();
                }
                catch { }
            }

            // Get alias name
            try
            {
                item.AliasName = tableDef.GetAliasName();
            }
            catch { }

            // Try to load metadata from the definition
            TryLoadMetadata(item, tableDef);

            return item;
        }

        /// <summary>
        /// Attempt to read metadata (description, tags, summary) from the definition
        /// Note: Geodatabase Definition objects don't expose metadata directly in ArcGIS Pro SDK.
        /// Metadata access requires using the Item API from the catalog/project context.
        /// </summary>
        private void TryLoadMetadata(SdeDatasetItem item, Definition definition)
        {
            try
            {
                // TODO: Metadata for geodatabase items needs to be accessed via Item API
                // Definition class doesn't have GetDescription() or GetMetadata() methods
                // Consider using MapView.Active.Map.GetLayersAsFlattenedList() and then
                // accessing layer.GetMetadata() for items added to the map

                // For now, metadata loading is disabled for direct geodatabase access
                item.HasMetadata = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading metadata for {item.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Parse metadata XML to extract description, summary, tags, purpose, credits
        /// </summary>
        private void ParseMetadataXml(SdeDatasetItem item, string xml)
        {
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(xml);

                // Try various metadata standard paths

                // ArcGIS metadata style
                item.Description = GetXmlNodeText(doc, "//dataIdInfo/idAbs") ??
                                   GetXmlNodeText(doc, "//dataIdInfo/idPurp") ??
                                   GetXmlNodeText(doc, "//idinfo/descript/abstract") ?? "";

                item.Summary = GetXmlNodeText(doc, "//dataIdInfo/idPurp") ??
                               GetXmlNodeText(doc, "//idinfo/descript/purpose") ?? "";

                item.Purpose = GetXmlNodeText(doc, "//dataIdInfo/idPurp") ?? "";

                item.Credits = GetXmlNodeText(doc, "//dataIdInfo/idCredit") ??
                               GetXmlNodeText(doc, "//idinfo/datacred") ?? "";

                // Tags / Keywords
                var tags = new List<string>();
                var themeNodes = doc.SelectNodes("//dataIdInfo/searchKeys/keyword");
                if (themeNodes != null)
                {
                    foreach (System.Xml.XmlNode node in themeNodes)
                    {
                        if (!string.IsNullOrWhiteSpace(node.InnerText))
                            tags.Add(node.InnerText.Trim());
                    }
                }

                // Also try FGDC-style keywords
                var fgdcTheme = doc.SelectNodes("//idinfo/keywords/theme/themekey");
                if (fgdcTheme != null)
                {
                    foreach (System.Xml.XmlNode node in fgdcTheme)
                    {
                        if (!string.IsNullOrWhiteSpace(node.InnerText) &&
                            !tags.Contains(node.InnerText.Trim(), StringComparer.OrdinalIgnoreCase))
                            tags.Add(node.InnerText.Trim());
                    }
                }

                item.Tags = string.Join(", ", tags);

                // Use constraints
                item.UseConstraints = GetXmlNodeText(doc, "//dataIdInfo/resConst/Consts/useLimit") ??
                                      GetXmlNodeText(doc, "//idinfo/useconst") ?? "";

                // Build a display snippet
                var snippetParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(item.Description))
                {
                    var desc = item.Description.Length > 120
                        ? item.Description.Substring(0, 120) + "…"
                        : item.Description;
                    snippetParts.Add(desc);
                }
                if (!string.IsNullOrWhiteSpace(item.Tags))
                {
                    snippetParts.Add($"Tags: {item.Tags}");
                }

                item.MetadataSnippet = snippetParts.Count > 0
                    ? string.Join(" | ", snippetParts)
                    : item.MetadataSnippet ?? "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing metadata XML: {ex.Message}");
            }
        }

        private string GetXmlNodeText(System.Xml.XmlDocument doc, string xpath)
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

        /// <summary>
        /// Check if a dataset item matches the search term
        /// </summary>
        private bool MatchesSearch(SdeDatasetItem item, string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                return true;

            var terms = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // All terms must match (AND logic)
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

                if (!termMatched)
                    return false;
            }

            return true;
        }

        #endregion

        #region Item Details

        /// <summary>
        /// Load detailed information for a dataset (fields, full metadata)
        /// </summary>
        internal async Task LoadItemDetails(SdeDatasetItem item)
        {
            if (item == null) return;

            SelectedResult = item;

            await QueuedTask.Run(() =>
            {
                try
                {
                    var fields = new List<FieldInfo>();
                    string metadataDisplay = "";

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
                            // Load fields
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

                                // Check for domain
                                var domain = field.GetDomain();
                                if (domain != null)
                                {
                                    fi.DomainName = domain.GetName();
                                    if (domain is CodedValueDomain cvd)
                                    {
                                        fi.DomainType = "Coded Value";
                                        fi.DomainInfo = string.Join(", ",
                                            cvd.GetCodedValuePairs()
                                                .Take(5)
                                                .Select(kv => $"{kv.Key}={kv.Value}"));
                                        if (cvd.GetCodedValuePairs().Count > 5)
                                            fi.DomainInfo += $" (+{cvd.GetCodedValuePairs().Count - 5} more)";
                                    }
                                    else if (domain is RangeDomain rd)
                                    {
                                        fi.DomainType = "Range";
                                        fi.DomainInfo = $"{rd.GetMinValue()} – {rd.GetMaxValue()}";
                                    }
                                }

                                // Default value
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

                        // Build full metadata display
                        metadataDisplay = BuildMetadataDisplay(item);
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        DetailFields.Clear();
                        foreach (var f in fields)
                            DetailFields.Add(f);

                        DetailMetadata = metadataDisplay;
                        ShowDetails = true;
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading details: {ex.Message}");
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = $"Error loading details: {ex.Message}";
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

            return string.Join("\n", parts);
        }

        #endregion

        #region Map Operations

        /// <summary>
        /// Add the selected dataset to the active map
        /// </summary>
        private async Task AddSelectedToMap()
        {
            if (SelectedResult == null || !SelectedResult.CanAddToMap)
                return;

            var item = SelectedResult;
            StatusText = $"Adding {item.SimpleName} to map...";

            await QueuedTask.Run(() =>
            {
                try
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusText = "No active map view. Please open a map first.";
                        });
                        return;
                    }

                    // Build the full data path
                    Uri gdbUri = new Uri(item.ConnectionPath);

                    if (item.DatasetType == "Feature Class")
                    {
                        var fcUri = new Uri(item.ConnectionPath + "\\" + item.Name);
                        LayerFactory.Instance.CreateLayer(fcUri, map);
                    }
                    else if (item.DatasetType == "Table")
                    {
                        var tableUri = new Uri(item.ConnectionPath + "\\" + item.Name);
                        StandaloneTableFactory.Instance.CreateStandaloneTable(tableUri, map);
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = $"✓ Added {item.SimpleName} to map";
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error adding to map: {ex.Message}");
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = $"Error adding to map: {ex.Message}";
                    });
                }
            });
        }

        private void CopySelectedPath()
        {
            if (SelectedResult == null) return;

            try
            {
                var fullPath = $"{SelectedResult.ConnectionPath}\\{SelectedResult.Name}";
                System.Windows.Clipboard.SetText(fullPath);
                StatusText = $"Copied path to clipboard";
            }
            catch (Exception ex)
            {
                StatusText = $"Error copying: {ex.Message}";
            }
        }

        #endregion

        #region Helpers

        private string GetSimpleName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return fullName;
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

        private void ClearSearch()
        {
            SearchText = "";
            SearchResults.Clear();
            ResultCount = 0;
            ShowDetails = false;
            StatusText = "Search cleared";
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
            if (pane == null)
                return;
            pane.Activate();
        }

        #endregion
    }

    #region Data Models

    /// <summary>
    /// Represents an enterprise geodatabase connection
    /// </summary>
    public class SdeConnectionItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string ConnectionType { get; set; }

        public override string ToString() => Name;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Represents a dataset found in the SDE geodatabase
    /// </summary>
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

        // Metadata
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

        /// <summary>
        /// Subtitle line for the result list — shows type + key info
        /// </summary>
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

    /// <summary>
    /// Represents a field in a dataset (for detail view)
    /// </summary>
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

        /// <summary>
        /// Summary line for the field list
        /// </summary>
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
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
