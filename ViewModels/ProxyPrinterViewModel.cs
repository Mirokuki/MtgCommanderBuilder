using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using MtgCommanderBuilder.Models;
using MtgCommanderBuilder.Services;

namespace MtgCommanderBuilder.ViewModels
{
    public class ProxyPrinterViewModel : ViewModelBase
    {
        private readonly DeckViewModel _deckViewModel;
        private readonly DatabaseService _dbService;

        private ObservableCollection<PrintableCard> _printableCards = new();
        private ObservableCollection<Card?> _activePageCards = new();
        private ObservableCollection<Card> _curationQueue = new();

        private int _currentPage = 1;
        private int _totalPages = 1;
        private string _paperSize = "Letter"; // "Letter" or "A4"
        private string _layoutMode = "Spaced"; // "Spaced" layout is always active
        private string _guideStyle = "Crop Marks"; // "Dashed Margins", "Pure Black Borders", "Crop Marks", "None"
        private string _guideColor = "Black"; // "Black", "White", "Red", "Gold", "Gray"
        private bool _purifyBorders = false;
        private bool _useBleedEdge = true;

        public List<string> PaperSizes { get; } = new() { "Letter", "A4" };
        public List<string> LayoutModes { get; } = new() { "Spaced", "Cricut (6 Cards)" };
        public List<string> GuideStyles { get; } = new() { "Crop Marks", "Dashed Margins", "Pure Black Borders", "None" };
        public List<string> GuideColors { get; } = new() { "Black", "White", "Red", "Gold", "Gray" };

        // Search properties
        private string _searchText = string.Empty;
        private ObservableCollection<Card> _searchResults = new();
        private bool _isSearchResultsOpen;

        public DeckViewModel ActiveDeck => _deckViewModel;

        public bool UseBleedEdge
        {
            get => _useBleedEdge;
            set
            {
                if (SetProperty(ref _useBleedEdge, value))
                {
                    RaiseCoordinatePropertiesChanged();
                }
            }
        }

        public bool IsCricutMode => LayoutMode == "Cricut (6 Cards)";

        public double PageWidth => PaperSize == "A4" ? (210.0 / 25.4) * 96.0 : 8.5 * 96.0;
        public double PageHeight => PaperSize == "A4" ? (297.0 / 25.4) * 96.0 : 11.0 * 96.0;
        public double CardW => (63.0 / 25.4) * 96.0;
        public double CardH => (88.0 / 25.4) * 96.0;
        public double Bleed => IsCricutMode ? (0.25 / 25.4) * 96.0 : (UseBleedEdge ? (1.0 / 25.4) * 96.0 : 0.0);
        public double Gutter => (IsCricutMode || UseBleedEdge) ? 2.0 * Bleed : 0.0;
        public double GridW => IsCricutMode ? 714.24 : (3 * CardW + 2 * Gutter);
        public double GridH => IsCricutMode ? (3 * CardW + 2 * Gutter) : (3 * CardH + 2 * Gutter);
        public double StartX => (PageWidth - GridW) / 2.0;
        public double StartY => (PageHeight - GridH) / 2.0;

        public double Col0X => IsCricutMode ? 48.64 : StartX;
        public double Col1X => IsCricutMode ? 433.92 : StartX + (CardW + Gutter);
        public double Col2X => IsCricutMode ? 0 : StartX + 2 * (CardW + Gutter);

        public double Row0Y => IsCricutMode ? 169.28 : StartY;
        public double Row1Y => IsCricutMode ? 408.00 : StartY + CardH + Gutter;
        public double Row2Y => IsCricutMode ? 646.40 : StartY + 2 * (CardH + Gutter);

        public double SlotWidth => (IsCricutMode ? CardH : CardW) + 2 * Bleed;
        public double SlotHeight => (IsCricutMode ? CardW : CardH) + 2 * Bleed;

        public double UnrotatedSlotWidth => CardW + 2 * Bleed;
        public double UnrotatedSlotHeight => CardH + 2 * Bleed;

        public double Slot0X => Col0X - Bleed;
        public double Slot1X => Col1X - Bleed;
        public double Slot2X => Col2X - Bleed;

