using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using MtgCommanderBuilder.Models;
using MtgCommanderBuilder.Services;

namespace MtgCommanderBuilder.ViewModels
{
    public class DeckViewModel : ViewModelBase
    {
        private Deck _deck;
        private readonly DeckStorageService _storageService;

        public Deck ActiveDeck
        {
            get => _deck;
            set
            {
                if (SetProperty(ref _deck, value))
                {
                    if (_deck != null)
                    {
                        bool anyModified = false;
                        if (_deck.Commander != null)
                        {
                            if (_deck.Commander.Categories == null || _deck.Commander.Categories.Count == 0)
                            {
                                _deck.Commander.Categories = AutoCategorize(_deck.Commander);
                                anyModified = true;
                            }
                            else
                            {
                                // Cleanup legacy false positive 'Protection' tags for cards with anti-regeneration clauses
                                string text = _deck.Commander.OracleText?.ToLowerInvariant() ?? string.Empty;
                                if ((text.Contains("can't be regenerated") || 
                                     text.Contains("cannot be regenerated") || 
                                     text.Contains("can't regenerate") || 
                                     text.Contains("cannot regenerate") ||
                                     text.Contains("without regenerating")) && 
                                    _deck.Commander.Categories.Contains("Protection"))
                                {
                                    _deck.Commander.Categories.Remove("Protection");
                                    anyModified = true;
                                }
                            }
                        }
                        if (_deck.Cards != null)
                        {
                            foreach (var card in _deck.Cards)
                            {
                                if (card.Categories == null || card.Categories.Count == 0)
                                {
                                    card.Categories = AutoCategorize(card);
                                    anyModified = true;
                                }
                                else
                                {
                                    // Cleanup legacy false positive 'Protection' tags for cards with anti-regeneration clauses
                                    string text = card.OracleText?.ToLowerInvariant() ?? string.Empty;
                                    if ((text.Contains("can't be regenerated") || 
                                         text.Contains("cannot be regenerated") || 
                                         text.Contains("can't regenerate") || 
                                         text.Contains("cannot regenerate") ||
                                         text.Contains("without regenerating")) && 
                                        card.Categories.Contains("Protection"))
                                    {
                                        card.Categories.Remove("Protection");
                                        anyModified = true;
                                    }
                                }
                            }
                        }
                        if (anyModified)
                        {
                            SaveDeck();
                        }
                    }
                    RefreshDeckState();
                }
            }
        }

        public string Name
        {
            get => _deck.Name;
            set
            {
                if (_deck.Name != value)
                {
                    _deck.Name = value;
                    OnPropertyChanged();
                    SaveDeck();
                }
            }
        }

        public Card? Commander
        {
            get => _deck.Commander;
            set
            {
                if (_deck.Commander != value)
                {
                    _deck.Commander = value;
                    if (_deck.Commander != null)
                    {
                        _deck.Commander.IsCommander = true;
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ColorIdentity));
                    OnPropertyChanged(nameof(DeckSize));
                    NotifyColorIdentityChanges();
                    NotifyGoalChanges();
                    RefreshDeckState();
                    SaveDeck();
                }
            }
        }

        private string _selectedGroupMode = "Card Type";
        public string SelectedGroupMode
        {
            get => _selectedGroupMode;
            set
            {
                if (SetProperty(ref _selectedGroupMode, value))
                {
                    RefreshDeckState();
                }
            }
        }

        public List<string> GroupModes { get; } = new()
        {
            "Card Type", "Custom Category", "Mana Value", "Color", "Rarity"
        };

        public List<string> SortOrders { get; } = new()
        {
            "EDHREC Rank", "Alphabetical", "Mana Value (Low-High)", "Mana Value (High-Low)", 
            "Card Type", "Color (WUBRG)", "Rarity", "Price (Low-High)", "Price (High-Low)", 
            "Set / Release Date", "Collector Number"
        };

        public ObservableCollection<DeckGroup> Groups { get; } = new();

        // Live collections for WPF UI binding
        public BulkObservableCollection<Card> Cards { get; } = new();
        public BulkObservableCollection<Card> CommanderList { get; } = new();

        public BulkObservableCollection<Card> Creatures { get; } = new();
        public BulkObservableCollection<Card> Instants { get; } = new();
        public BulkObservableCollection<Card> Sorceries { get; } = new();
        public BulkObservableCollection<Card> Artifacts { get; } = new();
        public BulkObservableCollection<Card> Enchantments { get; } = new();
        public BulkObservableCollection<Card> Planeswalkers { get; } = new();
        public BulkObservableCollection<Card> Lands { get; } = new();
        public BulkObservableCollection<Card> Others { get; } = new();

        public int DeckSize => _deck.DeckSize;
        public double AverageManaValue => _deck.AverageManaValue;
        public List<string> ColorIdentity => _deck.ColorIdentity;

        public int CommanderCount => CommanderList.Sum(c => c.Quantity);
        public int CreaturesCount => Creatures.Sum(c => c.Quantity);
        public int InstantsCount => Instants.Sum(c => c.Quantity);
        public int SorceriesCount => Sorceries.Sum(c => c.Quantity);
        public int ArtifactsCount => Artifacts.Sum(c => c.Quantity);
        public int EnchantmentsCount => Enchantments.Sum(c => c.Quantity);
        public int PlaneswalkersCount => Planeswalkers.Sum(c => c.Quantity);
        public int LandsCount => Lands.Sum(c => c.Quantity);
        public int OthersCount => Others.Sum(c => c.Quantity);

