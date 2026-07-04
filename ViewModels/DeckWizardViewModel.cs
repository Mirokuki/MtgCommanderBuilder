using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MtgCommanderBuilder.Models;
using MtgCommanderBuilder.Services;

namespace MtgCommanderBuilder.ViewModels
{
    public class DeckWizardViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;
        private readonly DeckStorageService _storageService;
        private readonly Action<Deck> _onDeckGenerated;

        private int _currentStep = 1;
        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (SetProperty(ref _currentStep, value))
                {
                    UpdateStepActiveStates();
                }
            }
        }

        // Step Visibility Triggers
        public bool Step1Active => CurrentStep == 1;
        public bool Step2Active => CurrentStep == 2;
        public bool Step3Active => CurrentStep == 3;
        public bool Step4Active => CurrentStep == 4;
        public bool Step5Active => CurrentStep == 5;
        public bool Step6Active => CurrentStep == 6;
        public bool Step7Active => CurrentStep == 7;

        private void UpdateStepActiveStates()
        {
            OnPropertyChanged(nameof(Step1Active));
            OnPropertyChanged(nameof(Step2Active));
            OnPropertyChanged(nameof(Step3Active));
            OnPropertyChanged(nameof(Step4Active));
            OnPropertyChanged(nameof(Step5Active));
            OnPropertyChanged(nameof(Step6Active));
            OnPropertyChanged(nameof(Step7Active));
        }

        // --- STEP 1: COMMANDER ---
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ExecuteCommanderSearch();
                }
            }
        }

        private Card? _selectedCommander;
        public Card? SelectedCommander
        {
            get => _selectedCommander;
            set => SetProperty(ref _selectedCommander, value);
        }

        public ObservableCollection<Card> SearchResults { get; } = new();

        private void ExecuteCommanderSearch()
        {
            SearchResults.Clear();
            if (string.IsNullOrWhiteSpace(SearchText) || !_dbService.IsLoaded)
                return;

            var matches = _dbService.Cards
                .Where(c => (c.TypeLine.Contains("Legendary", StringComparison.OrdinalIgnoreCase) && 
                             (c.TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase) || 
                              c.TypeLine.Contains("Planeswalker", StringComparison.OrdinalIgnoreCase)))
                             && c.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .Take(25)
                .ToList();

            foreach (var card in matches)
            {
                SearchResults.Add(card);
            }
        }

        // --- STEP 2: POWER LEVEL ---
        private string _powerLevel = "Casual (5-6)";
        public string PowerLevel
        {
            get => _powerLevel;
            set => SetProperty(ref _powerLevel, value);
        }

        public List<string> PowerLevels { get; } = new()
        {
            "Precon (3-4)",
            "Casual (5-6)",
            "Optimized (7-8)",
            "Competitive / cEDH (9-10)"
        };

        // --- STEP 3: STRATEGY ---
        private string _strategy = "Midrange / Value";
        public string Strategy
        {
            get => _strategy;
            set => SetProperty(ref _strategy, value);
        }

        public List<string> Strategies { get; } = new()
        {
            "Aggro / Tokens",
            "Midrange / Value",
            "Control / Stax",
            "Combo / Storm",
            "Voltron / Commander Damage",
            "Lifegain / Aristocrats",
            "Spellslinger / Burn"
        };

        // Choose Theme
        private string _theme = "Counters";
        public string Theme
        {
            get => _theme;
            set
            {
                if (SetProperty(ref _theme, value))
                {
                    OnPropertyChanged(nameof(ThemeCounters));
                    OnPropertyChanged(nameof(ThemeTokens));
                    OnPropertyChanged(nameof(ThemeAristocrats));
                    OnPropertyChanged(nameof(ThemeSpellslinger));
                }
            }
        }

        public bool ThemeCounters
        {
            get => Theme == "Counters";
            set { if (value) Theme = "Counters"; }
        }
        public bool ThemeTokens
        {
            get => Theme == "Tokens";
            set { if (value) Theme = "Tokens"; }
        }
        public bool ThemeAristocrats
        {
            get => Theme == "Aristocrats";
            set { if (value) Theme = "Aristocrats"; }
        }
        public bool ThemeSpellslinger
        {
            get => Theme == "Spellslinger";
            set { if (value) Theme = "Spellslinger"; }
        }

        // Power Level boolean wrappers
        public bool PowerCasual
        {
            get => PowerLevel.Contains("Casual") || PowerLevel.Contains("Precon");
            set { if (value) PowerLevel = "Casual (5-6)"; OnPowerChanged(); }
        }
        public bool PowerFocused
        {
            get => PowerLevel.Contains("Optimized") || PowerLevel.Contains("Focused");
            set { if (value) PowerLevel = "Optimized (7-8)"; OnPowerChanged(); }
        }
        public bool PowerHigh
        {
            get => PowerLevel.Contains("High Power");
            set { if (value) PowerLevel = "High Power"; OnPowerChanged(); }
        }
        public bool PowerCedh
        {
            get => PowerLevel.Contains("Competitive") || PowerLevel.Contains("cEDH");
            set { if (value) PowerLevel = "Competitive / cEDH (9-10)"; OnPowerChanged(); }
        }

        private void OnPowerChanged()
        {
            OnPropertyChanged(nameof(PowerCasual));
            OnPropertyChanged(nameof(PowerFocused));
            OnPropertyChanged(nameof(PowerHigh));
            OnPropertyChanged(nameof(PowerCedh));
        }

        // Strategy boolean wrappers
        public bool StrategyControl
        {
            get => Strategy.Contains("Control");
            set { if (value) Strategy = "Control / Stax"; OnStrategyChanged(); }
        }
        public bool StrategyCombo
        {
            get => Strategy.Contains("Combo");
            set { if (value) Strategy = "Combo / Storm"; OnStrategyChanged(); }
        }
        public bool StrategyMidrange
        {
            get => Strategy.Contains("Midrange");
            set { if (value) Strategy = "Midrange / Value"; OnStrategyChanged(); }
        }
        public bool StrategyAggro
        {
            get => Strategy.Contains("Aggro");
            set { if (value) Strategy = "Aggro / Tokens"; OnStrategyChanged(); }
        }
        public bool StrategyValue
        {
            get => Strategy.Contains("Value");
            set { if (value) Strategy = "Midrange / Value"; OnStrategyChanged(); }
        }
        public bool StrategyTribal
        {
            get => Strategy.Contains("Tribal");
            set { if (value) Strategy = "Tribal"; OnStrategyChanged(); }
        }

        private void OnStrategyChanged()
        {
            OnPropertyChanged(nameof(StrategyControl));
            OnPropertyChanged(nameof(StrategyCombo));
            OnPropertyChanged(nameof(StrategyMidrange));
            OnPropertyChanged(nameof(StrategyAggro));
            OnPropertyChanged(nameof(StrategyValue));
            OnPropertyChanged(nameof(StrategyTribal));
        }

        // --- STEP 4: COMPOSITION (Target Card Types, sums to 99) ---
        private int _landsCount = 36;
        public int LandsCount
        {
            get => _landsCount;
            set { if (SetProperty(ref _landsCount, value)) OnCompositionChanged(); }
        }

        private int _creaturesCount = 28;
        public int CreaturesCount
        {
            get => _creaturesCount;
            set { if (SetProperty(ref _creaturesCount, value)) OnCompositionChanged(); }
        }

        private int _artifactsCount = 8;
        public int ArtifactsCount
        {
            get => _artifactsCount;
            set { if (SetProperty(ref _artifactsCount, value)) OnCompositionChanged(); }
        }

        private int _enchantmentsCount = 6;
        public int EnchantmentsCount
        {
            get => _enchantmentsCount;
            set { if (SetProperty(ref _enchantmentsCount, value)) OnCompositionChanged(); }
        }

        private int _instantsCount = 12;
        public int InstantsCount
        {
            get => _instantsCount;
            set { if (SetProperty(ref _instantsCount, value)) OnCompositionChanged(); }
        }

        private int _sorceriesCount = 9;
        public int SorceriesCount
        {
            get => _sorceriesCount;
            set { if (SetProperty(ref _sorceriesCount, value)) OnCompositionChanged(); }
        }

        private int _planeswalkersCount = 0;
        public int PlaneswalkersCount
        {
            get => _planeswalkersCount;
            set { if (SetProperty(ref _planeswalkersCount, value)) OnCompositionChanged(); }
        }

        private int _battlesCount = 0;
        public int BattlesCount
        {
            get => _battlesCount;
            set { if (SetProperty(ref _battlesCount, value)) OnCompositionChanged(); }
        }

        public int TotalComposition => LandsCount + CreaturesCount + ArtifactsCount + EnchantmentsCount + InstantsCount + SorceriesCount + PlaneswalkersCount + BattlesCount;
        public bool IsCompositionValid => TotalComposition == 99;
        public bool IsCompositionInvalid => !IsCompositionValid;

        private void OnCompositionChanged()
        {
            OnPropertyChanged(nameof(TotalComposition));
            OnPropertyChanged(nameof(IsCompositionValid));
            OnPropertyChanged(nameof(IsCompositionInvalid));
        }

        public void ApplyRecommendedComposition()
        {
            LandsCount = 36;
            CreaturesCount = 28;
            ArtifactsCount = 8;
            EnchantmentsCount = 6;
            InstantsCount = 12;
            SorceriesCount = 9;
            PlaneswalkersCount = 0;
            BattlesCount = 0;
        }

        // --- STEP 5: FUNCTIONS / GOALS ---
        private int _rampGoal = 10;
        public int RampGoal
        {
            get => _rampGoal;
            set => SetProperty(ref _rampGoal, value);
        }

        private int _drawGoal = 10;
        public int DrawGoal
        {
            get => _drawGoal;
            set => SetProperty(ref _drawGoal, value);
        }

        private int _removalGoal = 8;
        public int RemovalGoal
        {
            get => _removalGoal;
            set => SetProperty(ref _removalGoal, value);
        }

        private int _wipeGoal = 3;
        public int WipeGoal
        {
            get => _wipeGoal;
            set => SetProperty(ref _wipeGoal, value);
        }

        private int _protectionGoal = 5;
        public int ProtectionGoal
        {
            get => _protectionGoal;
            set => SetProperty(ref _protectionGoal, value);
        }

        private int _recursionGoal = 4;
        public int RecursionGoal
        {
            get => _recursionGoal;
            set => SetProperty(ref _recursionGoal, value);
        }

        private int _tutorGoal = 2;
        public int TutorGoal
        {
            get => _tutorGoal;
            set => SetProperty(ref _tutorGoal, value);
        }

        private int _tokenGoal = 6;
        public int TokenGoal
        {
            get => _tokenGoal;
            set => SetProperty(ref _tokenGoal, value);
        }

        private int _finisherGoal = 4;
        public int FinisherGoal
        {
            get => _finisherGoal;
            set => SetProperty(ref _finisherGoal, value);
        }

        public void ResetGoals()
        {
            RampGoal = 10;
            DrawGoal = 10;
            RemovalGoal = 8;
            WipeGoal = 3;
            ProtectionGoal = 5;
            RecursionGoal = 4;
            TutorGoal = 2;
            TokenGoal = 6;
            FinisherGoal = 4;
        }

        // --- STEP 6: RESTRICTIONS ---
        private bool _allowProxies = true;
        public bool AllowProxies
        {
            get => _allowProxies;
            set => SetProperty(ref _allowProxies, value);
        }

        private bool _strictSingleton = true;
        public bool StrictSingleton
        {
            get => _strictSingleton;
            set => SetProperty(ref _strictSingleton, value);
        }

        private bool _colorIdentityRule = true;
        public bool ColorIdentityRule
        {
            get => _colorIdentityRule;
            set => SetProperty(ref _colorIdentityRule, value);
        }

        // --- COMMANDS ---
        public ICommand NextCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand GenerateCommand { get; }

        public DeckWizardViewModel(DatabaseService dbService, DeckStorageService storageService, Action<Deck> onDeckGenerated)
        {
            _dbService = dbService;
            _storageService = storageService;
            _onDeckGenerated = onDeckGenerated;

            NextCommand = new RelayCommand(NextStep, CanNextStep);
            BackCommand = new RelayCommand(BackStep, CanBackStep);
            ResetCommand = new RelayCommand(ResetWizard);
            GenerateCommand = new RelayCommand(GenerateDeck, CanGenerateDeck);
        }

        private bool CanNextStep()
        {
            if (CurrentStep == 1) return SelectedCommander != null;
            if (CurrentStep == 4) return IsCompositionValid;
            return CurrentStep < 7;
        }

        private void NextStep()
        {
            if (CanNextStep())
            {
                CurrentStep++;
            }
        }

        private bool CanBackStep()
        {
            return CurrentStep > 1;
        }

        private void BackStep()
        {
            if (CanBackStep())
            {
                CurrentStep--;
            }
        }

        private void ResetWizard()
        {
            CurrentStep = 1;
            SearchText = "";
            SelectedCommander = null;
            PowerLevel = "Casual (5-6)";
            Strategy = "Midrange / Value";
            ApplyRecommendedComposition();
            ResetGoals();
            AllowProxies = true;
            StrictSingleton = true;
            ColorIdentityRule = true;
        }

        private bool CanGenerateDeck()
        {
            return SelectedCommander != null && IsCompositionValid;
        }

        private void GenerateDeck()
        {
            if (!CanGenerateDeck()) return;

            // Create new deck with the selected commander
            var newDeck = new Deck
            {
                Name = $"{SelectedCommander.Name} - {Strategy} Deck",
                Commander = SelectedCommander
            };

            // Pre-populate lands count with basic lands based on commander color identity
            var colors = SelectedCommander.ColorIdentity;
            int landsToGenerate = LandsCount;
            
            if (colors.Count == 0) // Colorless
            {
                newDeck.Cards.Add(CreateBasicLand("Wastes", landsToGenerate));
            }
            else
            {
                int share = landsToGenerate / colors.Count;
                int remainder = landsToGenerate % colors.Count;

                for (int i = 0; i < colors.Count; i++)
                {
                    string landName = GetBasicLandForColor(colors[i]);
                    int qty = share + (i < remainder ? 1 : 0);
                    if (qty > 0)
                    {
                        newDeck.Cards.Add(CreateBasicLand(landName, qty));
                    }
                }
            }

            // Save deck to disk and trigger callback
            _storageService.SaveDeck(newDeck);
            _onDeckGenerated?.Invoke(newDeck);
        }

        private Card CreateBasicLand(string name, int qty)
        {
            // Find a basic land match in local database
            var match = _dbService.Cards.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var card = new Card
            {
                Name = name,
                Quantity = qty,
                TypeLine = "Basic Land"
            };

            if (match != null)
            {
                card.Set = match.Set;
                card.SetName = match.SetName;
                card.CollectorNumber = match.CollectorNumber;
                card.NormalImageUrl = match.NormalImageUrl;
                card.ArtCropImageUrl = match.ArtCropImageUrl;
                card.Cmc = 0;
            }

            return card;
        }

        private string GetBasicLandForColor(string color)
        {
            return color.ToUpper() switch
            {
                "W" => "Plains",
                "U" => "Island",
                "B" => "Swamp",
                "R" => "Mountain",
                "G" => "Forest",
                _ => "Wastes"
            };
        }
    }
}