        public double Slot0Y => Row0Y - Bleed;
        public double Slot1Y => Row1Y - Bleed;
        public double Slot2Y => Row2Y - Bleed;

        public void RaiseCoordinatePropertiesChanged()
        {
            OnPropertyChanged(nameof(IsCricutMode));
            OnPropertyChanged(nameof(PageWidth));
            OnPropertyChanged(nameof(PageHeight));
            OnPropertyChanged(nameof(CardW));
            OnPropertyChanged(nameof(CardH));
            OnPropertyChanged(nameof(Bleed));
            OnPropertyChanged(nameof(Gutter));
            OnPropertyChanged(nameof(GridW));
            OnPropertyChanged(nameof(GridH));
            OnPropertyChanged(nameof(StartX));
            OnPropertyChanged(nameof(StartY));
            OnPropertyChanged(nameof(Col0X));
            OnPropertyChanged(nameof(Col1X));
            OnPropertyChanged(nameof(Col2X));
            OnPropertyChanged(nameof(Row0Y));
            OnPropertyChanged(nameof(Row1Y));
            OnPropertyChanged(nameof(Row2Y));
            OnPropertyChanged(nameof(SlotWidth));
            OnPropertyChanged(nameof(SlotHeight));
            OnPropertyChanged(nameof(UnrotatedSlotWidth));
            OnPropertyChanged(nameof(UnrotatedSlotHeight));
            OnPropertyChanged(nameof(Slot0X));
            OnPropertyChanged(nameof(Slot1X));
            OnPropertyChanged(nameof(Slot2X));
            OnPropertyChanged(nameof(Slot0Y));
            OnPropertyChanged(nameof(Slot1Y));
            OnPropertyChanged(nameof(Slot2Y));
        }

        public ObservableCollection<PrintableCard> PrintableCards
        {
            get => _printableCards;
            set => SetProperty(ref _printableCards, value);
        }

        public ObservableCollection<Card?> ActivePageCards
        {
            get => _activePageCards;
            set => SetProperty(ref _activePageCards, value);
        }

        public ObservableCollection<Card> CurationQueue
        {
            get => _curationQueue;
            set => SetProperty(ref _curationQueue, value);
        }

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (value < 1) value = 1;
                if (value > TotalPages) value = TotalPages;
                if (SetProperty(ref _currentPage, value))
                {
                    UpdateActivePageCards();
                    UpdateNavigationProperties();
                }
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            set
            {
                if (SetProperty(ref _totalPages, value))
                {
                    if (CurrentPage > _totalPages)
                    {
                        CurrentPage = _totalPages;
                    }
                    else
                    {
                        UpdateNavigationProperties();
                    }
                }
            }
        }

        public string PaperSize
        {
            get => _paperSize;
            set
            {
                if (SetProperty(ref _paperSize, value))
                {
                    OnPropertyChanged(nameof(PaperDimensionsLabel));
                    RaiseCoordinatePropertiesChanged();
                }
            }
        }

        public string PaperDimensionsLabel => PaperSize == "Letter" ? "Letter (8.5\" x 11\")" : "A4 (210mm x 297mm)";

        // LayoutMode: "Adjacent", "Spaced", or "Cricut (6 Cards)"
        public string LayoutMode
        {
            get => _layoutMode;
            set
            {
                if (SetProperty(ref _layoutMode, value))
                {
                    RecalculatePages();
                    RaiseCoordinatePropertiesChanged();
                }
            }
        }

        // GuideStyle: "Dashed Margins", "Pure Black Borders", "Crop Marks", "None"
        public string GuideStyle
        {
            get => _guideStyle;
            set => SetProperty(ref _guideStyle, value);
        }

        // GuideColor: "Black", "White", "Red", "Gold", "Gray"
        public string GuideColor
        {
            get => _guideColor;
            set
            {
                if (SetProperty(ref _guideColor, value))
                {
                    OnPropertyChanged(nameof(GuideColorBrush));
                }
            }
        }

        public System.Windows.Media.Brush GuideColorBrush
        {
            get
            {
                return GuideColor switch
                {
                    "White" => System.Windows.Media.Brushes.White,
                    "Red" => System.Windows.Media.Brushes.Red,
                    "Gold" => System.Windows.Application.Current?.Resources["AccentGoldBrush"] as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Gold,
                    "Gray" => System.Windows.Media.Brushes.Gray,
                    "Black" => System.Windows.Media.Brushes.Black,
                    _ => System.Windows.Media.Brushes.Black
                };
            }
        }