        public int RampCount => Cards.Where(c => 
            (c.Categories != null && c.Categories.Any(cat => cat.Equals("Ramp", StringComparison.OrdinalIgnoreCase))) ||
            (c.OracleText != null && (c.OracleText.Contains("search your library for a land", StringComparison.OrdinalIgnoreCase) ||
                                       c.OracleText.Contains("search your library for a basic land", StringComparison.OrdinalIgnoreCase) ||
                                       (c.OracleText.Contains("put onto the battlefield", StringComparison.OrdinalIgnoreCase) && c.OracleText.Contains("land", StringComparison.OrdinalIgnoreCase)) ||
                                       c.OracleText.Contains("add {", StringComparison.OrdinalIgnoreCase) ||
                                       c.OracleText.Contains("adds {", StringComparison.OrdinalIgnoreCase))) && !c.TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase)
        ).Sum(c => c.Quantity);

        public int DrawCount => Cards.Where(c => 
            (c.Categories != null && c.Categories.Any(cat => cat.Equals("Draw", StringComparison.OrdinalIgnoreCase) || cat.Equals("Card Draw", StringComparison.OrdinalIgnoreCase))) ||
            (c.OracleText != null && (c.OracleText.Contains("draw a card", StringComparison.OrdinalIgnoreCase) || 
                                       c.OracleText.Contains("draw cards", StringComparison.OrdinalIgnoreCase) ||
                                       c.OracleText.Contains("draws a card", StringComparison.OrdinalIgnoreCase)))
        ).Sum(c => c.Quantity);

        public int RemovalCount => Cards.Where(c => 
            (c.Categories != null && c.Categories.Any(cat => cat.Equals("Removal", StringComparison.OrdinalIgnoreCase) || cat.Equals("Spot Removal", StringComparison.OrdinalIgnoreCase))) ||
            (c.OracleText != null && (c.OracleText.Contains("destroy target", StringComparison.OrdinalIgnoreCase) || 
                                       c.OracleText.Contains("exile target", StringComparison.OrdinalIgnoreCase) ||
                                       (c.OracleText.Contains("return target", StringComparison.OrdinalIgnoreCase) && c.OracleText.Contains("to its owner's hand", StringComparison.OrdinalIgnoreCase))))
        ).Sum(c => c.Quantity);

        public int WipeCount => Cards.Where(c => 
            (c.Categories != null && c.Categories.Any(cat => cat.Equals("Wipe", StringComparison.OrdinalIgnoreCase) || cat.Equals("Board Wipe", StringComparison.OrdinalIgnoreCase))) ||
            (c.OracleText != null && (c.OracleText.Contains("destroy all", StringComparison.OrdinalIgnoreCase) || 
                                       c.OracleText.Contains("exile all", StringComparison.OrdinalIgnoreCase) ||
                                       c.OracleText.Contains("each player sacrifices", StringComparison.OrdinalIgnoreCase)))
        ).Sum(c => c.Quantity);

        public int ProtectionCount => Cards.Where(c => 
            (c.Categories != null && c.Categories.Any(cat => cat.Equals("Protection", StringComparison.OrdinalIgnoreCase))) ||
            (c.OracleText != null && (c.OracleText.Contains("gain protection from", StringComparison.OrdinalIgnoreCase) || 
                                       c.OracleText.Contains("hexproof", StringComparison.OrdinalIgnoreCase) ||
                                       c.OracleText.Contains("indestructible", StringComparison.OrdinalIgnoreCase) ||
                                       c.OracleText.Contains("counter target spell", StringComparison.OrdinalIgnoreCase)))
        ).Sum(c => c.Quantity);

        public int TutorsCount => Cards.Where(c => 
            (c.Categories != null && c.Categories.Any(cat => cat.Equals("Tutors", StringComparison.OrdinalIgnoreCase))) ||
            (c.OracleText != null && c.OracleText.Contains("search your library", StringComparison.OrdinalIgnoreCase) && !c.IsLand)
        ).Sum(c => c.Quantity);

        public int WinConditionsCount => Cards.Where(c => 
            c.OracleText != null && (
                c.OracleText.Contains("win the game", StringComparison.OrdinalIgnoreCase) || 
                c.OracleText.Contains("opponent loses the game", StringComparison.OrdinalIgnoreCase) ||
                c.OracleText.Contains("infect", StringComparison.OrdinalIgnoreCase) ||
                c.OracleText.Contains("extra turn", StringComparison.OrdinalIgnoreCase)
            )
        ).Sum(c => c.Quantity);

        // --- GOAL TARGETS (For Screen 3: Goals Screen) ---
        private int _landsGoal = 36;
        public int LandsGoal { get => _landsGoal; set { if (SetProperty(ref _landsGoal, value)) NotifyGoalChanges(); } }

        private int _rampGoal = 10;
        public int RampGoal { get => _rampGoal; set { if (SetProperty(ref _rampGoal, value)) NotifyGoalChanges(); } }

        private int _drawGoal = 10;
        public int DrawGoal { get => _drawGoal; set { if (SetProperty(ref _drawGoal, value)) NotifyGoalChanges(); } }

        private int _removalGoal = 8;
        public int RemovalGoal { get => _removalGoal; set { if (SetProperty(ref _removalGoal, value)) NotifyGoalChanges(); } }

        private int _wipeGoal = 3;
        public int WipeGoal { get => _wipeGoal; set { if (SetProperty(ref _wipeGoal, value)) NotifyGoalChanges(); } }

        private int _protectionGoal = 6;
        public int ProtectionGoal { get => _protectionGoal; set { if (SetProperty(ref _protectionGoal, value)) NotifyGoalChanges(); } }

        private int _tutorGoal = 4;
        public int TutorGoal { get => _tutorGoal; set { if (SetProperty(ref _tutorGoal, value)) NotifyGoalChanges(); } }

        private int _creatureGoal = 28;
        public int CreatureGoal { get => _creatureGoal; set { if (SetProperty(ref _creatureGoal, value)) NotifyGoalChanges(); } }

        private int _artifactGoal = 8;
        public int ArtifactGoal { get => _artifactGoal; set { if (SetProperty(ref _artifactGoal, value)) NotifyGoalChanges(); } }

        private int _enchantmentGoal = 6;
        public int EnchantmentGoal { get => _enchantmentGoal; set { if (SetProperty(ref _enchantmentGoal, value)) NotifyGoalChanges(); } }

        private int _instantGoal = 12;
        public int InstantGoal { get => _instantGoal; set { if (SetProperty(ref _instantGoal, value)) NotifyGoalChanges(); } }

        private int _sorceryGoal = 9;
        public int SorceryGoal { get => _sorceryGoal; set { if (SetProperty(ref _sorceryGoal, value)) NotifyGoalChanges(); } }

        private int _winConditionGoal = 4;
        public int WinConditionGoal { get => _winConditionGoal; set { if (SetProperty(ref _winConditionGoal, value)) NotifyGoalChanges(); } }

                // --- LANDS DISTRIBUTION (For Screen 4: Mana Designer) ---
        private int _basicsCount = 14;
        public int BasicsCount { get => _basicsCount; set { if (SetProperty(ref _basicsCount, value)) OnPropertyChanged(nameof(TotalLandsCount)); } }

        private int _dualsCount = 10;
        public int DualsCount { get => _dualsCount; set { if (SetProperty(ref _dualsCount, value)) OnPropertyChanged(nameof(TotalLandsCount)); } }

        private int _fetchesCount = 3;
        public int FetchesCount { get => _fetchesCount; set { if (SetProperty(ref _fetchesCount, value)) OnPropertyChanged(nameof(TotalLandsCount)); } }

        private int _utilityLandsCount = 2;
        public int UtilityLandsCount { get => _utilityLandsCount; set { if (SetProperty(ref _utilityLandsCount, value)) OnPropertyChanged(nameof(TotalLandsCount)); } }

        public int TotalLandsCount => BasicsCount + DualsCount + FetchesCount + UtilityLandsCount;

        public int EstimatedConsistency => Math.Max(40, Math.Min(95, 100 - (int)(AverageManaValue * 8) + (BasicsCount + DualsCount * 2)));

        public int GoalsCompletionPercentage
        {
            get
            {
                int metCount = 0;
                if (Math.Abs(LandsCount - LandsGoal) <= 2) metCount++;
                if (RampCount >= RampGoal) metCount++;
                if (DrawCount >= DrawGoal) metCount++;
                if (RemovalCount >= RemovalGoal) metCount++;
                if (WipeCount >= WipeGoal) metCount++;
                if (ProtectionCount >= ProtectionGoal) metCount++;
                if (TutorsCount >= TutorGoal) metCount++;
                if (Math.Abs(CreaturesCount - CreatureGoal) <= 4) metCount++;
                if (Math.Abs(ArtifactsCount - ArtifactGoal) <= 2) metCount++;
                if (Math.Abs(EnchantmentsCount - EnchantmentGoal) <= 2) metCount++;
                if (Math.Abs(InstantsCount - InstantGoal) <= 3) metCount++;
                if (Math.Abs(SorceriesCount - SorceryGoal) <= 3) metCount++;
                if (WinConditionsCount >= WinConditionGoal) metCount++;
                return (int)((metCount / 13.0) * 100);
            }
        }

        private void NotifyGoalChanges()
        {
            OnPropertyChanged(nameof(GoalsCompletionPercentage));
            OnPropertyChanged(nameof(LandsStatus));
            OnPropertyChanged(nameof(LandsStatusBrush));
            OnPropertyChanged(nameof(RampStatus));
            OnPropertyChanged(nameof(RampStatusBrush));
            OnPropertyChanged(nameof(DrawStatus));
            OnPropertyChanged(nameof(DrawStatusBrush));
            OnPropertyChanged(nameof(RemovalStatus));
            OnPropertyChanged(nameof(RemovalStatusBrush));
            OnPropertyChanged(nameof(WipeStatus));
            OnPropertyChanged(nameof(WipeStatusBrush));
            OnPropertyChanged(nameof(TutorStatus));
            OnPropertyChanged(nameof(TutorStatusBrush));
            OnPropertyChanged(nameof(WinConditionStatus));
            OnPropertyChanged(nameof(WinConditionStatusBrush));
        }

        // --- GOAL STATUS AND COLOR PROPERTIES (For Sidebar Goal Tracker) ---
        public string LandsStatus => GetGoalStatus(LandsCount, LandsGoal, true);
        public string LandsStatusBrush => GetGoalStatusBrush(LandsCount, LandsGoal, true);

        public string RampStatus => GetGoalStatus(RampCount, RampGoal, false);
        public string RampStatusBrush => GetGoalStatusBrush(RampCount, RampGoal, false);

        public string DrawStatus => GetGoalStatus(DrawCount, DrawGoal, false);
        public string DrawStatusBrush => GetGoalStatusBrush(DrawCount, DrawGoal, false);

        public string RemovalStatus => GetGoalStatus(RemovalCount, RemovalGoal, false);
        public string RemovalStatusBrush => GetGoalStatusBrush(RemovalCount, RemovalGoal, false);

        public string WipeStatus => GetGoalStatus(WipeCount, WipeGoal, false);
        public string WipeStatusBrush => GetGoalStatusBrush(WipeCount, WipeGoal, false);

        public string TutorStatus => GetGoalStatus(TutorsCount, TutorGoal, false);
        public string TutorStatusBrush => GetGoalStatusBrush(TutorsCount, TutorGoal, false);

        public string WinConditionStatus => GetGoalStatus(WinConditionsCount, WinConditionGoal, false);
        public string WinConditionStatusBrush => GetGoalStatusBrush(WinConditionsCount, WinConditionGoal, false);

        private string GetGoalStatus(int current, int goal, bool exactMatchNeeded)
        {
            if (goal <= 0) return "Not Set";
            if (exactMatchNeeded)
            {
                if (current < goal - 2) return "Under";
                if (current > goal + 2) return "Surplus";
                return "Met";
            }
            else
            {
                if (current < goal) return "Under";
                return "Met";
            }
        }

        private string GetGoalStatusBrush(int current, int goal, bool exactMatchNeeded)
        {
            string status = GetGoalStatus(current, goal, exactMatchNeeded);
            if (status == "Met") return "#2ecc71"; // Green
            if (status == "Surplus") return "#3498db"; // Blue
            return "#e67e22"; // Orange
        }

        // --- COLOR IDENTITY HELPER PROPERTIES ---
        public bool HasWhite => ColorIdentity != null && ColorIdentity.Contains("W");
        public bool HasBlue => ColorIdentity != null && ColorIdentity.Contains("U");
        public bool HasBlack => ColorIdentity != null && ColorIdentity.Contains("B");
        public bool HasRed => ColorIdentity != null && ColorIdentity.Contains("R");
        public bool HasGreen => ColorIdentity != null && ColorIdentity.Contains("G");

        public string ColorIdentityText
        {
            get
            {
                if (ColorIdentity == null || ColorIdentity.Count == 0) return "Colorless";
                return string.Join("", ColorIdentity);
            }
        }

        public void NotifyColorIdentityChanges()
        {
            OnPropertyChanged(nameof(ColorIdentity));
            OnPropertyChanged(nameof(HasWhite));
            OnPropertyChanged(nameof(HasBlue));
            OnPropertyChanged(nameof(HasBlack));
            OnPropertyChanged(nameof(HasRed));
            OnPropertyChanged(nameof(HasGreen));
            OnPropertyChanged(nameof(ColorIdentityText));
        }

        public int MaxCmcCount
        {
            get
            {
                var nonLandCards = Cards.Where(c => !c.IsLand).ToList();
                if (!nonLandCards.Any()) return 10; // Baseline default if empty
                
                var buckets = new int[8];
                foreach (var card in nonLandCards)
                {
                    int intCmc = (int)Math.Floor(card.Cmc);
                    int bucket = (intCmc < 0) ? 0 : (intCmc >= 7 ? 7 : intCmc);
                    buckets[bucket] += card.Quantity;
                }
                
                int max = buckets.Max();
                return max > 0 ? max : 10;
            }
        }

        private bool _enforceCommanderRules = true;
        private bool _enforceLegendaryCommander = true;
        private bool _enforceSingletonLimit = true;
        private bool _enforceDeckSizeCap = true;
        private bool _enforceColorIdentity = true;

        public bool EnforceCommanderRules
        {
            get => _enforceCommanderRules;
            set
            {
                if (SetProperty(ref _enforceCommanderRules, value))
                {
                    OnPropertyChanged(nameof(CanEnforceSubRules));
                }
            }
        }

        public bool EnforceLegendaryCommander
        {
            get => _enforceLegendaryCommander;
            set => SetProperty(ref _enforceLegendaryCommander, value);
        }

        public bool EnforceSingletonLimit
        {
            get => _enforceSingletonLimit;
            set => SetProperty(ref _enforceSingletonLimit, value);
        }

        public bool EnforceDeckSizeCap
        {
            get => _enforceDeckSizeCap;
            set => SetProperty(ref _enforceDeckSizeCap, value);
        }

        public bool EnforceColorIdentity
        {
            get => _enforceColorIdentity;
            set => SetProperty(ref _enforceColorIdentity, value);
        }

        public bool CanEnforceSubRules => EnforceCommanderRules;

        public event Action<string>? ValidationWarningTriggered;

        public DeckViewModel(DeckStorageService storageService)
        {
            _storageService = storageService;
            _deck = new Deck();
        }

        public void SaveDeck()
        {
            _storageService.SaveDeck(_deck);
        }

        private string _currentSortOrder = "EDHREC Rank";
        public string CurrentSortOrder
        {
            get => _currentSortOrder;
            set
            {
                if (SetProperty(ref _currentSortOrder, value))
                {
                    RefreshDeckState();
                }
            }
        }

        public void RefreshDeckState()
        {
            var allCards = new List<Card>();
            if (_deck.Commander != null)
            {
                _deck.Commander.IsCommander = true;
                allCards.Add(_deck.Commander);
            }

            // Sort mainboard cards before adding
            IEnumerable<Card> sortedCards = _deck.Cards;
            switch (CurrentSortOrder)
            {
                case "Alphabetical":
                    sortedCards = sortedCards.OrderBy(c => c.Name);
                    break;
                case "Mana Value (Low-High)":
                    sortedCards = sortedCards.OrderBy(c => c.Cmc).ThenBy(c => c.Name);
                    break;
                case "Mana Value (High-Low)":
                    sortedCards = sortedCards.OrderByDescending(c => c.Cmc).ThenBy(c => c.Name);
                    break;
                case "Card Type":
                    sortedCards = sortedCards.OrderBy(c => c.PrimaryType).ThenBy(c => c.Name);
                    break;
                case "Color (WUBRG)":
                    sortedCards = sortedCards.OrderBy(c => c.ColorSortIndex).ThenBy(c => c.ColorSortString).ThenBy(c => c.Name);
                    break;
                case "Rarity":
                    sortedCards = sortedCards.OrderBy(c => c.RaritySortIndex).ThenBy(c => c.Name);
                    break;
                case "Price (Low-High)":
                    sortedCards = sortedCards.OrderBy(c => c.PriceUsdValue < 0 ? double.MaxValue : c.PriceUsdValue).ThenBy(c => c.Name);
                    break;
                case "Price (High-Low)":
                    sortedCards = sortedCards.OrderByDescending(c => c.PriceUsdValue).ThenBy(c => c.Name);
                    break;
                case "Set / Release Date":
                    sortedCards = sortedCards.OrderBy(c => c.SetName).ThenBy(c => c.Name);
                    break;
                case "Collector Number":
                    sortedCards = sortedCards.OrderBy(c => c.CollectorNumberValue).ThenBy(c => c.CollectorNumber).ThenBy(c => c.Name);
                    break;
                case "EDHREC Rank":
                default:
                    sortedCards = sortedCards.OrderBy(c => c.EdhrecRank ?? int.MaxValue).ThenBy(c => c.Name);
                    break;
            }

            allCards.AddRange(sortedCards);

            Cards.ReplaceRange(allCards);
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Commander));
            OnPropertyChanged(nameof(DeckSize));
            OnPropertyChanged(nameof(AverageManaValue));
            OnPropertyChanged(nameof(ColorIdentity));
            OnPropertyChanged(nameof(MaxCmcCount));
            RefreshGroupedLists();
        }

        public void AddCard(Card card, bool setAsCommander = false, bool skipRefreshAndSave = false)
        {
            if (!setAsCommander)
            {
                if (Commander != null && Commander.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase))
                {
                    ValidationWarningTriggered?.Invoke($"Cannot add '{card.Name}': It is already set as your Commander!");
                    return;
                }
            }

            if (EnforceCommanderRules)
            {
                if (setAsCommander)
                {
                    // Check Commander Eligibility
                    bool isCommanderEligible = (card.TypeLine.Contains("Legendary", StringComparison.OrdinalIgnoreCase) && card.TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase)) ||
                                              (card.OracleText != null && card.OracleText.Contains("can be your commander", StringComparison.OrdinalIgnoreCase));
                    if (EnforceLegendaryCommander && !isCommanderEligible)
                    {
                        ValidationWarningTriggered?.Invoke($"Cannot set '{card.Name}' as Commander! A commander must be a Legendary Creature (or explicitly state it can be your commander).");
                        return;
                    }
                }
                else
                {
                    // Check deck size cap
                    if (EnforceDeckSizeCap && DeckSize >= 100)
                    {
                        ValidationWarningTriggered?.Invoke("Cannot add card: Your deck already contains 100 cards.");
                        return;
                    }

                    // Check color identity
                    if (EnforceColorIdentity && Commander != null)
                    {
                        bool isLegal = card.ColorIdentity.All(color => Commander.ColorIdentity.Contains(color));
                        if (!isLegal)
                        {
                            ValidationWarningTriggered?.Invoke($"Cannot add '{card.Name}': It is outside your commander's color identity ({string.Join("", Commander.ColorIdentity)})!");
                            return;
                        }
                    }

                    // Check singleton constraint
                    if (EnforceSingletonLimit)
                    {
                        bool isBasic = card.TypeLine.Contains("Basic", StringComparison.OrdinalIgnoreCase) && card.IsLand;
                        bool isUnlimited = card.OracleText != null && card.OracleText.Contains("any number of cards named", StringComparison.OrdinalIgnoreCase);

                        if (!isBasic && !isUnlimited)
                        {
                            var existingInDeck = _deck.Cards.FirstOrDefault(c => c.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase));
                            if (existingInDeck != null)
                            {
                                ValidationWarningTriggered?.Invoke($"Cannot add '{card.Name}': It is already in your deck (only 1 copy allowed).");
                                return;
                            }
                        }
                    }
                }
            }

            if (setAsCommander)
            {
                // If the card is currently in the main board, remove it
                var existingInMainboard = _deck.Cards.FirstOrDefault(c => c.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase));
                if (existingInMainboard != null)
                {
                    _deck.Cards.Remove(existingInMainboard);
                }

                // If we already have a commander, move them to the main board
                if (Commander != null)
                {
                    AddCard(Commander, false, true);
                }
                
                // Clone the card for commander slot
                var newComm = CloneCard(card);
                newComm.IsCommander = true;
                newComm.Quantity = 1;
                
                if (skipRefreshAndSave)
                {
                    _deck.Commander = newComm;
                }
                else
                {
                    Commander = newComm;
                }
            }
            else
            {
                var existing = _deck.Cards.FirstOrDefault(c => c.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Quantity++;
                }
                else
                {
                    var newCard = CloneCard(card);
                    newCard.Quantity = 1;
                    _deck.Cards.Add(newCard);
                }
                
                if (!skipRefreshAndSave)
                {
                    RefreshDeckState();
                    SaveDeck();
                }
            }
        }

        public void RemoveCard(Card card)
        {
            if (card.IsCommander)
            {
                Commander = null;
            }
            else
            {
                var existing = _deck.Cards.FirstOrDefault(c => c.Id == card.Id);
                if (existing != null)
                {
                    if (existing.Quantity > 1)
                    {
                        existing.Quantity--;
                    }
                    else
                    {
                        _deck.Cards.Remove(existing);
                    }
                    RefreshDeckState();
                    SaveDeck();
                }
            }
        }

        public void RemoveAllCopies(Card card)
        {
            if (card.IsCommander)
            {
                Commander = null;
            }
            else
            {
                var existing = _deck.Cards.FirstOrDefault(c => c.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    _deck.Cards.Remove(existing);
                    RefreshDeckState();
                    SaveDeck();
                }
            }
        }

        public Card CloneCard(Card source)
        {
            var cloned = new Card
            {
                Id = source.Id,
                Name = source.Name,
                ManaCost = source.ManaCost,
                Cmc = source.Cmc,
                TypeLine = source.TypeLine,
                OracleText = source.OracleText,
                Colors = source.Colors != null ? new List<string>(source.Colors) : new List<string>(),
                ColorIdentity = source.ColorIdentity != null ? new List<string>(source.ColorIdentity) : new List<string>(),
                Rarity = source.Rarity,
                EdhrecRank = source.EdhrecRank,
                NormalImageUrl = source.NormalImageUrl,
                ArtCropImageUrl = source.ArtCropImageUrl,
                PriceUsd = source.PriceUsd,
                Set = source.Set,
                SetName = source.SetName,
                CollectorNumber = source.CollectorNumber,
                Quantity = source.Quantity,
                Categories = source.Categories != null ? new List<string>(source.Categories) : new List<string>()
            };

            // Auto-categorize if the source card doesn't have any categories assigned yet
            if (cloned.Categories.Count == 0)
            {
                cloned.Categories = AutoCategorize(cloned);
            }

            return cloned;
        }

        public static List<string> AutoCategorize(Card card)
        {
            var cats = new List<string>();
            if (card == null) return cats;

            string text = card.OracleText?.ToLowerInvariant() ?? string.Empty;
            string type = card.TypeLine?.ToLowerInvariant() ?? string.Empty;

            // 1. Landfall
            if (text.Contains("landfall") || text.Contains("whenever a land enters the battlefield"))
            {
                cats.Add("Landfall");
            }

            // 2. Counterspell
            if (text.Contains("counter target") && (text.Contains("spell") || text.Contains("activated") || text.Contains("trigger")))
            {
                cats.Add("Counterspell");
            }

            // 3. Board Wipe
            if (text.Contains("destroy all") || text.Contains("exile all") || 
                (text.Contains("each player sacrifices") && (text.Contains("all") || text.Contains("each"))) ||
                (text.Contains("return all") && (text.Contains("hand") || text.Contains("hands"))) ||
                (text.Contains("damage to each") && (text.Contains("creature") || text.Contains("player"))) ||
                (text.Contains("damage to all") && text.Contains("creature")) ||
                text.Contains("each creature gets -") || text.Contains("creatures get -"))
            {
                if (!type.Contains("land")) // lands shouldn't be sweepers usually
                {
                    cats.Add("Board Wipe");
                }
            }

            // 4. Removal (Spot removal - exclude board wipes to avoid double tagging)
            if (!cats.Contains("Board Wipe"))
            {
                if (text.Contains("destroy target") || text.Contains("exile target") || 
                    text.Contains("return target permanent") || text.Contains("return target creature") || 
                    text.Contains("return target nonland permanent"))
                {
                    cats.Add("Removal");
                }
                else if ((card.IsInstant || card.IsSorcery) && (text.Contains("damage to target") || text.Contains("damage to any target")))
                {
                    cats.Add("Removal");
                }
            }

            // 5. Ramp (excluding basic lands)
            if (!card.IsLand || text.Contains("cabal coffers") || text.Contains("temple of the false god"))
            {
                bool isRamp = false;

                // Land search ramp
                if (text.Contains("search your library") && 
                    (text.Contains("land card") || text.Contains("basic land")) && 
                    (text.Contains("battlefield") || text.Contains("onto the battlefield")))
                {
                    isRamp = true;
                }
                // Mana dorks / rocks (taps for mana)
                else if (text.Contains("{t}: add") || text.Contains("add {c}") || 
                         text.Contains("add {w}") || text.Contains("add {u}") || 
                         text.Contains("add {b}") || text.Contains("add {r}") || 
                         text.Contains("add {g}"))
                {
                    if (!type.Contains("land"))
                    {
                        isRamp = true;
                    }
                }
                
                if (isRamp)
                {
                    cats.Add("Ramp");
                }
            }

            // 6. Draw (Card draw or card selection)
            if ((text.Contains("draw") && (text.Contains("card") || text.Contains("cards"))) || 
                (text.Contains("look at the top") && text.Contains("library") && text.Contains("hand")) ||
                (text.Contains("exile the top") && text.Contains("library") && text.Contains("play")) ||
                (text.Contains("reveal the top") && text.Contains("library") && text.Contains("hand")))
            {
                cats.Add("Draw");
            }

            // 7. Counters (Counter placement/proliferation)
            if (text.Contains("+1/+1 counter") || text.Contains("-1/-1 counter") || 
                text.Contains("put a counter") || text.Contains("put a +1/+1") ||
                text.Contains("proliferate"))
            {
                cats.Add("Counters");
            }

            // 8. Tutors
            if (text.Contains("search your library") && !cats.Contains("Ramp"))
            {
                cats.Add("Tutors");
            }

            // 9. Pump
            if (text.Contains("+x/+y") || text.Contains("+1/+1 until end of turn") || 
                text.Contains("+2/+2 until end of turn") || text.Contains("+3/+3 until end of turn") || 
                text.Contains("double target creature") || text.Contains("gain trample") || text.Contains("gain double strike"))
            {
                cats.Add("Pump");
            }

            // 10. Protection
            bool hasRegeneration = text.Contains("regenerate") && 
                                   !text.Contains("can't be regenerated") && 
                                   !text.Contains("cannot be regenerated") &&
                                   !text.Contains("can't regenerate") &&
                                   !text.Contains("cannot regenerate") &&
                                   !text.Contains("without regenerating");

            if (text.Contains("hexproof") || text.Contains("indestructible") || 
                text.Contains("shroud") || text.Contains("protection from") || 
                hasRegeneration || text.Contains("phase out"))
            {
                cats.Add("Protection");
            }

            // 11. Recursion
            if (text.Contains("graveyard") && 
                (text.Contains("return") || text.Contains("put") || text.Contains("reanimate")) &&
                (text.Contains("battlefield") || text.Contains("your hand") || text.Contains("under your control") || text.Contains("onto the battlefield")))
            {
                if (text.Contains("from your graveyard") || text.Contains("from a graveyard") || text.Contains("from among cards in your graveyard"))
                {
                    cats.Add("Recursion");
                }
            }

            return cats;
        }

        private void RefreshGroupedLists()
        {
            Groups.Clear();

            var commanderList = new List<Card>();
            if (_deck.Commander != null)
            {
                commanderList.Add(_deck.Commander);
            }
            CommanderList.ReplaceRange(commanderList);

            var mainboardCards = Cards.Where(c => !c.IsCommander).ToList();

            if (SelectedGroupMode == "Card Type")
            {
                var types = new[] { "Creature", "Planeswalker", "Instant", "Sorcery", "Artifact", "Enchantment", "Land", "Other" };
                var typeHeaders = new Dictionary<string, string>
                {
                    { "Creature", "Creatures" },
                    { "Planeswalker", "Planeswalkers" },
                    { "Instant", "Instants" },
                    { "Sorcery", "Sorceries" },
                    { "Artifact", "Artifacts" },
                    { "Enchantment", "Enchantments" },
                    { "Land", "Lands" },
                    { "Other", "Other Spells" }
                };

                foreach (var t in types)
                {
                    var matching = mainboardCards.Where(c => c.PrimaryType == t).ToList();
                    if (matching.Any())
                    {
                        Groups.Add(new DeckGroup
                        {
                            GroupName = typeHeaders[t],
                            Cards = matching,
                            HeaderBrush = System.Windows.Media.Brushes.White
                        });
                    }
                }
            }
            else if (SelectedGroupMode == "Custom Category")
            {
                var allCategories = mainboardCards
                    .SelectMany(c => c.Categories ?? new List<string>())
                    .Select(tag => tag.Trim())
                    .Where(tag => !string.IsNullOrEmpty(tag))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag)
                    .ToList();

                foreach (var categoryName in allCategories)
                {
                    var matching = mainboardCards
                        .Where(c => c.Categories != null && c.Categories.Any(tag => tag.Equals(categoryName, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    if (matching.Any())
                    {
                        Groups.Add(new DeckGroup
                        {
                            GroupName = categoryName,
                            Cards = matching,
                            HeaderBrush = System.Windows.Application.Current != null && System.Windows.Application.Current.Resources.Contains("AccentGoldBrush")
                                ? System.Windows.Application.Current.Resources["AccentGoldBrush"] as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Gold
                                : System.Windows.Media.Brushes.Gold
                        });
                    }
                }

                var untagged = mainboardCards
                    .Where(c => c.Categories == null || c.Categories.Count == 0 || c.Categories.All(tag => string.IsNullOrWhiteSpace(tag)))
                    .ToList();

                if (untagged.Any())
                {
                    Groups.Add(new DeckGroup
                    {
                        GroupName = "Untagged / Other",
                        Cards = untagged,
                        HeaderBrush = System.Windows.Media.Brushes.Gray
                    });
                }
            }
            else if (SelectedGroupMode == "Mana Value")
            {
                for (int i = 0; i <= 7; i++)
                {
                    List<Card> matching;
                    string groupName;
                    if (i == 7)
                    {
                        matching = mainboardCards.Where(c => c.Cmc >= 7).ToList();
                        groupName = "7+ Mana Value";
                    }
                    else
                    {
                        matching = mainboardCards.Where(c => (int)Math.Floor(c.Cmc) == i && c.Cmc < 7).ToList();
                        groupName = $"{i} Mana Value";
                    }

                    if (matching.Any())
                    {
                        Groups.Add(new DeckGroup
                        {
                            GroupName = groupName,
                            Cards = matching,
                            HeaderBrush = System.Windows.Media.Brushes.White
                        });
                    }
                }
            }
            else if (SelectedGroupMode == "Color")
            {
                var colorBrushes = new Dictionary<string, System.Windows.Media.Brush>();
                if (System.Windows.Application.Current != null)
                {
                    colorBrushes["W"] = System.Windows.Application.Current.Resources["MtgWhiteGlow"] as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.White;
                    colorBrushes["U"] = System.Windows.Application.Current.Resources["MtgBlueGlow"] as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.DeepSkyBlue;
                    colorBrushes["B"] = System.Windows.Application.Current.Resources["MtgBlackGlow"] as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.MediumPurple;
                    colorBrushes["R"] = System.Windows.Application.Current.Resources["MtgRedGlow"] as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Tomato;
                    colorBrushes["G"] = System.Windows.Application.Current.Resources["MtgGreenGlow"] as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.LimeGreen;
                    colorBrushes["Multi"] = System.Windows.Application.Current.Resources["MtgGoldGlow"] as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Gold;
                    colorBrushes["C"] = System.Windows.Application.Current.Resources["MtgColorlessGlow"] as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Silver;
                }
                else
                {
                    colorBrushes["W"] = System.Windows.Media.Brushes.White;
                    colorBrushes["U"] = System.Windows.Media.Brushes.DeepSkyBlue;
                    colorBrushes["B"] = System.Windows.Media.Brushes.MediumPurple;
                    colorBrushes["R"] = System.Windows.Media.Brushes.Tomato;
                    colorBrushes["G"] = System.Windows.Media.Brushes.LimeGreen;
                    colorBrushes["Multi"] = System.Windows.Media.Brushes.Gold;
                    colorBrushes["C"] = System.Windows.Media.Brushes.Silver;
                }

                var nonLands = mainboardCards.Where(c => !c.IsLand).ToList();
                var lands = mainboardCards.Where(c => c.IsLand).ToList();

                var colorKeys = new[] { "W", "U", "B", "R", "G", "Multi", "C" };
                var colorNames = new Dictionary<string, string>
                {
                    { "W", "White" }, { "U", "Blue" }, { "B", "Black" }, { "R", "Red" }, { "G", "Green" },
                    { "Multi", "Multicolored" }, { "C", "Colorless" }
                };

                foreach (var key in colorKeys)
                {
                    List<Card> matching;
                    if (key == "Multi")
                    {
                        matching = nonLands.Where(c => c.Colors != null && c.Colors.Count > 1).ToList();
                    }
                    else if (key == "C")
                    {
                        matching = nonLands.Where(c => c.Colors == null || c.Colors.Count == 0).ToList();
                    }
                    else
                    {
                        matching = nonLands.Where(c => c.Colors != null && c.Colors.Count == 1 && c.Colors[0].Equals(key, StringComparison.OrdinalIgnoreCase)).ToList();
                    }

                    if (matching.Any())
                    {
                        Groups.Add(new DeckGroup
                        {
                            GroupName = colorNames[key],
                            Cards = matching,
                            HeaderBrush = colorBrushes[key]
                        });
                    }
                }

                if (lands.Any())
                {
                    Groups.Add(new DeckGroup
                    {
                        GroupName = "Lands",
                        Cards = lands,
                        HeaderBrush = System.Windows.Media.Brushes.Chocolate
                    });
                }
            }
            else if (SelectedGroupMode == "Rarity")
            {
                var rarities = new[] { "mythic", "rare", "uncommon", "common", "other" };
                var rarityHeaders = new Dictionary<string, string>
                {
                    { "mythic", "Mythic Rare" },
                    { "rare", "Rare" },
                    { "uncommon", "Uncommon" },
                    { "common", "Common" },
                    { "other", "Special / Other Rarity" }
                };
                var rarityBrushes = new Dictionary<string, System.Windows.Media.Brush>
                {
                    { "mythic", System.Windows.Media.Brushes.OrangeRed },
                    { "rare", System.Windows.Media.Brushes.Gold },
                    { "uncommon", System.Windows.Media.Brushes.Silver },
                    { "common", System.Windows.Media.Brushes.White },
                    { "other", System.Windows.Media.Brushes.Purple }
                };

                foreach (var r in rarities)
                {
                    List<Card> matching;
                    if (r == "other")
                    {
                        matching = mainboardCards.Where(c => string.IsNullOrEmpty(c.Rarity) || !rarities.Contains(c.Rarity.ToLower())).ToList();
                    }
                    else
                    {
                        matching = mainboardCards.Where(c => c.Rarity != null && c.Rarity.Equals(r, StringComparison.OrdinalIgnoreCase)).ToList();
                    }

                    if (matching.Any())
                    {
                        Groups.Add(new DeckGroup
                        {
                            GroupName = rarityHeaders[r],
                            Cards = matching,
                            HeaderBrush = rarityBrushes[r]
                        });
                    }
                }
            }

            OnPropertyChanged(nameof(DeckSize));
            OnPropertyChanged(nameof(AverageManaValue));
            OnPropertyChanged(nameof(CommanderCount));
            OnPropertyChanged(nameof(Groups));
            OnPropertyChanged(nameof(RampCount));
            OnPropertyChanged(nameof(DrawCount));
            OnPropertyChanged(nameof(RemovalCount));
            OnPropertyChanged(nameof(WipeCount));
            OnPropertyChanged(nameof(ProtectionCount));
            OnPropertyChanged(nameof(TutorsCount));
            OnPropertyChanged(nameof(WinConditionsCount));
            OnPropertyChanged(nameof(CreaturesCount));
            OnPropertyChanged(nameof(InstantsCount));
            OnPropertyChanged(nameof(SorceriesCount));
            OnPropertyChanged(nameof(ArtifactsCount));
            OnPropertyChanged(nameof(EnchantmentsCount));
            OnPropertyChanged(nameof(LandsCount));
            OnPropertyChanged(nameof(GoalsCompletionPercentage));
            NotifyColorIdentityChanges();
            NotifyGoalChanges();
        }

        public void ImportFromText(string textList, List<Card> localDb)
        {
            _deck.Cards.Clear();
            _deck.Commander = null;

            var lines = textList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var regex = new Regex(@"^(?:(\d+)x?\s+)?([^(\r\n\[]+)(?:\s+\(|\[|$)", RegexOptions.Compiled);
            var importWarnings = new List<string>();
            int deckSizeCount = 0;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Simple check for commander tags
                bool setAsCommander = trimmed.Contains("*CMDR*", StringComparison.OrdinalIgnoreCase) || 
                                     trimmed.Contains("*COMMANDER*", StringComparison.OrdinalIgnoreCase);

                var match = regex.Match(trimmed);
                if (match.Success)
                {
                    var qtyStr = match.Groups[1].Value;
                    int qty = string.IsNullOrEmpty(qtyStr) ? 1 : int.Parse(qtyStr);
                    var cardName = match.Groups[2].Value.Trim();

                    // Extract categories from brackets first (e.g. "1 Arcane Signet [Ramp, Mana Rocks]")
                    var categories = new List<string>();
                    var bracketMatches = Regex.Matches(trimmed, @"\[([^\]]+)\]");
                    var defaultCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "Commander", "Creature", "Land", "Sorcery", "Planeswalker", "Enchantment", 
                        "Artifact", "Instant", "Card", "Maybeboard", "Sideboard", "Battle", "Kindred", "Tribal"
                    };
                    foreach (Match bm in bracketMatches)
                    {
                        var catContent = bm.Groups[1].Value;
                        var parts = catContent.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var part in parts)
                        {
                            var cleanCat = part.Trim();
                            if (!string.IsNullOrEmpty(cleanCat) && !defaultCategories.Contains(cleanCat))
                            {
                                categories.Add(cleanCat);
                            }
                        }
                    }

                    // Search locally in database
                    var matchCard = localDb.FirstOrDefault(c => c.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase));
                    
                    // Fallback to fuzzy starts-with if exact match fails
                    if (matchCard == null)
                    {
                        matchCard = localDb.FirstOrDefault(c => c.Name.StartsWith(cardName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (matchCard != null)
                    {
                        if (EnforceCommanderRules)
                        {
                            if (setAsCommander)
                            {
                                bool isCommanderEligible = (matchCard.TypeLine.Contains("Legendary", StringComparison.OrdinalIgnoreCase) && matchCard.TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase)) ||
                                                          (matchCard.OracleText != null && matchCard.OracleText.Contains("can be your commander", StringComparison.OrdinalIgnoreCase));
                                if (EnforceLegendaryCommander && !isCommanderEligible)
                                {
                                    importWarnings.Add($"'{matchCard.Name}' is not eligible to be Commander.");
                                    continue;
                                }

                                AddCard(matchCard, true, true);
                                if (Commander != null)
                                {
                                    foreach (var cat in categories)
                                    {
                                        if (!Commander.Categories.Contains(cat))
                                            Commander.Categories.Add(cat);
                                    }
                                }
                                deckSizeCount++;
                            }
                            else
                            {
                                // Check deck size
                                if (EnforceDeckSizeCap && deckSizeCount + qty > 100)
                                {
                                    importWarnings.Add($"Could not add {qty}x '{matchCard.Name}': Exceeds 100-card cap.");
                                    continue;
                                }

                                // Check if already set as commander
                                if (Commander != null && Commander.Name.Equals(matchCard.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    importWarnings.Add($"'{matchCard.Name}' is already set as Commander.");
                                    continue;
                                }

                                // Check color identity
                                var activeComm = Commander;
                                if (EnforceColorIdentity && activeComm != null)
                                {
                                    bool isLegalIdentity = matchCard.ColorIdentity.All(color => activeComm.ColorIdentity.Contains(color));
                                    if (!isLegalIdentity)
                                    {
                                        importWarnings.Add($"'{matchCard.Name}' is outside Commander's color identity.");
                                        continue;
                                    }
                                }

                                // Check singleton
                                if (EnforceSingletonLimit)
                                {
                                    bool isBasic = matchCard.TypeLine.Contains("Basic", StringComparison.OrdinalIgnoreCase) && matchCard.IsLand;
                                    bool isUnlimited = matchCard.OracleText != null && matchCard.OracleText.Contains("any number of cards named", StringComparison.OrdinalIgnoreCase);
                                    
                                    if (!isBasic && !isUnlimited)
                                    {
                                        var existing = _deck.Cards.FirstOrDefault(c => c.Name.Equals(matchCard.Name, StringComparison.OrdinalIgnoreCase));
                                        if (existing != null || qty > 1)
                                        {
                                            importWarnings.Add($"'{matchCard.Name}' violates the singleton rule.");
                                            continue;
                                        }
                                    }
                                }

                                // Optimized bulk add quantity
                                var existingCard = _deck.Cards.FirstOrDefault(c => c.Name.Equals(matchCard.Name, StringComparison.OrdinalIgnoreCase));
                                if (existingCard != null)
                                {
                                    existingCard.Quantity += qty;
                                    foreach (var cat in categories)
                                    {
                                        if (!existingCard.Categories.Contains(cat))
                                            existingCard.Categories.Add(cat);
                                    }
                                }
                                else
                                {
                                    var newCard = CloneCard(matchCard);
                                    newCard.Quantity = qty;
                                    newCard.Categories = new List<string>(categories);
                                    _deck.Cards.Add(newCard);
                                }
                                deckSizeCount += qty;
                            }
                        }
                        else
                        {
                            // If rules are disabled, add standard way
                            if (setAsCommander)
                            {
                                AddCard(matchCard, true, true);
                                if (Commander != null)
                                {
                                    foreach (var cat in categories)
                                    {
                                        if (!Commander.Categories.Contains(cat))
                                            Commander.Categories.Add(cat);
                                    }
                                }
                                deckSizeCount++;
                            }
                            else
                            {
                                if (Commander != null && Commander.Name.Equals(matchCard.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    importWarnings.Add($"'{matchCard.Name}' is already set as Commander.");
                                    continue;
                                }

                                var existing = _deck.Cards.FirstOrDefault(c => c.Name.Equals(matchCard.Name, StringComparison.OrdinalIgnoreCase));
                                if (existing != null)
                                {
                                    existing.Quantity += qty;
                                    foreach (var cat in categories)
                                    {
                                        if (!existing.Categories.Contains(cat))
                                            existing.Categories.Add(cat);
                                    }
                                }
                                else
                                {
                                    var newCard = CloneCard(matchCard);
                                    newCard.Quantity = qty;
                                    newCard.Categories = new List<string>(categories);
                                    _deck.Cards.Add(newCard);
                                }
                                deckSizeCount += qty;
                            }
                        }
                    }
                }
            }

            RefreshDeckState();
            SaveDeck();

            if (importWarnings.Any())
            {
                var summary = "Import completed with restrictions:\n" + string.Join("\n", importWarnings.Distinct().Take(8));
                if (importWarnings.Distinct().Count() > 8)
                {
                    summary += $"\n...and {importWarnings.Distinct().Count() - 8} more rules violations.";
                }
                ValidationWarningTriggered?.Invoke(summary);
            }
        }

        public async Task ImportFromArchidektAsync(string deckId, List<Card> localDb)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MtgCommanderBuilder/1.0 (local desktop deck builder; rsuff)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            
            var url = $"https://archidekt.com/api/decks/{deckId}/";
            var jsonString = await client.GetStringAsync(url);
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var archidektDeck = JsonSerializer.Deserialize<ArchidektDeckDto>(jsonString, options);
            
            if (archidektDeck == null || archidektDeck.Cards == null)
            {
                throw new Exception("The response from Archidekt did not contain any valid deck or card data.");
            }

            ImportFromArchidektDto(archidektDeck, localDb);
        }

        public void ImportFromArchidektDto(ArchidektDeckDto archidektDeck, List<Card> localDb)
        {
            _deck.Cards.Clear();
            _deck.Commander = null;

            if (!string.IsNullOrWhiteSpace(archidektDeck.Name))
            {
                _deck.Name = archidektDeck.Name;
                OnPropertyChanged(nameof(Name));
            }

            var importWarnings = new List<string>();
            int deckSizeCount = 0;

            // Default categories to filter out
            var defaultCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Commander", "Creature", "Land", "Sorcery", "Planeswalker", "Enchantment", 
                "Artifact", "Instant", "Card", "Maybeboard", "Sideboard", "Battle", "Kindred", "Tribal"
            };

            foreach (var entry in archidektDeck.Cards)
            {
                if (entry.Card?.OracleCard == null || string.IsNullOrWhiteSpace(entry.Card.OracleCard.Name))
                    continue;

                var cardName = entry.Card.OracleCard.Name;
                int qty = entry.Quantity;
                var rawCats = entry.Categories ?? new List<string>();

                // Check if maybeboard card
                if (rawCats.Any(c => c.Equals("Maybeboard", StringComparison.OrdinalIgnoreCase)))
                {
                    continue; // Skip maybeboard
                }

                bool setAsCommander = rawCats.Any(c => c.Equals("Commander", StringComparison.OrdinalIgnoreCase));

                // Filter out standard categories to get only custom categories
                var customCats = rawCats.Where(c => !defaultCategories.Contains(c)).ToList();

                // Search locally in database
                var matchCard = localDb.FirstOrDefault(c => c.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase));
                if (matchCard == null)
                {
                    matchCard = localDb.FirstOrDefault(c => c.Name.StartsWith(cardName, StringComparison.OrdinalIgnoreCase));
                }

                if (matchCard != null)
                {
                    if (EnforceCommanderRules)
                    {
                        if (setAsCommander)
                        {
                            bool isCommanderEligible = (matchCard.TypeLine.Contains("Legendary", StringComparison.OrdinalIgnoreCase) && matchCard.TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase)) ||
                                                      (matchCard.OracleText != null && matchCard.OracleText.Contains("can be your commander", StringComparison.OrdinalIgnoreCase));
                            if (EnforceLegendaryCommander && !isCommanderEligible)
                            {
                                importWarnings.Add($"'{matchCard.Name}' is not eligible to be Commander.");
                                continue;
                            }

                            AddCard(matchCard, true, true);
                            if (Commander != null)
                            {
                                Commander.Categories = new List<string>(customCats);
                            }
                            deckSizeCount++;
                        }
                        else
                        {
                            // Check deck size
                            if (EnforceDeckSizeCap && deckSizeCount + qty > 100)
                            {
                                importWarnings.Add($"Could not add {qty}x '{matchCard.Name}': Exceeds 100-card cap.");
                                continue;
                            }

                            // Check if already set as commander
                            if (Commander != null && Commander.Name.Equals(matchCard.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                importWarnings.Add($"'{matchCard.Name}' is already set as Commander.");
                                continue;
                            }

                            // Check color identity
                            var activeComm = Commander;
                            if (EnforceColorIdentity && activeComm != null)
                            {
                                bool isLegalIdentity = matchCard.ColorIdentity.All(color => activeComm.ColorIdentity.Contains(color));
                                if (!isLegalIdentity)
                                {
                                    importWarnings.Add($"'{matchCard.Name}' is outside Commander's color identity.");
                                    continue;
                                }
                            }

                            // Check singleton
                            if (EnforceSingletonLimit)
                            {
                                bool isBasic = matchCard.TypeLine.Contains("Basic", StringComparison.OrdinalIgnoreCase) && matchCard.IsLand;
                                bool isUnlimited = matchCard.OracleText != null && matchCard.OracleText.Contains("any number of cards named", StringComparison.OrdinalIgnoreCase);
                                
                                if (!isBasic && !isUnlimited)
                                {
                                    var existing = _deck.Cards.FirstOrDefault(c => c.Name.Equals(matchCard.Name, StringComparison.OrdinalIgnoreCase));
                                    if (existing != null || qty > 1)
                                    {
                                        importWarnings.Add($"'{matchCard.Name}' violates the singleton rule.");
                                        continue;
                                    }
                                }
                            }

                            // Optimized bulk add quantity
                            var existingCard = _deck.Cards.FirstOrDefault(c => c.Name.Equals(matchCard.Name, StringComparison.OrdinalIgnoreCase));
                            if (existingCard != null)
                            {
                                existingCard.Quantity += qty;
                                foreach (var cat in customCats)
                                {
                                    if (!existingCard.Categories.Contains(cat))
                                        existingCard.Categories.Add(cat);
                                }
                            }
                            else
                            {
                                var newCard = CloneCard(matchCard);
                                newCard.Quantity = qty;
                                newCard.Categories = new List<string>(customCats);
                                _deck.Cards.Add(newCard);
                            }
                            deckSizeCount += qty;
                        }
                    }
                    else
                    {
                        // If rules are disabled, add standard way
                        if (setAsCommander)
                        {
                            AddCard(matchCard, true, true);
                            if (Commander != null)
                            {
                                Commander.Categories = new List<string>(customCats);
                            }
                            deckSizeCount++;
                        }
                        else
                        {
                            if (Commander != null && Commander.Name.Equals(matchCard.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                importWarnings.Add($"'{matchCard.Name}' is already set as Commander.");
                                continue;
                            }

                            var existing = _deck.Cards.FirstOrDefault(c => c.Name.Equals(matchCard.Name, StringComparison.OrdinalIgnoreCase));
                            if (existing != null)
                            {
                                existing.Quantity += qty;
                                foreach (var cat in customCats)
                                {
                                    if (!existing.Categories.Contains(cat))
                                        existing.Categories.Add(cat);
                                }
                            }
                            else
                            {
                                var newCard = CloneCard(matchCard);
                                newCard.Quantity = qty;
                                newCard.Categories = new List<string>(customCats);
                                _deck.Cards.Add(newCard);
                            }
                            deckSizeCount += qty;
                        }
                    }
                }
            }

            RefreshDeckState();
            SaveDeck();

            if (importWarnings.Any())
            {
                var summary = "Import completed with restrictions:\n" + string.Join("\n", importWarnings.Distinct().Take(8));
                if (importWarnings.Distinct().Count() > 8)
                {
                    summary += $"\n...and {importWarnings.Distinct().Count() - 8} more rules violations.";
                }
                ValidationWarningTriggered?.Invoke(summary);
            }
        }

        public string ExportToText()
        {
            var writer = new System.Text.StringBuilder();
            if (Commander != null)
            {
                writer.AppendLine($"1 {Commander.Name} *CMDR*");
            }

            foreach (var card in Cards)
            {
                if (card.IsCommander) continue;
                writer.AppendLine($"{card.Quantity} {card.Name}");
            }

            return writer.ToString();
        }

        public string ExportToMoxfieldText()
        {
            var writer = new System.Text.StringBuilder();
            if (Commander != null)
            {
                writer.AppendLine($"1 {Commander.Name} ({Commander.SetDisplay}) {Commander.CollectorNumber} *CMDR*");
            }

            foreach (var card in Cards)
            {
                if (card.IsCommander) continue;
                writer.AppendLine($"{card.Quantity} {card.Name} ({card.SetDisplay}) {card.CollectorNumber}");
            }

            return writer.ToString();
        }

        public string ExportToMpcXml()
        {
            var writer = new System.Text.StringBuilder();
            writer.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            writer.AppendLine("<order>");
            writer.AppendLine("  <details>");
            writer.AppendLine($"    <quantity>{DeckSize}</quantity>");
            
            // Bracket size for MPC
            int bracket = 18;
            if (DeckSize <= 18) bracket = 18;
            else if (DeckSize <= 36) bracket = 36;
            else if (DeckSize <= 54) bracket = 54;
            else if (DeckSize <= 72) bracket = 72;
            else if (DeckSize <= 90) bracket = 90;
            else if (DeckSize <= 108) bracket = 108;
            else if (DeckSize <= 126) bracket = 126;
            else if (DeckSize <= 144) bracket = 144;
            else if (DeckSize <= 180) bracket = 180;
            else if (DeckSize <= 198) bracket = 198;
            else bracket = 612;

            writer.AppendLine($"    <bracket>{bracket}</bracket>");
            writer.AppendLine("    <stock>(S30) Standard Smooth</stock>");
            writer.AppendLine("    <foil>false</foil>");
            writer.AppendLine("  </details>");
            
            writer.AppendLine("  <fronts>");
            
            int slotIndex = 0;
            // Add commander first
            if (Commander != null)
            {
                writer.AppendLine("    <card>");
                writer.AppendLine($"      <id>{EscapeXml(Commander.NormalImageUrl)}</id>");
                writer.AppendLine($"      <slots>{slotIndex}</slots>");
                writer.AppendLine($"      <name>{EscapeXml(Commander.Name)}.png</name>");
                writer.AppendLine($"      <query>{EscapeXml(Commander.Name.ToLowerInvariant())}</query>");
                writer.AppendLine("    </card>");
                slotIndex++;
            }
            
            // Add all other cards in the deck
            foreach (var card in _deck.Cards)
            {
                var slots = new List<int>();
                for (int i = 0; i < card.Quantity; i++)
                {
                    slots.Add(slotIndex);
                    slotIndex++;
                }
                
                writer.AppendLine("    <card>");
                writer.AppendLine($"      <id>{EscapeXml(card.NormalImageUrl)}</id>");
                writer.AppendLine($"      <slots>{string.Join(",", slots)}</slots>");
                writer.AppendLine($"      <name>{EscapeXml(card.Name)}.png</name>");
                writer.AppendLine($"      <query>{EscapeXml(card.Name.ToLowerInvariant())}</query>");
                writer.AppendLine("    </card>");
            }
            writer.AppendLine("  </fronts>");
            
            writer.AppendLine("  <backs>");
            // Standard MTG card back
            var backSlots = new List<int>();
            for (int i = 0; i < DeckSize; i++)
            {
                backSlots.Add(i);
            }
            writer.AppendLine("    <card>");
            writer.AppendLine("      <id>https://i.imgur.com/P7qYTcI.png</id>"); // Standard high-quality MTG back
            writer.AppendLine($"      <slots>{string.Join(",", backSlots)}</slots>");
            writer.AppendLine("      <name>Card Back.png</name>");
            writer.AppendLine("      <query>card back</query>");
            writer.AppendLine("    </card>");
            writer.AppendLine("  </backs>");
            writer.AppendLine("</order>");
            
            return writer.ToString();
        }

        private string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("&", "&amp;")
                        .Replace("<", "&lt;")
                        .Replace(">", "&gt;")
                        .Replace("\"", "&quot;")
                        .Replace("'", "&apos;");
        }
    }

    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification = false;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
            {
                base.OnCollectionChanged(e);
            }
        }

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!_suppressNotification)
            {
                base.OnPropertyChanged(e);
            }
        }

        public void ReplaceRange(IEnumerable<T> collection)
        {
            _suppressNotification = true;
            try
            {
                Clear();
                foreach (var item in collection)
                {
                    Add(item);
                }
            }
            finally
            {
                _suppressNotification = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Count"));
                OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
            }
        }
    }

    public class DeckGroup
    {
        public string GroupName { get; set; } = string.Empty;
        public List<Card> Cards { get; set; } = new();
        public int Count => Cards.Sum(c => c.Quantity);
        public System.Windows.Media.Brush HeaderBrush { get; set; } = System.Windows.Media.Brushes.White;
    }

    public class ArchidektDeckDto
    {
        public string? Name { get; set; }
        public List<ArchidektCardEntry>? Cards { get; set; }
    }

    public class ArchidektCardEntry
    {
        public int Quantity { get; set; }
        public ArchidektCardDetail? Card { get; set; }
        public List<string>? Categories { get; set; }
    }

    public class ArchidektCardDetail
    {
        public ArchidektOracleCard? OracleCard { get; set; }
    }

    public class ArchidektOracleCard
    {
        public string? Name { get; set; }
    }
}
