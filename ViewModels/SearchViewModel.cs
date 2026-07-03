using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using MtgCommanderBuilder.Models;
using MtgCommanderBuilder.Services;

namespace MtgCommanderBuilder.ViewModels
{
    public class SearchViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;
        private readonly DeckViewModel _deckViewModel;

        private readonly Dictionary<string, int> _edhrecIdRank = new();
        private readonly Dictionary<string, int> _edhrecNameRank = new();
        private string? _currentEdhrecCommanderId = null;

        public DeckViewModel DeckViewModel => _deckViewModel;
        
        private string _searchText = string.Empty;
        private string _selectedType = "All";
        private string _oracleSearch = string.Empty;
        private string _sortOrder = "EDHREC Rank";
        private bool _limitToCommanderColors = true;
        private bool _searchAffectsStaples = false;
        private readonly System.Windows.Threading.DispatcherTimer _typeDebounceTimer;
        private readonly System.Windows.Threading.DispatcherTimer _settingsSaveDebounceTimer;

        public bool SearchAffectsStaples
        {
            get => _searchAffectsStaples;
            set
            {
                if (SetProperty(ref _searchAffectsStaples, value))
                {
                    RefreshStaples();
                    SaveSettings();
                }
            }
        }

        private void OnSearchCriteriaChanged()
        {
            ExecuteSearch();
            if (SearchAffectsStaples)
            {
                RefreshStaples();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    // Restart the debouncer timer (wait 0.5s of inactivity before executing search)
                    _typeDebounceTimer.Stop();
                    _typeDebounceTimer.Start();
                }
            }
        }

        public string SelectedType
        {
            get => _selectedType;
            set
            {
                if (SetProperty(ref _selectedType, value))
                {
                    OnSearchCriteriaChanged();
                }
            }
        }

        public string OracleSearch
        {
            get => _oracleSearch;
            set
            {
                if (SetProperty(ref _oracleSearch, value))
                {
                    _typeDebounceTimer.Stop();
                    _typeDebounceTimer.Start();
                }
            }
        }

        public string SortOrder
        {
            get => _sortOrder;
            set
            {
                if (SetProperty(ref _sortOrder, value))
                {
                    _deckViewModel.CurrentSortOrder = value;
                    ExecuteSearch();
                    RefreshStaples();
                }
            }
        }

        public bool LimitToCommanderColors
        {
            get => _limitToCommanderColors;
            set
            {
                if (SetProperty(ref _limitToCommanderColors, value))
                {
                    if (_deckViewModel.EnforceColorIdentity != value)
                    {
                        _deckViewModel.EnforceColorIdentity = value;
                    }
                    ExecuteSearch();
                    RefreshStaples();
                }
            }
        }

        public BulkObservableCollection<Card> SearchResults { get; } = new();
        public BulkObservableCollection<Card> Staples { get; } = new();

        public List<string> CardTypes { get; } = new()
        {
            "All", "Creature", "Instant", "Sorcery", "Artifact", "Enchantment", "Land", "Planeswalker"
        };

        public List<string> SortOrders { get; } = new()
        {
            "EDHREC Rank", "Alphabetical", "Mana Value (Low-High)", "Mana Value (High-Low)", "Card Type",
            "Color (WUBRG)", "Rarity", "Price (Low-High)", "Price (High-Low)", "Set / Release Date", "Collector Number"
        };

        public RelayCommand<Card> AddToDeckCommand { get; }
        public RelayCommand<Card> SetAsCommanderCommand { get; }

        private readonly DeckStorageService _storageService;
        private List<CustomStaplesTab> _customTabs = new();
        private string? _lastCommanderId;

        private static readonly HashSet<string> CuratedStaples = new(StringComparer.OrdinalIgnoreCase)
        {
            // White
            "Swords to Plowshares", "Path to Exile", "Teferi's Protection", "Esper Sentinel", 
            "Smothering Tithe", "Farewell", "Land Tax", "Enlightened Tutor", 
            "Grand Abolisher", "Silence", "Drannith Magistrate", "Austere Command", "Generous Gift",
            
            // Blue
            "Cyclonic Rift", "Rhystic Study", "Mystic Remora", "Fierce Guardianship", 
            "Mana Drain", "Force of Will", "Force of Negation", "Swan Song", 
            "Brainstorm", "Mystical Tutor", "Ponder", "Preordain", 
            "Phyrexian Metamorph", "Pongify", "Rapid Hybridization", "Counterspell",
            
            // Black
            "Demonic Tutor", "Vampiric Tutor", "Toxic Deluge", "Reanimate", 
            "Dark Ritual", "Necropotence", "Deadly Rollick", "Opposition Agent", 
            "Dauthi Voidwalker", "Imperial Seal", "Diabolic Intent", "Snuff Out", 
            "Feed the Swarm", "Cabal Coffers",
            
            // Red
            "Dockside Extortionist", "Jeska's Will", "Deflecting Swat", "Blasphemous Act", 
            "Vandalblast", "Chaos Warp", "Underworld Breach", "Wheel of Fortune", 
            "Ragavan, Nimble Pilferer", "Red Elemental Blast", "Pyroblast", "Faithless Looting", 
            "Bolt Bend", "Gamble",
            
            // Green
            "Birds of Paradise", "Sylvan Library", "Veil of Summer", "Worldly Tutor", 
            "Green Sun's Zenith", "Finale of Devastation", "Chord of Calling", "Cultivate", 
            "Kodama's Reach", "Nature's Lore", "Three Visits", "Farseek", 
            "Beast Within", "Eternal Witness", "Heroic Intervention", "Craterhoof Behemoth", 
            "Delighted Halfling",
            
            // Colorless / Artifacts
            "Sol Ring", "Arcane Signet", "Mana Crypt", "Chrome Mox", 
            "Mox Diamond", "Mana Vault", "Fellwar Stone", "Sensei's Divining Top", 
            "The One Ring", "Lightning Greaves", "Swiftfoot Boots", "Skullclamp", 
            "Crucible of Worlds", "Jeweled Lotus",
            
            // Lands
            "Command Tower", "Exotic Orchard", "Ancient Tomb", "Boseiju, Who Endures", 
            "Otawara, Soaring City", "Urborg, Tomb of Yawgmoth", "Yavimaya, Cradle of Growth", 
            "Reliquary Tower", "Plaza of Heroes", "City of Brass", "Mana Confluence", 
            "Homeward Path", "Strip Mine", "Prismatic Vista"
        };

        private int GetColorOrder(Card card)
        {
            if (card.IsLand) return 7;
            if (card.Colors == null || card.Colors.Count == 0) return 6; // Colorless non-land
            if (card.Colors.Count > 1) return 5; // Multi
            
            string primaryColor = card.Colors[0];
            switch (primaryColor)
            {
                case "W": return 0;
                case "U": return 1;
                case "B": return 2;
                case "R": return 3;
                case "G": return 4;
                default: return 6;
            }
        }

        public List<CustomStaplesTab> CustomStaplesTabs => _customTabs;
        public ObservableCollection<string> StaplesCategories { get; } = new();

        private string _selectedStaplesCategory = "General Staples";
        public string SelectedStaplesCategory
        {
            get => _selectedStaplesCategory;
            set
            {
                if (SetProperty(ref _selectedStaplesCategory, value))
                {
                    OnPropertyChanged(nameof(IsCustomTabSelected));
                    RefreshStaples();
                }
            }
        }

        public bool IsCustomTabSelected => SelectedStaplesCategory != "General Staples";

        private double _cardGridScale = 1.0;
        public double CardGridScale
        {
            get => _cardGridScale;
            set
            {
                if (SetProperty(ref _cardGridScale, value))
                {
                    _settingsSaveDebounceTimer.Stop();
                    _settingsSaveDebounceTimer.Start();
                }
            }
        }

        public SearchViewModel(DatabaseService dbService, DeckViewModel deckViewModel, DeckStorageService storageService)
        {
            _dbService = dbService;
            _deckViewModel = deckViewModel;
            _storageService = storageService;

            // Initialize typing debouncer (wait 1s of inactivity before firing search)
            _typeDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _typeDebounceTimer.Tick += (s, e) =>
            {
                _typeDebounceTimer.Stop();
                OnSearchCriteriaChanged();
            };

            // Initialize settings save debouncer (wait 500ms after slider dragging before writing to disk)
            _settingsSaveDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _settingsSaveDebounceTimer.Tick += (s, e) =>
            {
                _settingsSaveDebounceTimer.Stop();
                SaveSettings();
            };

            // Load custom tabs and scale from settings
            var settings = _storageService.LoadSettings();
            _customTabs = settings.CustomStaplesTabs ?? new List<CustomStaplesTab>();
            _cardGridScale = settings.CardGridScale > 0 ? settings.CardGridScale : 1.0;
            _searchAffectsStaples = settings.SearchAffectsStaples;

            // Populate categories list
            StaplesCategories.Add("General Staples");
            foreach (var tab in _customTabs)
            {
                StaplesCategories.Add(tab.Name);
            }

            AddToDeckCommand = new RelayCommand<Card>(c => _deckViewModel.AddCard(c, false));
            SetAsCommanderCommand = new RelayCommand<Card>(c => 
            {
                _deckViewModel.AddCard(c, true);
            });

            _lastCommanderId = _deckViewModel.Commander?.Id;

            // Initial background load of EDHRec recommendations
            _ = LoadEdhrecDataAsync(_deckViewModel.Commander);

            // Re-trigger search when commander colors or rules change
            _deckViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DeckViewModel.Commander))
                {
                    var currentComm = _deckViewModel.Commander;
                    if (currentComm?.Id != _lastCommanderId)
                    {
                        _lastCommanderId = currentComm?.Id;
                        _ = LoadEdhrecDataAsync(currentComm);
                        RefreshStaples();
                        ExecuteSearch();
                    }
                }
                else if (e.PropertyName == nameof(DeckViewModel.EnforceCommanderRules))
                {
                    RefreshStaples();
                    ExecuteSearch();
                }
                else if (e.PropertyName == nameof(DeckViewModel.EnforceColorIdentity))
                {
                    if (LimitToCommanderColors != _deckViewModel.EnforceColorIdentity)
                    {
                        LimitToCommanderColors = _deckViewModel.EnforceColorIdentity;
                    }
                    else
                    {
                        RefreshStaples();
                        ExecuteSearch();
                    }
                }
            };
        }

        private IEnumerable<Card> ApplySearchCriteria(IEnumerable<Card> query)
        {
            // 1. Advanced Scryfall Search Syntax parsing
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string text = SearchText.Trim();
                var filterRegex = new System.Text.RegularExpressions.Regex(
                    @"(?:\b(?<key>type|t|oracle|o|color|c|identity|id|cmc|mv|rarity|r|set|s|price|p|usd)(?<op>:>=|:<=|:=|:>|:<|>=|<=|>|<|=|:)\s*(?:""(?<val>[^""]*)""|'(?<val>[^\']*)'|(?<val>[^\s]*)))|(?<name>""[^""]+""|'[^\s']+'|[^\s]+)", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
                
                var matches = filterRegex.Matches(text);
                
                var nameFilters = new List<string>();
                var typeFilters = new List<string>();
                var oracleFilters = new List<string>();
                var colorFilters = new List<(string Op, string Val)>();
                var identityFilters = new List<(string Op, string Val)>();
                var cmcFilters = new List<(string Op, double Val)>();
                var rarityFilters = new List<string>();
                var setFilters = new List<string>();
                var priceFilters = new List<(string Op, double Val)>();
                
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups["key"].Success)
                    {
                        string key = match.Groups["key"].Value.ToLowerInvariant();
                        string op = match.Groups["op"].Value;
                        string val = match.Groups["val"].Value;
                        
                        if (string.IsNullOrWhiteSpace(val))
                            continue;
                        
                        // Normalize operator (e.g. ":>" -> ">", ":>=" -> ">=", ":" -> ":")
                        if (op.StartsWith(":") && op.Length > 1)
                        {
                            op = op.Substring(1);
                        }
                        
                        switch (key)
                        {
                            case "t":
                            case "type":
                                typeFilters.Add(val);
                                break;
                            case "o":
                            case "oracle":
                                oracleFilters.Add(val);
                                break;
                            case "c":
                            case "color":
                                colorFilters.Add((op, val));
                                break;
                            case "id":
                            case "identity":
                                identityFilters.Add((op, val));
                                break;
                            case "cmc":
                            case "mv":
                                if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double cmcVal))
                                    cmcFilters.Add((op, cmcVal));
                                break;
                            case "r":
                            case "rarity":
                                rarityFilters.Add(val);
                                break;
                            case "s":
                            case "set":
                                setFilters.Add(val);
                                break;
                            case "p":
                            case "price":
                            case "usd":
                                if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double priceVal))
                                    priceFilters.Add((op, priceVal));
                                break;
                        }
                    }
                    else if (match.Groups["name"].Success)
                    {
                        string nameVal = match.Groups["name"].Value;
                        if (!string.IsNullOrWhiteSpace(nameVal))
                        {
                            nameFilters.Add(nameVal);
                        }
                    }
                }

                // Apply Name Filters
                foreach (var nameTerm in nameFilters)
                {
                    query = query.Where(card => card.Name.Contains(nameTerm, StringComparison.OrdinalIgnoreCase));
                }

                // Apply parsed Type Filters
                foreach (var typeTerm in typeFilters)
                {
                    query = query.Where(card => card.TypeLine.Contains(typeTerm, StringComparison.OrdinalIgnoreCase));
                }

                // Apply parsed Oracle Filters
                foreach (var oracleTerm in oracleFilters)
                {
                    query = query.Where(card => card.OracleText != null && card.OracleText.Contains(oracleTerm, StringComparison.OrdinalIgnoreCase));
                }

                // Apply parsed Color Filters
                foreach (var cf in colorFilters)
                {
                    string val = cf.Val.ToUpperInvariant();
                    if (val == "C" || val == "COLORLESS")
                    {
                        query = query.Where(card => card.Colors.Count == 0);
                    }
                    else if (val == "M" || val == "MULTICOLOR")
                    {
                        query = query.Where(card => card.Colors.Count > 1);
                    }
                    else
                    {
                        var filterColors = val.Where(ch => "WUBRG".Contains(ch)).Select(ch => ch.ToString()).ToList();
                        if (filterColors.Any())
                        {
                            if (cf.Op == "=" || cf.Op == ":")
                            {
                                query = query.Where(card => card.Colors.Count == filterColors.Count && filterColors.All(c => card.Colors.Contains(c)));
                            }
                            else if (cf.Op == "<=")
                            {
                                query = query.Where(card => card.Colors.All(c => filterColors.Contains(c)));
                            }
                            else if (cf.Op == ">=")
                            {
                                query = query.Where(card => filterColors.All(c => card.Colors.Contains(c)));
                            }
                            else if (cf.Op == ">")
                            {
                                query = query.Where(card => card.Colors.Count > filterColors.Count && filterColors.All(c => card.Colors.Contains(c)));
                            }
                            else if (cf.Op == "<")
                            {
                                query = query.Where(card => card.Colors.Count < filterColors.Count && card.Colors.All(c => filterColors.Contains(c)));
                            }
                        }
                    }
                }

                // Apply parsed Color Identity Filters
                foreach (var idf in identityFilters)
                {
                    string val = idf.Val.ToUpperInvariant();
                    if (val == "C" || val == "COLORLESS")
                    {
                        query = query.Where(card => card.ColorIdentity.Count == 0);
                    }
                    else
                    {
                        var filterIds = val.Where(ch => "WUBRG".Contains(ch)).Select(ch => ch.ToString()).ToList();
                        if (filterIds.Any())
                        {
                            if (idf.Op == "=" || idf.Op == ":")
                            {
                                query = query.Where(card => card.ColorIdentity.Count == filterIds.Count && filterIds.All(c => card.ColorIdentity.Contains(c)));
                            }
                            else if (idf.Op == "<=")
                            {
                                query = query.Where(card => card.ColorIdentity.All(c => filterIds.Contains(c)));
                            }
                            else if (idf.Op == ">=")
                            {
                                query = query.Where(card => filterIds.All(c => card.ColorIdentity.Contains(c)));
                            }
                            else if (idf.Op == ">")
                            {
                                query = query.Where(card => card.ColorIdentity.Count > filterIds.Count && filterIds.All(c => card.ColorIdentity.Contains(c)));
                            }
                            else if (idf.Op == "<")
                            {
                                query = query.Where(card => card.ColorIdentity.Count < filterIds.Count && card.ColorIdentity.All(c => filterIds.Contains(c)));
                            }
                        }
                    }
                }

                // Apply parsed CMC Filters
                foreach (var cmcf in cmcFilters)
                {
                    double target = cmcf.Val;
                    if (cmcf.Op == "=" || cmcf.Op == ":")
                        query = query.Where(card => Math.Abs(card.Cmc - target) < 0.01);
                    else if (cmcf.Op == ">=")
                        query = query.Where(card => card.Cmc >= target);
                    else if (cmcf.Op == "<=")
                        query = query.Where(card => card.Cmc <= target);
                    else if (cmcf.Op == ">")
                        query = query.Where(card => card.Cmc > target);
                    else if (cmcf.Op == "<")
                        query = query.Where(card => card.Cmc < target);
                }

                // Apply parsed Rarity Filters
                foreach (var rarityTerm in rarityFilters)
                {
                    query = query.Where(card => card.Rarity.Contains(rarityTerm, StringComparison.OrdinalIgnoreCase));
                }

                // Apply parsed Set Filters
                foreach (var setTerm in setFilters)
                {
                    query = query.Where(card => card.Set.Equals(setTerm, StringComparison.OrdinalIgnoreCase) || card.SetName.Contains(setTerm, StringComparison.OrdinalIgnoreCase));
                }

                // Apply parsed Price Filters
                foreach (var pf in priceFilters)
                {
                    double target = pf.Val;
                    query = query.Where(card => {
                        if (string.IsNullOrEmpty(card.PriceUsd) || !double.TryParse(card.PriceUsd, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double priceVal))
                            return false;
                        
                        if (pf.Op == "=")
                            return Math.Abs(priceVal - target) < 0.01;
                        // For continuous values like price, treat ':' as '<=' (e.g. price:5 returns cards $5.00 or less)
                        else if (pf.Op == ":" || pf.Op == "<=")
                            return priceVal <= target;
                        else if (pf.Op == ">=")
                            return priceVal >= target;
                        else if (pf.Op == ">")
                            return priceVal > target;
                        else if (pf.Op == "<")
                            return priceVal < target;
                        
                        return false;
                    });
                }
            }

            // 2. UI Combobox Type filter
            if (SelectedType != "All")
            {
                query = query.Where(card => card.TypeLine.Contains(SelectedType, StringComparison.OrdinalIgnoreCase));
            }

            // 3. UI Textbox Oracle search
            if (!string.IsNullOrWhiteSpace(OracleSearch))
            {
                query = query.Where(card => card.OracleText != null && 
                                            card.OracleText.Contains(OracleSearch, StringComparison.OrdinalIgnoreCase));
            }

            return query;
        }

        private IEnumerable<Card> ApplySort(IEnumerable<Card> query)
        {
            switch (SortOrder)
            {
                case "Alphabetical":
                    return query.OrderBy(c => c.Name);
                case "Mana Value (Low-High)":
                    return query.OrderBy(c => c.Cmc).ThenBy(c => c.Name);
                case "Mana Value (High-Low)":
                    return query.OrderByDescending(c => c.Cmc).ThenBy(c => c.Name);
                case "Card Type":
                    return query.OrderBy(c => c.PrimaryType).ThenBy(c => c.Name);
                case "Color (WUBRG)":
                    return query.OrderBy(c => c.ColorSortIndex).ThenBy(c => c.ColorSortString).ThenBy(c => c.Name);
                case "Rarity":
                    return query.OrderBy(c => c.RaritySortIndex).ThenBy(c => c.Name);
                case "Price (Low-High)":
                    return query.OrderBy(c => c.PriceUsdValue < 0 ? double.MaxValue : c.PriceUsdValue).ThenBy(c => c.Name);
                case "Price (High-Low)":
                    return query.OrderByDescending(c => c.PriceUsdValue).ThenBy(c => c.Name);
                case "Set / Release Date":
                    return query.OrderBy(c => c.SetName).ThenBy(c => c.Name);
                case "Collector Number":
                    return query.OrderBy(c => c.CollectorNumberValue).ThenBy(c => c.CollectorNumber).ThenBy(c => c.Name);
                case "EDHREC Rank":
                default:
                    if (_edhrecIdRank.Count > 0)
                    {
                        return query.OrderBy(c =>
                        {
                            if (_edhrecIdRank.TryGetValue(c.Id, out int r))
                                return r;
                            if (_edhrecNameRank.TryGetValue(c.Name.ToLowerInvariant(), out r))
                                return r;
                            return (c.EdhrecRank ?? 999999) + 100000;
                        }).ThenBy(c => c.Name);
                    }
                    else
                    {
                        return query.OrderBy(c => c.EdhrecRank ?? int.MaxValue).ThenBy(c => c.Name);
                    }
            }
        }

        public void ExecuteSearch()
        {
            if (!_dbService.IsLoaded)
            {
                SearchResults.Clear();
                return;
            }

            // Start query with all local cards
            IEnumerable<Card> query = _dbService.Cards;

            // 1. Color Identity restriction
            if (LimitToCommanderColors && _deckViewModel.Commander != null)
            {
                var allowedColors = _deckViewModel.Commander.ColorIdentity;
                query = query.Where(card => card.ColorIdentity.All(color => allowedColors.Contains(color)));
            }

            // 2. Apply search text, type, and oracle filters
            query = ApplySearchCriteria(query);

            // 3. Sorting
            query = ApplySort(query);

            // Deduplicate
            query = query.GroupBy(c => c.Name).Select(g => g.First());

            // 4. Render
            SearchResults.ReplaceRange(query.Take(150));
        }

        public void RefreshStaples()
        {
            if (!_dbService.IsLoaded)
            {
                Staples.Clear();
                return;
            }

            if (SelectedStaplesCategory == "General Staples")
            {
                // Dynamic high-fidelity curated staples
                var query = _dbService.Cards.Where(c => CuratedStaples.Contains(c.Name));

                if (_deckViewModel.Commander != null && LimitToCommanderColors)
                {
                    var colors = _deckViewModel.Commander.ColorIdentity;
                    query = query.Where(c => c.ColorIdentity.All(color => colors.Contains(color)));
                }

                if (SearchAffectsStaples)
                {
                    query = ApplySearchCriteria(query);
                }

                var list = ApplySort(query.GroupBy(c => c.Name).Select(g => g.First())).ToList();

                Staples.ReplaceRange(list);
            }
            else
            {
                // Curated category listing
                var tab = _customTabs.FirstOrDefault(t => t.Name.Equals(SelectedStaplesCategory, StringComparison.OrdinalIgnoreCase));
                if (tab != null)
                {
                    var query = _dbService.Cards.Where(c => tab.CardNames.Contains(c.Name));

                    if (SearchAffectsStaples)
                    {
                        query = ApplySearchCriteria(query);
                    }

                    var list = ApplySort(query.GroupBy(c => c.Name).Select(g => g.First())).ToList();

                    Staples.ReplaceRange(list);
                }
                else
                {
                    Staples.Clear();
                }
            }
        }

        public void SaveSettings()
        {
            var settings = _storageService.LoadSettings();
            settings.CustomStaplesTabs = _customTabs;
            settings.CardGridScale = CardGridScale;
            settings.SearchAffectsStaples = SearchAffectsStaples;
            _storageService.SaveSettings(settings);
        }

        public void CreateCustomStaplesTab(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();

            if (name.Equals("General Staples", StringComparison.OrdinalIgnoreCase) ||
                _customTabs.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("A category with that name already exists.", "Invalid Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newTab = new CustomStaplesTab { Name = name };
            _customTabs.Add(newTab);
            StaplesCategories.Add(name);
            SelectedStaplesCategory = name; // Auto-select new tab
            SaveSettings();
        }

        public void RenameCurrentStaplesTab(string newName)
        {
            if (!IsCustomTabSelected) return;
            if (string.IsNullOrWhiteSpace(newName)) return;
            newName = newName.Trim();

            if (newName.Equals("General Staples", StringComparison.OrdinalIgnoreCase) ||
                _customTabs.Any(t => t.Name.Equals(newName, StringComparison.OrdinalIgnoreCase) && !t.Name.Equals(SelectedStaplesCategory, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("A category with that name already exists.", "Invalid Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var tab = _customTabs.FirstOrDefault(t => t.Name.Equals(SelectedStaplesCategory, StringComparison.OrdinalIgnoreCase));
            if (tab != null)
            {
                string oldName = tab.Name;
                tab.Name = newName;

                int idx = StaplesCategories.IndexOf(oldName);
                if (idx >= 0)
                {
                    StaplesCategories[idx] = newName;
                }

                SelectedStaplesCategory = newName;
                SaveSettings();
            }
        }

        public void DeleteCurrentStaplesTab()
        {
            if (!IsCustomTabSelected) return;

            var tab = _customTabs.FirstOrDefault(t => t.Name.Equals(SelectedStaplesCategory, StringComparison.OrdinalIgnoreCase));
            if (tab != null)
            {
                var result = MessageBox.Show($"Are you sure you want to delete the custom staples tab '{tab.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    string nameToDelete = tab.Name;
                    _customTabs.Remove(tab);
                    StaplesCategories.Remove(nameToDelete);
                    SelectedStaplesCategory = "General Staples";
                    SaveSettings();
                }
            }
        }

        public void AddCardToCurrentCustomTab(Card card)
        {
            if (!IsCustomTabSelected) return;
            var tab = _customTabs.FirstOrDefault(t => t.Name.Equals(SelectedStaplesCategory, StringComparison.OrdinalIgnoreCase));
            if (tab != null)
            {
                if (tab.CardNames.Contains(card.Name, StringComparer.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"'{card.Name}' is already in this staples tab.", "Card Already Exists", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                tab.CardNames.Add(card.Name);
                SaveSettings();
                RefreshStaples();
            }
        }

        public void RemoveCardFromCurrentCustomTab(Card card)
        {
            if (!IsCustomTabSelected) return;
            var tab = _customTabs.FirstOrDefault(t => t.Name.Equals(SelectedStaplesCategory, StringComparison.OrdinalIgnoreCase));
            if (tab != null)
            {
                if (tab.CardNames.Remove(card.Name))
                {
                    SaveSettings();
                    RefreshStaples();
                }
            }
        }

        public static string GetEdhrecSlug(string cardName)
        {
            if (string.IsNullOrEmpty(cardName)) return string.Empty;

            var cleaned = new System.Text.StringBuilder();
            bool lastWasSpaceOrHyphen = false;
            
            foreach (char c in cardName)
            {
                if (char.IsLetterOrDigit(c))
                {
                    cleaned.Append(char.ToLowerInvariant(c));
                    lastWasSpaceOrHyphen = false;
                }
                else if (c == ' ' || c == '-')
                {
                    if (!lastWasSpaceOrHyphen)
                    {
                        cleaned.Append('-');
                        lastWasSpaceOrHyphen = true;
                    }
                }
            }
            
            string slug = cleaned.ToString().Trim('-');
            
            while (slug.Contains("--"))
            {
                slug = slug.Replace("--", "-");
            }
            
            return slug;
        }

        private async Task LoadEdhrecDataAsync(Card? commander)
        {
            if (commander == null)
            {
                _edhrecIdRank.Clear();
                _edhrecNameRank.Clear();
                _currentEdhrecCommanderId = null;
                
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ExecuteSearch();
                });
                return;
            }

            if (_currentEdhrecCommanderId == commander.Id)
            {
                return;
            }

            _currentEdhrecCommanderId = commander.Id;
            _edhrecIdRank.Clear();
            _edhrecNameRank.Clear();

            string slug = GetEdhrecSlug(commander.Name);
            if (string.IsNullOrEmpty(slug)) return;

            string cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MtgCommanderBuilder", "Data", "EdhrecCache"
            );
            
            try
            {
                Directory.CreateDirectory(cacheDir);
            }
            catch {}

            string cachePath = Path.Combine(cacheDir, $"{slug}.json");
            string jsonContent = string.Empty;

            try
            {
                if (File.Exists(cachePath))
                {
                    jsonContent = await File.ReadAllTextAsync(cachePath);
                }
                else
                {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("MtgCommanderBuilder/1.0 (local desktop deck builder; rsuff)");
                    
                    string url = $"https://json.edhrec.com/pages/commanders/{slug}.json";
                    jsonContent = await client.GetStringAsync(url);
                    
                    try
                    {
                        await File.WriteAllTextAsync(cachePath, jsonContent);
                    }
                    catch {}
                }

                if (!string.IsNullOrEmpty(jsonContent))
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var edhrecData = JsonSerializer.Deserialize<EdhrecJsonDto>(jsonContent, options);
                    
                    var cardlists = edhrecData?.Container?.JsonDict?.CardLists;
                    if (cardlists != null && cardlists.Count > 0)
                    {
                        var allCardViews = new List<EdhrecCardViewDto>();
                        var seenIds = new HashSet<string>();

                        foreach (var list in cardlists)
                        {
                            if (list.CardViews == null) continue;
                            foreach (var cv in list.CardViews)
                            {
                                if (string.IsNullOrEmpty(cv.Name)) continue;
                                string lookupKey = cv.Id ?? cv.Name;
                                if (!seenIds.Contains(lookupKey))
                                {
                                    seenIds.Add(lookupKey);
                                    allCardViews.Add(cv);
                                }
                            }
                        }

                        var sortedCardViews = allCardViews
                            .OrderByDescending(cv => cv.Synergy ?? -999.0)
                            .ThenByDescending(cv => cv.Inclusion ?? cv.NumDecks ?? 0)
                            .ThenBy(cv => cv.Name)
                            .ToList();

                        for (int i = 0; i < sortedCardViews.Count; i++)
                        {
                            var cv = sortedCardViews[i];
                            int rank = i + 1;

                            if (!string.IsNullOrEmpty(cv.Id))
                            {
                                _edhrecIdRank[cv.Id] = rank;
                            }
                            if (!string.IsNullOrEmpty(cv.Name))
                            {
                                _edhrecNameRank[cv.Name.ToLowerInvariant()] = rank;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading EDHRec recommendations for {commander.Name}: {ex.Message}");
            }
            finally
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ExecuteSearch();
                });
            }
        }
    }

    public class EdhrecJsonDto
    {
        [JsonPropertyName("container")]
        public EdhrecContainerDto? Container { get; set; }
    }

    public class EdhrecContainerDto
    {
        [JsonPropertyName("json_dict")]
        public EdhrecJsonDictDto? JsonDict { get; set; }
    }

    public class EdhrecJsonDictDto
    {
        [JsonPropertyName("cardlists")]
        public List<EdhrecCardListDto>? CardLists { get; set; }
    }

    public class EdhrecCardListDto
    {
        [JsonPropertyName("header")]
        public string? Header { get; set; }

        [JsonPropertyName("tag")]
        public string? Tag { get; set; }

        [JsonPropertyName("cardviews")]
        public List<EdhrecCardViewDto>? CardViews { get; set; }
    }

    public class EdhrecCardViewDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("synergy")]
        public double? Synergy { get; set; }

        [JsonPropertyName("inclusion")]
        public int? Inclusion { get; set; }

        [JsonPropertyName("num_decks")]
        public int? NumDecks { get; set; }
    }
}