        public bool PurifyBorders
        {
            get => _purifyBorders;
            set => SetProperty(ref _purifyBorders, value);
        }

        // Search Direct Properties
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ExecuteProxySearch();
                }
            }
        }

        public ObservableCollection<Card> SearchResults
        {
            get => _searchResults;
            set => SetProperty(ref _searchResults, value);
        }

        public bool IsSearchResultsOpen
        {
            get => _isSearchResultsOpen;
            set => SetProperty(ref _isSearchResultsOpen, value);
        }

        // Navigation state
        private bool _canGoNext;
        public bool CanGoNext
        {
            get => _canGoNext;
            private set => SetProperty(ref _canGoNext, value);
        }

        private bool _canGoPrev;
        public bool CanGoPrev
        {
            get => _canGoPrev;
            private set => SetProperty(ref _canGoPrev, value);
        }

        private string _pageLabel = "Page 1 of 1";
        public string PageLabel
        {
            get => _pageLabel;
            private set => SetProperty(ref _pageLabel, value);
        }

        // Commands
        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand ToggleAllCommand { get; }

        public ProxyPrinterViewModel(DeckViewModel deckViewModel, DatabaseService dbService)
        {
            _deckViewModel = deckViewModel;
            _dbService = dbService;

            // Initialize ActivePageCards with 9 null slots
            for (int i = 0; i < 9; i++)
            {
                _activePageCards.Add(null);
            }

            NextPageCommand = new RelayCommand(GoToNextPage, () => CanGoNext);
            PrevPageCommand = new RelayCommand(GoToPrevPage, () => CanGoPrev);
            ToggleAllCommand = new RelayCommand(ToggleAllCards);

            // Listen to active deck changes to reload the curation queue dynamically
            _deckViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DeckViewModel.ActiveDeck))
                {
                    LoadActiveDeck();
                }
            };
            _deckViewModel.Cards.CollectionChanged += (s, e) =>
            {
                LoadActiveDeck();
            };

            // Initial load from deck
            LoadActiveDeck();
        }

        private bool _isSyncing = false;
        public void LoadActiveDeck()
        {
            if (_isSyncing) return;
            _isSyncing = true;
            try
            {
                CurationQueue.Clear();
                foreach (var pc in PrintableCards)
                {
                    pc.PropertyChanged -= OnPrintableCardPropertyChanged;
                }
                PrintableCards.Clear();

                var deckCards = _deckViewModel.Cards.ToList();

                foreach (var card in deckCards)
                {
                    var newPC = new PrintableCard(card);
                    newPC.PropertyChanged += OnPrintableCardPropertyChanged;
                    PrintableCards.Add(newPC);

                    for (int i = 0; i < newPC.PrintQuantity; i++)
                    {
                        CurationQueue.Add(newPC.Card);
                    }
                }

                RecalculatePages();
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void OnPrintableCardPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PrintableCard.IsSelected) || e.PropertyName == nameof(PrintableCard.PrintQuantity))
            {
                if (sender is PrintableCard pc)
                {
                    SyncCardCurationQuantity(pc);
                }
            }
        }

        public void SyncCardCurationQuantity(PrintableCard pc)
        {
            int targetQty = pc.IsSelected ? pc.PrintQuantity : 0;
            int currentQty = CurationQueue.Count(c => c.Id == pc.Card.Id);

            if (currentQty < targetQty)
            {
                for (int i = 0; i < targetQty - currentQty; i++)
                {
                    CurationQueue.Add(pc.Card);
                }
                RecalculatePages();
            }
            else if (currentQty > targetQty)
            {
                for (int i = 0; i < currentQty - targetQty; i++)
                {
                    for (int j = CurationQueue.Count - 1; j >= 0; j--)
                    {
                        if (CurationQueue[j].Id == pc.Card.Id)
                        {
                            CurationQueue.RemoveAt(j);
                            break;
                        }
                    }
                }
                RecalculatePages();
            }
        }

        private void ToggleAllCards()
        {
            bool anyUnselected = _printableCards.Any(pc => !pc.IsSelected);
            foreach (var pc in _printableCards)
            {
                pc.IsSelected = anyUnselected;
            }
            // SyncCardCurationQuantity is triggered automatically per modified card!
        }

        public void ClearCuration()
        {
            CurationQueue.Clear();
            PrintableCards.Clear();
            RecalculatePages();
        }

        public List<Card> GetSelectedCopies()
        {
            return CurationQueue.ToList();
        }

        private void ExecuteProxySearch()
        {
            if (string.IsNullOrWhiteSpace(SearchText) || SearchText.Length < 2)
            {
                SearchResults.Clear();
                IsSearchResultsOpen = false;
                return;
            }

            if (_dbService == null || !_dbService.IsLoaded)
            {
                return;
            }

            var query = SearchText.Trim();
            var matches = _dbService.Cards
                .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(c => c.Name)
                .Take(15)
                .ToList();

            SearchResults.Clear();
            foreach (var m in matches)
            {
                SearchResults.Add(m);
            }

            IsSearchResultsOpen = SearchResults.Count > 0;
        }

        public void AddCardToPrintList(Card card)
        {
            SearchText = string.Empty;
            SearchResults.Clear();
            IsSearchResultsOpen = false;

            var existing = PrintableCards.FirstOrDefault(pc => pc.Card.Id == card.Id);
            if (existing != null)
            {
                existing.PrintQuantity++;
                existing.IsSelected = true;
                // SyncCardCurationQuantity gets fired automatically via property change
            }
            else
            {
                var newPC = new PrintableCard(card);
                newPC.PrintQuantity = 1;
                newPC.IsSelected = true;
                newPC.PropertyChanged += OnPrintableCardPropertyChanged;
                PrintableCards.Add(newPC);

                CurationQueue.Add(card);
                RecalculatePages();
            }
        }

        public void RecalculatePages()
        {
            int totalCards = CurationQueue.Count;
            int cardsPerPage = IsCricutMode ? 6 : 9;
            TotalPages = Math.Max(1, (totalCards + cardsPerPage - 1) / cardsPerPage);

            if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }
            else
            {
                UpdateActivePageCards();
                UpdateNavigationProperties();
            }
        }

        private void UpdateActivePageCards()
        {
            int cardsPerPage = IsCricutMode ? 6 : 9;
            int startIndex = (CurrentPage - 1) * cardsPerPage;

            if (IsCricutMode)
            {
                // Map 6 cards to slots 0, 1, 3, 4, 6, 7 and set 2, 5, 8 to null
                int[] activeSlotIndices = { 0, 1, 3, 4, 6, 7 };
                var activeSlots = new HashSet<int>(activeSlotIndices);

                int cardOffset = 0;
                for (int i = 0; i < 9; i++)
                {
                    if (activeSlots.Contains(i))
                    {
                        int cardIndex = startIndex + cardOffset;
                        if (cardIndex < CurationQueue.Count)
                        {
                            ActivePageCards[i] = CurationQueue[cardIndex];
                        }
                        else
                        {
                            ActivePageCards[i] = null;
                        }
                        cardOffset++;
                    }
                    else
                    {
                        ActivePageCards[i] = null;
                    }
                }
            }
            else
            {
                for (int i = 0; i < 9; i++)
                {
                    int cardIndex = startIndex + i;
                    if (cardIndex < CurationQueue.Count)
                    {
                        ActivePageCards[i] = CurationQueue[cardIndex];
                    }
                    else
                    {
                        ActivePageCards[i] = null;
                    }
                }
            }
            OnPropertyChanged(nameof(ActivePageCards));
        }

        private void UpdateNavigationProperties()
        {
            CanGoNext = CurrentPage < TotalPages;
            CanGoPrev = CurrentPage > 1;
            
            PageLabel = $"Page {CurrentPage} of {TotalPages} ({CurationQueue.Count} cards curated)";

            CommandManager.InvalidateRequerySuggested();
        }

        private void GoToNextPage()
        {
            if (CanGoNext)
            {
                CurrentPage++;
            }
        }

        private void GoToPrevPage()
        {
            if (CanGoPrev)
            {
                CurrentPage--;
            }
        }
    }
}
