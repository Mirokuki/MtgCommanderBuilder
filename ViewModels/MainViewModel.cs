using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using MtgCommanderBuilder.Models;
using MtgCommanderBuilder.Services;

namespace MtgCommanderBuilder.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private string _statusText = "Initializing application...";
        private double _downloadProgress = 0;
        private double _cardZoomScale = 1.0;
        private bool _isDownloading;
        private bool _isDbMissing = true;
        private bool _isDbLoaded;

        // Services
        public DatabaseService DbService { get; }
        public DeckStorageService StorageService { get; }
        public ImageCacheService ImageCache { get; }

        // ViewModels
        public DeckViewModel ActiveDeckViewModel { get; }
        public SearchViewModel CardSearchViewModel { get; }
        public ProxyPrinterViewModel ProxyPrinter { get; }
        public DeckWizardViewModel DeckWizard { get; }

        // Data binding lists
        public ObservableCollection<Deck> SavedDecks { get; } = new();

        private Deck? _selectedDeck;
        public Deck? SelectedDeck
        {
            get => _selectedDeck;
            set
            {
                if (SetProperty(ref _selectedDeck, value) && value != null)
                {
                    SelectDeck(value);
                }
            }
        }



        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set => SetProperty(ref _downloadProgress, value);
        }

        public double CardZoomScale
        {
            get => _cardZoomScale;
            set => SetProperty(ref _cardZoomScale, value);
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                if (SetProperty(ref _isDownloading, value))
                {
                    OnPropertyChanged(nameof(ShowDownloadOverlay));
                }
            }
        }

        public bool IsDbMissing
        {
            get => _isDbMissing;
            set
            {
                if (SetProperty(ref _isDbMissing, value))
                {
                    OnPropertyChanged(nameof(ShowDownloadOverlay));
                }
            }
        }

        public bool IsDbLoaded
        {
            get => _isDbLoaded;
            set => SetProperty(ref _isDbLoaded, value);
        }

        public bool UseFullDatabase
        {
            get => DbService.UseFullDatabase;
            set
            {
                if (DbService.UseFullDatabase != value)
                {
                    DbService.UseFullDatabase = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DownloadDescription));
                    OnPropertyChanged(nameof(DownloadButtonText));
                    
                    // Recheck missing and loaded state
                    IsDbMissing = !DbService.IsDatabaseDownloaded;
                    IsDbLoaded = DbService.IsLoaded;
                    
                    if (IsDbLoaded)
                    {
                        CardSearchViewModel.RefreshStaples();
                        CardSearchViewModel.ExecuteSearch();
                    }
                    else if (!IsDbMissing)
                    {
                        InitializeDatabase();
                    }
                    else
                    {
                        StatusText = UseFullDatabase 
                            ? "Full printings database is missing. Click download to initialize!"
                            : "Local card database is missing. Click download to initialize!";
                    }

                    SaveAppSettings();
                }
            }
        }

        public string DownloadDescription
        {
            get
            {
                return UseFullDatabase
                    ? "To view and search every single card art printing (~1.5GB dataset), the app needs to index Scryfall's full printings database."
                    : "To search all 30,000+ Magic cards locally and 100% offline (~120MB dataset), the app needs to index Scryfall's lightweight Oracle database.";
            }
        }

        public string DownloadButtonText
        {
            get
            {
                return UseFullDatabase
                    ? "DOWNLOAD FULL PRINTINGS DB (~1.5GB)"
                    : "DOWNLOAD ORACLE DB (~120MB)";
            }
        }

        private bool _isGridViewActive;
        public bool IsGridViewActive
        {
            get => _isGridViewActive;
            set
            {
                if (SetProperty(ref _isGridViewActive, value))
                {
                    SaveAppSettings();
                }
            }
        }

        private string _activeView = "DeckBuilder";
        public string ActiveView
        {
            get => _activeView;
            set => SetProperty(ref _activeView, value);
        }

        // New Settings Backing Fields & Properties
        private string _defaultFormat = "Commander (EDH)";
        public string DefaultFormat
        {
            get => _defaultFormat;
            set { if (SetProperty(ref _defaultFormat, value)) SaveAppSettings(); }
        }

        private int _defaultDeckSize = 100;
        public int DefaultDeckSize
        {
            get => _defaultDeckSize;
            set { if (SetProperty(ref _defaultDeckSize, value)) SaveAppSettings(); }
        }

        private int _defaultCommanderTax = 2;
        public int DefaultCommanderTax
        {
            get => _defaultCommanderTax;
            set { if (SetProperty(ref _defaultCommanderTax, value)) SaveAppSettings(); }
        }

        private bool _confirmCardRemovals = true;
        public bool ConfirmCardRemovals
        {
            get => _confirmCardRemovals;
            set { if (SetProperty(ref _confirmCardRemovals, value)) SaveAppSettings(); }
        }

        private bool _warnOffColorCards = true;
        public bool WarnOffColorCards
        {
            get => _warnOffColorCards;
            set { if (SetProperty(ref _warnOffColorCards, value)) SaveAppSettings(); }
        }

        private bool _enableAdvancedWarnings = true;
        public bool EnableAdvancedWarnings
        {
            get => _enableAdvancedWarnings;
            set { if (SetProperty(ref _enableAdvancedWarnings, value)) SaveAppSettings(); }
        }

        private bool _showCardRoles = true;
        public bool ShowCardRoles
        {
            get => _showCardRoles;
            set { if (SetProperty(ref _showCardRoles, value)) SaveAppSettings(); }
        }

        private bool _autoSortDeck = false;
        public bool AutoSortDeck
        {
            get => _autoSortDeck;
            set { if (SetProperty(ref _autoSortDeck, value)) SaveAppSettings(); }
        }

        private string _currency = "USD ($)";
        public string Currency
        {
            get => _currency;
            set { if (SetProperty(ref _currency, value)) SaveAppSettings(); }
        }

        private string _language = "English";
        public string Language
        {
            get => _language;
            set { if (SetProperty(ref _language, value)) SaveAppSettings(); }
        }

        private string _deckListLayoutMode = "Spreadsheet Grid";
        public string DeckListLayoutMode
        {
            get => _deckListLayoutMode;
            set { if (SetProperty(ref _deckListLayoutMode, value)) SaveAppSettings(); }
        }

        public List<string> DeckListLayoutModes { get; } = new() { "Spreadsheet Grid", "Card Spoiler Grid" };

        private void SaveAppSettings()
        {
            StorageService?.SaveSettings(new AppSettings
            {
                UseFullDatabase = UseFullDatabase,
                IsGridViewActive = IsGridViewActive,
                CardGridScale = CardSearchViewModel?.CardGridScale ?? 1.0,
                CustomStaplesTabs = CardSearchViewModel?.CustomStaplesTabs ?? new(),

                DefaultFormat = DefaultFormat,
                DefaultDeckSize = DefaultDeckSize,
                DefaultCommanderTax = DefaultCommanderTax,
                ConfirmCardRemovals = ConfirmCardRemovals,
                WarnOffColorCards = WarnOffColorCards,
                EnableAdvancedWarnings = EnableAdvancedWarnings,
                ShowCardRoles = ShowCardRoles,
                AutoSortDeck = AutoSortDeck,
                Currency = Currency,
                Language = Language,
                DeckListLayoutMode = DeckListLayoutMode
            });
        }



        public bool ShowDownloadOverlay => IsDownloading || IsDbMissing;

        // Commands
        public RelayCommand CreateNewDeckCommand { get; }
        public RelayCommand<Deck> SelectDeckCommand { get; }
        public RelayCommand<Deck> DeleteDeckCommand { get; }
        public RelayCommand DownloadDatabaseCommand { get; }

        public MainViewModel()
        {
            // Initialize Services
            DbService = new DatabaseService();
            StorageService = new DeckStorageService();
            ImageCache = new ImageCacheService();

            // Load saved settings
            var settings = StorageService.LoadSettings();
            DbService.UseFullDatabase = settings.UseFullDatabase;
            _isGridViewActive = settings.IsGridViewActive;

            _defaultFormat = settings.DefaultFormat;
            _defaultDeckSize = settings.DefaultDeckSize;
            _defaultCommanderTax = settings.DefaultCommanderTax;
            _confirmCardRemovals = settings.ConfirmCardRemovals;
            _warnOffColorCards = settings.WarnOffColorCards;
            _enableAdvancedWarnings = settings.EnableAdvancedWarnings;
            _showCardRoles = settings.ShowCardRoles;
            _autoSortDeck = settings.AutoSortDeck;
            _currency = settings.Currency;
            _language = settings.Language;
            _deckListLayoutMode = settings.DeckListLayoutMode ?? "Spreadsheet Grid";

            // Hook database events
            DbService.StatusTextChanged += text => Application.Current?.Dispatcher?.Invoke(() => StatusText = text);
            DbService.DownloadProgressChanged += progress => Application.Current?.Dispatcher?.Invoke(() => DownloadProgress = progress);

            // Initialize child ViewModels
            ActiveDeckViewModel = new DeckViewModel(StorageService);
            ProxyPrinter = new ProxyPrinterViewModel(ActiveDeckViewModel, DbService);
            CardSearchViewModel = new SearchViewModel(DbService, ActiveDeckViewModel, StorageService);
            CardSearchViewModel.CardGridScale = settings.CardGridScale > 0 ? settings.CardGridScale : 1.0;
            DeckWizard = new DeckWizardViewModel(DbService, StorageService, OnWizardDeckGenerated);

            ActiveDeckViewModel.ValidationWarningTriggered += message => 
            {
                Application.Current?.Dispatcher?.Invoke(() => 
                {
                    MessageBox.Show(message, "Commander Restriction", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            };

            ActiveDeckViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ActiveDeckViewModel.Name))
                {
                    var existingDeck = SavedDecks.FirstOrDefault(d => d.Id == ActiveDeckViewModel.ActiveDeck.Id);
                    if (existingDeck != null)
                    {
                        existingDeck.Name = ActiveDeckViewModel.Name;
                    }
                }
            };

            // Define commands
            CreateNewDeckCommand = new RelayCommand(CreateNewDeck);
            SelectDeckCommand = new RelayCommand<Deck>(SelectDeck);
            DeleteDeckCommand = new RelayCommand<Deck>(DeleteDeck);
            DownloadDatabaseCommand = new RelayCommand(async () => await DownloadDatabaseAsync());

            // Load saved decks and check database status
            LoadSavedDecks();
            InitializeDatabase();
        }

        private void OnWizardDeckGenerated(Deck deck)
        {
            StorageService.SaveDeck(deck);
            SavedDecks.Insert(0, deck);
            SelectedDeck = deck;
            ActiveView = "DeckBuilder";
        }

        private void LoadSavedDecks()
        {
            SavedDecks.Clear();
            var decks = StorageService.GetAllDecks();
            
            foreach (var d in decks)
            {
                SavedDecks.Add(d);
            }

            if (SavedDecks.Count > 0)
            {
                SelectedDeck = SavedDecks[0];
            }
            else
            {
                // Create a default deck
                CreateNewDeck();
            }
        }

        private void CreateNewDeck()
        {
            var newDeck = new Deck { Name = "New Commander Deck" };
            StorageService.SaveDeck(newDeck);
            SavedDecks.Insert(0, newDeck);
            SelectedDeck = newDeck;
        }

        private void SelectDeck(Deck deck)
        {
            var fullDeck = StorageService.LoadDeck(deck.Id);
            if (fullDeck != null)
            {
                if (DbService != null && DbService.IsLoaded)
                {
                    RepairDeckCardData(fullDeck);
                }

                ActiveDeckViewModel.ActiveDeck = fullDeck;
                if (_selectedDeck?.Id != deck.Id)
                {
                    _selectedDeck = deck;
                    OnPropertyChanged(nameof(SelectedDeck));
                }
            }
        }

        private void RepairDeckCardData(Deck deck)
        {
            if (deck.Commander != null)
            {
                RepairCardFields(deck.Commander);
            }

            foreach (var card in deck.Cards)
            {
                RepairCardFields(card);
            }
        }

        private void RepairCardFields(Card card)
        {
            if (string.IsNullOrWhiteSpace(card.Set) && DbService != null && DbService.IsLoaded)
            {
                var match = DbService.Cards.FirstOrDefault(c => c.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    card.Set = match.Set;
                    card.SetName = match.SetName;
                    card.CollectorNumber = match.CollectorNumber;
                    if (string.IsNullOrEmpty(card.NormalImageUrl))
                    {
                        card.NormalImageUrl = match.NormalImageUrl;
                    }
                    if (string.IsNullOrEmpty(card.ArtCropImageUrl))
                    {
                        card.ArtCropImageUrl = match.ArtCropImageUrl;
                    }
                    if (string.IsNullOrEmpty(card.PriceUsd))
                    {
                        card.PriceUsd = match.PriceUsd;
                    }
                }
            }
        }

        private void DeleteDeck(Deck deck)
        {
            if (deck == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete the deck '{deck.Name}'?",
                "Confirm Delete Deck",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // Delete unused EDHrec cache before deleting
            DeleteUnusedEdhrecCache(deck);

            StorageService.DeleteDeck(deck.Id);

            var existingDeck = SavedDecks.FirstOrDefault(d => d.Id == deck.Id);
            if (existingDeck != null)
            {
                SavedDecks.Remove(existingDeck);
            }

            if (ActiveDeckViewModel.ActiveDeck != null && ActiveDeckViewModel.ActiveDeck.Id == deck.Id)
            {
                if (SavedDecks.Count > 0)
                {
                    SelectedDeck = SavedDecks[0];
                }
                else
                {
                    CreateNewDeck();
                }
            }
        }

        private void DeleteUnusedEdhrecCache(Deck deck)
        {
            if (deck?.Commander == null || string.IsNullOrWhiteSpace(deck.Commander.Name)) return;

            string commanderName = deck.Commander.Name;

            // Check if any other saved deck (excluding the one we are deleting) uses the same commander
            bool isCommanderUsedElsewhere = SavedDecks.Any(d => d.Id != deck.Id && d.Commander != null && d.Commander.Name == commanderName);

            if (!isCommanderUsedElsewhere)
            {
                string slug = SearchViewModel.GetEdhrecSlug(commanderName);
                if (!string.IsNullOrEmpty(slug))
                {
                    string cacheDir = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                        "MtgCommanderBuilder", "Data", "EdhrecCache"
                    );
                    string cachePath = System.IO.Path.Combine(cacheDir, $"{slug}.json");
                    if (System.IO.File.Exists(cachePath))
                    {
                        try
                        {
                            System.IO.File.Delete(cachePath);
                            System.Diagnostics.Debug.WriteLine($"Deleted unused EDHRec cache file for {commanderName} at {cachePath}");
                        }
                        catch (System.Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error deleting unused EDHRec cache file: {ex.Message}");
                        }
                    }
                }
            }
        }

        private async void InitializeDatabase()
        {
            if (DbService.IsDatabaseDownloaded)
            {
                IsDbMissing = false;
                try
                {
                    await DbService.InitializeDatabaseAsync();
                    IsDbLoaded = true;
                    
                    // Repair the loaded deck's card data now that the master database is initialized
                    if (ActiveDeckViewModel?.ActiveDeck != null)
                    {
                        RepairDeckCardData(ActiveDeckViewModel.ActiveDeck);
                        ActiveDeckViewModel.RefreshDeckState();
                    }

                    // Trigger initial search and staples refresh
                    CardSearchViewModel.RefreshStaples();
                    CardSearchViewModel.ExecuteSearch();
                }
                catch (Exception ex)
                {
                    StatusText = "Error loading database: " + ex.Message;
                }
            }
            else
            {
                IsDbMissing = true;
                StatusText = "Local Card Database is missing. Click download to initialize!";
            }
        }

        private async Task DownloadDatabaseAsync()
        {
            IsDownloading = true;
            IsDbMissing = false;
            try
            {
                await DbService.DownloadDatabaseAsync();
                await DbService.InitializeDatabaseAsync();
                
                IsDbLoaded = true;
                IsDbMissing = false;
                
                // Repair the loaded deck's card data now that the master database is initialized
                if (ActiveDeckViewModel?.ActiveDeck != null)
                {
                    RepairDeckCardData(ActiveDeckViewModel.ActiveDeck);
                    ActiveDeckViewModel.RefreshDeckState();
                }

                // Trigger initial search and staples refresh
                CardSearchViewModel.RefreshStaples();
                CardSearchViewModel.ExecuteSearch();
            }
            catch (Exception ex)
            {
                StatusText = "Database setup failed: " + ex.Message;
                IsDbMissing = true;
            }
            finally
            {
                IsDownloading = false;
            }
        }
    }
}
