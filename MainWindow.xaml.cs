using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MtgCommanderBuilder.Models;
using MtgCommanderBuilder.ViewModels;
using MtgCommanderBuilder.Views;
using System.Text.Json;

namespace MtgCommanderBuilder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Card? _selectedCard;
        private bool _isImportMode;

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateNonLandCount();
            ApplyCommanderGlow();
            
            if (ViewModel?.ActiveDeckViewModel != null)
            {
                ViewModel.ActiveDeckViewModel.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(DeckViewModel.Cards) || ev.PropertyName == nameof(DeckViewModel.DeckSize))
                    {
                        UpdateNonLandCount();
                        RefreshInspectorButtons();
                    }
                    else if (ev.PropertyName == nameof(DeckViewModel.Commander))
                    {
                        ApplyCommanderGlow();
                        RefreshInspectorButtons();
                    }
                };
            }

            // Keep listeners hooked when the active deck shifts (new deck selected)
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(MainViewModel.SelectedDeck) || ev.PropertyName == nameof(MainViewModel.ActiveDeckViewModel))
                    {
                        UpdateNonLandCount();
                        ApplyCommanderGlow();
                        RefreshInspectorButtons();
                        
                        // Hook into the new deck view model
                        if (ViewModel.ActiveDeckViewModel != null)
                        {
                            ViewModel.ActiveDeckViewModel.PropertyChanged += (s2, ev2) =>
                            {
                                if (ev2.PropertyName == nameof(DeckViewModel.Cards) || ev2.PropertyName == nameof(DeckViewModel.DeckSize))
                                {
                                    UpdateNonLandCount();
                                    RefreshInspectorButtons();
                                }
                                else if (ev2.PropertyName == nameof(DeckViewModel.Commander))
                                {
                                    ApplyCommanderGlow();
                                    RefreshInspectorButtons();
                                }
                            };
                        }
                    }
                };
            }

            if (ViewModel?.ProxyPrinter != null)
            {
                ViewModel.ProxyPrinter.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(ProxyPrinterViewModel.PaperSize) ||
                        ev.PropertyName == nameof(ProxyPrinterViewModel.GuideStyle) ||
                        ev.PropertyName == nameof(ProxyPrinterViewModel.GuideColor) ||
                        ev.PropertyName == nameof(ProxyPrinterViewModel.UseBleedEdge) ||
                        ev.PropertyName == nameof(ProxyPrinterViewModel.ActivePageCards))
                    {
                        RefreshScreenPreviewCropMarks();
                    }
                };

                // Initial preview draw
                RefreshScreenPreviewCropMarks();
            }
        }

        private void UpdateNonLandCount()
        {
            // TxtNonLandCount is no longer in the layout
        }

        private void RefreshInspectorButtons()
        {
            if (_selectedCard == null || ViewModel?.ActiveDeckViewModel == null) return;

            var activeDeck = ViewModel.ActiveDeckViewModel;
            
            // Check if card is in deck (either as commander or in mainboard)
            bool isCommander = activeDeck.Commander != null && activeDeck.Commander.Name.Equals(_selectedCard.Name, StringComparison.OrdinalIgnoreCase);
            bool isInMainboard = activeDeck.ActiveDeck.Cards.Any(c => c.Name.Equals(_selectedCard.Name, StringComparison.OrdinalIgnoreCase));
            bool isInDeck = isCommander || isInMainboard;

            // Check if card can have multiples (basic land, unlimited copies text, or if singleton constraint is disabled)
            bool isBasicLand = _selectedCard.IsLand && _selectedCard.TypeLine.Contains("Basic", StringComparison.OrdinalIgnoreCase);
            bool isUnlimited = _selectedCard.OracleText != null && _selectedCard.OracleText.Contains("any number of cards named", StringComparison.OrdinalIgnoreCase);
            bool allowsMultiples = isBasicLand || isUnlimited || !activeDeck.EnforceSingletonLimit;

            // Determines visibility of "Add" and "Remove"
            bool showAdd = !isInDeck || allowsMultiples;
            bool showRemove = isInDeck;
            bool showSetCommander = _selectedCard.IsValidCommander;

            // Toggle visibility of standard buttons
            if (InspectorAddToDeckBtn != null)
                InspectorAddToDeckBtn.Visibility = showAdd ? Visibility.Visible : Visibility.Collapsed;
            if (InspectorRemoveFromDeckBtn != null)
                InspectorRemoveFromDeckBtn.Visibility = showRemove ? Visibility.Visible : Visibility.Collapsed;
            if (InspectorSetCommanderBtn != null)
                InspectorSetCommanderBtn.Visibility = showSetCommander ? Visibility.Visible : Visibility.Collapsed;

            // Update Columns in UniformGrid
            int visibleCount = 0;
            if (showAdd) visibleCount++;
            if (showRemove) visibleCount++;
            if (showSetCommander) visibleCount++;

            if (InspectorActionPanel != null)
            {
                InspectorActionPanel.Columns = visibleCount > 0 ? visibleCount : 1;
            }
        }

        public void SelectCardInInspector(Card card)
        {
            _selectedCard = card;
            SelectionPanel.DataContext = card;

            // Update Inspector details
            InspectorName.Text = card.Name;
            InspectorType.Text = card.TypeLine;
            InspectorText.Text = card.OracleText ?? "No rules text.";
            InspectorCmc.Text = card.Cmc.ToString();
            InspectorPrice.Text = !string.IsNullOrEmpty(card.PriceUsd) ? $"${card.PriceUsd}" : "N/A";

            // Sync categories from existing deck card/commander first to show in textbox
            if (ViewModel?.ActiveDeckViewModel != null)
            {
                var activeDeck = ViewModel.ActiveDeckViewModel;
                Card? existingInDeck = null;
                if (activeDeck.Commander != null && activeDeck.Commander.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase))
                {
                    existingInDeck = activeDeck.Commander;
                }
                else
                {
                    existingInDeck = activeDeck.ActiveDeck.Cards.FirstOrDefault(c => c.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase));
                }

                if (existingInDeck != null)
                {
                    card.Categories = new List<string>(existingInDeck.Categories);
                }
            }

            // Update custom categories text box
            CategoriesTextBox.Text = card.Categories != null ? string.Join(", ", card.Categories) : "";

            // Show panels
            NoSelectionPanel.Visibility = Visibility.Collapsed;
            SelectionPanel.Visibility = Visibility.Visible;

            // Asynchronously update image
            InspectorImage.Card = card;

            // Populate printings combo box
            if (ViewModel?.DbService != null)
            {
                var name = card.Name;
                var printings = ViewModel.DbService.Cards.Where(c => c.Name == name)
                                         .OrderBy(c => c.SetName)
                                         .ThenBy(c => c.CollectorNumber)
                                         .ToList();

                // Unhook selection changed event before modifying items
                PrintingsComboBox.SelectionChanged -= PrintingsComboBox_SelectionChanged;
                PrintingsComboBox.ItemsSource = printings;

                var active = printings.FirstOrDefault(p => p.Id == card.Id) ?? printings.FirstOrDefault();
                PrintingsComboBox.SelectedItem = active;

                PrintingsComboBox.SelectionChanged += PrintingsComboBox_SelectionChanged;
            }

            RefreshInspectorButtons();
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is Card card)
            {
                SelectCardInInspector(card);
            }
        }

        private void PrintingsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PrintingsComboBox.SelectedItem is Card card)
            {
                _selectedCard = card;
                SelectionPanel.DataContext = card;

                // Update Inspector details with the newly selected printing
                InspectorName.Text = card.Name;
                InspectorType.Text = card.TypeLine;
                InspectorText.Text = card.OracleText ?? "No rules text.";
                InspectorCmc.Text = card.Cmc.ToString();
                InspectorPrice.Text = !string.IsNullOrEmpty(card.PriceUsd) ? $"${card.PriceUsd}" : "N/A";

                // Update Inspector image
                InspectorImage.Card = card;

                // Real-time deck card art/printing syncing
                if (ViewModel?.ActiveDeckViewModel != null)
                {
                    var activeDeck = ViewModel.ActiveDeckViewModel;
                    bool changed = false;

                    // Sync categories from existing deck card/commander first
                    Card? existingInDeck = null;
                    if (activeDeck.Commander != null && activeDeck.Commander.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        existingInDeck = activeDeck.Commander;
                    }
                    else
                    {
                        existingInDeck = activeDeck.ActiveDeck.Cards.FirstOrDefault(c => c.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase));
                    }

                    if (existingInDeck != null)
                    {
                        card.Categories = new List<string>(existingInDeck.Categories);
                    }

                    // 1. If it's the commander, update the commander printing
                    if (activeDeck.Commander != null && activeDeck.Commander.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase) && activeDeck.Commander.Id != card.Id)
                    {
                        var cloned = activeDeck.CloneCard(card);
                        cloned.IsCommander = true;
                        cloned.Quantity = 1;
                        activeDeck.Commander = cloned;
                        changed = true;
                    }

                    // 2. If it's in the mainboard, update the mainboard printing
                    var existing = activeDeck.ActiveDeck.Cards.FirstOrDefault(c => c.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase));
                    if (existing != null && existing.Id != card.Id)
                    {
                        int index = activeDeck.ActiveDeck.Cards.IndexOf(existing);
                        if (index >= 0)
                        {
                            var cloned = activeDeck.CloneCard(card);
                            cloned.Quantity = existing.Quantity;
                            cloned.IsCommander = false;
                            activeDeck.ActiveDeck.Cards[index] = cloned;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        activeDeck.RefreshDeckState();
                        activeDeck.SaveDeck();
                    }
                }

                CategoriesTextBox.Text = card.Categories != null ? string.Join(", ", card.Categories) : "";

                RefreshInspectorButtons();
            }
        }

        private void AddToDeck_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCard != null)
            {
                CommitCategories();
                if (ViewModel?.CardSearchViewModel?.AddToDeckCommand.CanExecute(_selectedCard) == true)
                {
                    ViewModel.CardSearchViewModel.AddToDeckCommand.Execute(_selectedCard);
                    RefreshInspectorButtons();
                }
            }
        }

        private void CategoriesTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitCategories();
        }

        private void CategoriesTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                CommitCategories();
                e.Handled = true;
            }
        }

        private void CommitCategories()
        {
            if (_selectedCard == null || ViewModel?.ActiveDeckViewModel == null) return;

            var text = CategoriesTextBox.Text ?? "";
            var categories = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(t => t.Trim())
                                 .Where(t => !string.IsNullOrEmpty(t))
                                 .ToList();

            // Check if categories actually changed to avoid redundant saves/refreshes
            bool isSame = _selectedCard.Categories != null &&
                          _selectedCard.Categories.Count == categories.Count &&
                          _selectedCard.Categories.Zip(categories, (a, b) => a.Equals(b, StringComparison.OrdinalIgnoreCase)).All(x => x);

            if (isSame) return;

            _selectedCard.Categories = categories;

            var activeDeck = ViewModel.ActiveDeckViewModel;
            bool changed = false;

            if (activeDeck.Commander != null && activeDeck.Commander.Name.Equals(_selectedCard.Name, StringComparison.OrdinalIgnoreCase))
            {
                activeDeck.Commander.Categories = new List<string>(categories);
                changed = true;
            }

            var existing = activeDeck.ActiveDeck.Cards.FirstOrDefault(c => c.Name.Equals(_selectedCard.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Categories = new List<string>(categories);
                changed = true;
            }

            if (changed)
            {
                activeDeck.RefreshDeckState();
                activeDeck.SaveDeck();
            }
        }

        private void RemoveFromDeck_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCard != null && ViewModel?.ActiveDeckViewModel != null)
            {
                ViewModel.ActiveDeckViewModel.RemoveCard(_selectedCard);
                RefreshInspectorButtons();
            }
        }

        private void SetCommander_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCard != null && ViewModel?.CardSearchViewModel?.SetAsCommanderCommand.CanExecute(_selectedCard) == true)
            {
                ViewModel.CardSearchViewModel.SetAsCommanderCommand.Execute(_selectedCard);
                RefreshInspectorButtons();
            }
        }

        private void GridAddToDeck_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Card card)
            {
                if (ViewModel?.CardSearchViewModel?.AddToDeckCommand.CanExecute(card) == true)
                {
                    ViewModel.CardSearchViewModel.AddToDeckCommand.Execute(card);
                    RefreshInspectorButtons();
                }
            }
        }

        private void GridRemoveFromDeck_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Card card && ViewModel?.ActiveDeckViewModel != null)
            {
                ViewModel.ActiveDeckViewModel.RemoveCard(card);
                RefreshInspectorButtons();
            }
        }

        private void GridSetCommander_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Card card)
            {
                if (ViewModel?.CardSearchViewModel?.SetAsCommanderCommand.CanExecute(card) == true)
                {
                    ViewModel.CardSearchViewModel.SetAsCommanderCommand.Execute(card);
                    RefreshInspectorButtons();
                }
            }
        }

        private void AutoLands_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ActiveDeckViewModel == null) return;
            var activeDeckVm = ViewModel.ActiveDeckViewModel;
            if (activeDeckVm.Commander == null)
            {
                MessageBox.Show("Please select a Commander first to establish a Color Identity.", "Commander Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ViewModel.DbService == null || !ViewModel.DbService.IsLoaded)
            {
                MessageBox.Show("Card database must be loaded to suggest lands.", "Database Not Loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int currentSize = activeDeckVm.DeckSize;
            int neededLands = 100 - currentSize;

            if (neededLands <= 0)
            {
                MessageBox.Show("Your deck already has 100 or more cards!", "No Lands Needed", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var nonLands = activeDeckVm.Cards.Where(c => !c.IsLand).ToList();
            int wCount = 0, uCount = 0, bCount = 0, rCount = 0, gCount = 0;

            foreach (var card in nonLands)
            {
                if (string.IsNullOrEmpty(card.ManaCost)) continue;
                
                int qty = card.Quantity;
                wCount += CountOccurrences(card.ManaCost, "{W}") * qty;
                uCount += CountOccurrences(card.ManaCost, "{U}") * qty;
                bCount += CountOccurrences(card.ManaCost, "{B}") * qty;
                rCount += CountOccurrences(card.ManaCost, "{R}") * qty;
                gCount += CountOccurrences(card.ManaCost, "{G}") * qty;
            }

            if (!string.IsNullOrEmpty(activeDeckVm.Commander.ManaCost))
            {
                wCount += CountOccurrences(activeDeckVm.Commander.ManaCost, "{W}");
                uCount += CountOccurrences(activeDeckVm.Commander.ManaCost, "{U}");
                bCount += CountOccurrences(activeDeckVm.Commander.ManaCost, "{B}");
                rCount += CountOccurrences(activeDeckVm.Commander.ManaCost, "{R}");
                gCount += CountOccurrences(activeDeckVm.Commander.ManaCost, "{G}");
            }

            var identity = activeDeckVm.Commander.ColorIdentity;
            
            if (!identity.Contains("W")) wCount = 0;
            if (!identity.Contains("U")) uCount = 0;
            if (!identity.Contains("B")) bCount = 0;
            if (!identity.Contains("R")) rCount = 0;
            if (!identity.Contains("G")) gCount = 0;

            int totalSymbols = wCount + uCount + bCount + rCount + gCount;

            var db = ViewModel.DbService.Cards;
            var plainsCard = db.FirstOrDefault(c => c.Name.Equals("Plains", StringComparison.OrdinalIgnoreCase));
            var islandCard = db.FirstOrDefault(c => c.Name.Equals("Island", StringComparison.OrdinalIgnoreCase));
            var swampCard = db.FirstOrDefault(c => c.Name.Equals("Swamp", StringComparison.OrdinalIgnoreCase));
            var mountainCard = db.FirstOrDefault(c => c.Name.Equals("Mountain", StringComparison.OrdinalIgnoreCase));
            var forestCard = db.FirstOrDefault(c => c.Name.Equals("Forest", StringComparison.OrdinalIgnoreCase));

            if (plainsCard == null || islandCard == null || swampCard == null || mountainCard == null || forestCard == null)
            {
                MessageBox.Show("Error finding basic lands in offline database.", "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int wLands = 0, uLands = 0, bLands = 0, rLands = 0, gLands = 0;

            if (totalSymbols == 0)
            {
                int activeColors = identity.Count;
                if (activeColors == 0)
                {
                    var wastes = db.FirstOrDefault(c => c.Name.Equals("Wastes", StringComparison.OrdinalIgnoreCase)) ?? plainsCard;
                    AddLandsToDeck(wastes, neededLands);
                    return;
                }

                int baseSplit = neededLands / activeColors;
                int remainder = neededLands % activeColors;

                if (identity.Contains("W")) wLands = baseSplit;
                if (identity.Contains("U")) uLands = baseSplit;
                if (identity.Contains("B")) bLands = baseSplit;
                if (identity.Contains("R")) rLands = baseSplit;
                if (identity.Contains("G")) gLands = baseSplit;

                if (remainder > 0)
                {
                    string first = identity[0];
                    if (first == "W") wLands += remainder;
                    else if (first == "U") uLands += remainder;
                    else if (first == "B") bLands += remainder;
                    else if (first == "R") rLands += remainder;
                    else if (first == "G") gLands += remainder;
                }
            }
            else
            {
                double wRatio = (double)wCount / totalSymbols;
                double uRatio = (double)uCount / totalSymbols;
                double bRatio = (double)bCount / totalSymbols;
                double rRatio = (double)rCount / totalSymbols;
                double gRatio = (double)gCount / totalSymbols;

                wLands = (int)Math.Round(wRatio * neededLands);
                uLands = (int)Math.Round(uRatio * neededLands);
                bLands = (int)Math.Round(bRatio * neededLands);
                rLands = (int)Math.Round(rRatio * neededLands);
                gLands = (int)Math.Round(gRatio * neededLands);

                int sum = wLands + uLands + bLands + rLands + gLands;
                int diff = neededLands - sum;

                if (diff != 0)
                {
                    var list = new List<(string Color, int Count)>
                    {
                        ("W", wCount), ("U", uCount), ("B", bCount), ("R", rCount), ("G", gCount)
                    }.OrderByDescending(x => x.Count).ToList();

                    string targetColor = list[0].Color;
                    if (targetColor == "W") wLands += diff;
                    else if (targetColor == "U") uLands += diff;
                    else if (targetColor == "B") bLands += diff;
                    else if (targetColor == "R") rLands += diff;
                    else if (targetColor == "G") gLands += diff;
                }
            }

            if (wLands > 0) AddLandsToDeck(plainsCard, wLands);
            if (uLands > 0) AddLandsToDeck(islandCard, uLands);
            if (bLands > 0) AddLandsToDeck(swampCard, bLands);
            if (rLands > 0) AddLandsToDeck(mountainCard, rLands);
            if (gLands > 0) AddLandsToDeck(forestCard, gLands);

            activeDeckVm.RefreshDeckState();
            activeDeckVm.SaveDeck();

            MessageBox.Show($"Successfully filled your deck with basic lands:\n" +
                            $"- {wLands} Plains\n" +
                            $"- {uLands} Islands\n" +
                            $"- {bLands} Swamps\n" +
                            $"- {rLands} Mountains\n" +
                            $"- {gLands} Forests", 
                            "Lands Suggestions Applied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddLandsToDeck(Card landCard, int count)
        {
            if (ViewModel?.ActiveDeckViewModel == null) return;
            for (int i = 0; i < count; i++)
            {
                ViewModel.ActiveDeckViewModel.AddCard(landCard, false, true);
            }
        }

        private void ClearLands_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ActiveDeckViewModel == null) return;
            var activeDeckVm = ViewModel.ActiveDeckViewModel;

            var excludedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Plains", "Island", "Swamp", "Mountain", "Forest", "Wastes",
                "Snow-Covered Plains", "Snow-Covered Island", "Snow-Covered Swamp", "Snow-Covered Mountain", "Snow-Covered Forest"
            };

            int initialCount = activeDeckVm.ActiveDeck.Cards.Count;
            activeDeckVm.ActiveDeck.Cards.RemoveAll(c => excludedNames.Contains(c.Name));

            if (activeDeckVm.ActiveDeck.Cards.Count != initialCount)
            {
                activeDeckVm.RefreshDeckState();
                activeDeckVm.SaveDeck();
                MessageBox.Show("Successfully removed all basic lands from your deck!", "Lands Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("No basic lands found in your deck to remove.", "Lands Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }

        private void ApplyCommanderGlow()
        {
            // CommanderBorder is no longer in the layout
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            _isImportMode = true;
            DialogTitle.Text = "Import Deck list";
            DialogTextBox.Text = string.Empty;
            DialogTextBox.IsReadOnly = false;
            DialogActionBtn.Content = "Import";
            DialogOverlay.Visibility = Visibility.Visible;
        }

        private void ImportFile_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            if (ViewModel.DbService == null || !ViewModel.DbService.IsLoaded)
            {
                MessageBox.Show("Error: Card database is not loaded yet. Please wait.", "Database Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Deck Files (*.txt;*.dec;*.csv;*.json)|*.txt;*.dec;*.csv;*.json|Text Files (*.txt)|*.txt|DEC Files (*.dec)|*.dec|CSV Files (*.csv)|*.csv|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Import Deck File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string filePath = openFileDialog.FileName;
                    string fileContent = System.IO.File.ReadAllText(filePath);
                    string extension = System.IO.Path.GetExtension(filePath).ToLower();

                    // If it is JSON, it could be a native serialized Deck format or a Scryfall/other format
                    if (extension == ".json")
                    {
                        try
                        {
                            var options = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };
                            var importedDeck = JsonSerializer.Deserialize<Deck>(fileContent, options);

                            // Check if standard import has cards
                            if (importedDeck != null && (importedDeck.Cards != null || importedDeck.Commander != null) && (importedDeck.Cards.Count > 0 || importedDeck.Commander != null))
                            {
                                // Generate a new ID so it doesn't overwrite any existing deck of the same ID
                                importedDeck.Id = Guid.NewGuid();
                                importedDeck.Cards ??= new List<Card>();
                                if (string.IsNullOrWhiteSpace(importedDeck.Name) || importedDeck.Name == "New Commander Deck")
                                {
                                    importedDeck.Name = System.IO.Path.GetFileNameWithoutExtension(filePath);
                                }

                                // Repair Scryfall DTO information if fields are missing or sets aren't populated
                                if (ViewModel.DbService.IsLoaded)
                                {
                                    if (importedDeck.Commander != null)
                                    {
                                        var match = ViewModel.DbService.Cards.FirstOrDefault(c => c.Name.Equals(importedDeck.Commander.Name, StringComparison.OrdinalIgnoreCase));
                                        if (match != null)
                                        {
                                            importedDeck.Commander.NormalImageUrl = match.NormalImageUrl;
                                            importedDeck.Commander.ArtCropImageUrl = match.ArtCropImageUrl;
                                            importedDeck.Commander.Set = match.Set;
                                            importedDeck.Commander.SetName = match.SetName;
                                            importedDeck.Commander.CollectorNumber = match.CollectorNumber;
                                            importedDeck.Commander.PriceUsd = match.PriceUsd;
                                            importedDeck.Commander.Cmc = match.Cmc;
                                            importedDeck.Commander.TypeLine = match.TypeLine;
                                            importedDeck.Commander.OracleText = match.OracleText;
                                            importedDeck.Commander.Colors = match.Colors;
                                            importedDeck.Commander.ColorIdentity = match.ColorIdentity;
                                        }
                                    }

                                    foreach (var card in importedDeck.Cards)
                                    {
                                        var match = ViewModel.DbService.Cards.FirstOrDefault(c => c.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase));
                                        if (match != null)
                                        {
                                            card.NormalImageUrl = match.NormalImageUrl;
                                            card.ArtCropImageUrl = match.ArtCropImageUrl;
                                            card.Set = match.Set;
                                            card.SetName = match.SetName;
                                            card.CollectorNumber = match.CollectorNumber;
                                            card.PriceUsd = match.PriceUsd;
                                            card.Cmc = match.Cmc;
                                            card.TypeLine = match.TypeLine;
                                            card.OracleText = match.OracleText;
                                            card.Colors = match.Colors;
                                            card.ColorIdentity = match.ColorIdentity;
                                        }
                                    }
                                }

                                ViewModel.StorageService.SaveDeck(importedDeck);
                                ViewModel.SavedDecks.Insert(0, importedDeck);
                                ViewModel.SelectedDeck = importedDeck;

                                MessageBox.Show($"Successfully imported JSON deck: \"{importedDeck.Name}\"!", "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                                return;
                            }
                            else
                            {
                                // Attempt to parse as an Archidekt JSON deck file
                                Deck? archidektImportedDeck = null;
                                try
                                {
                                    var archidektDeck = JsonSerializer.Deserialize<MtgCommanderBuilder.ViewModels.ArchidektDeckDto>(fileContent, options);
                                    if (archidektDeck != null && archidektDeck.Cards != null && archidektDeck.Cards.Count > 0)
                                    {
                                        archidektImportedDeck = new Deck
                                        {
                                            Id = Guid.NewGuid(),
                                            Name = !string.IsNullOrWhiteSpace(archidektDeck.Name) ? archidektDeck.Name : System.IO.Path.GetFileNameWithoutExtension(filePath),
                                            Cards = new List<Card>()
                                        };

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

                                            if (rawCats.Any(c => c.Equals("Maybeboard", StringComparison.OrdinalIgnoreCase)))
                                                continue;

                                            bool setAsCommander = rawCats.Any(c => c.Equals("Commander", StringComparison.OrdinalIgnoreCase));
                                            var customCats = rawCats.Where(c => !defaultCategories.Contains(c)).ToList();

                                            var match = ViewModel.DbService.Cards.FirstOrDefault(c => c.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase));
                                            if (match == null)
                                            {
                                                match = ViewModel.DbService.Cards.FirstOrDefault(c => c.Name.StartsWith(cardName, StringComparison.OrdinalIgnoreCase));
                                            }

                                            if (match != null)
                                            {
                                                var newCard = new Card
                                                {
                                                    Id = match.Id,
                                                    Name = match.Name,
                                                    ManaCost = match.ManaCost,
                                                    Cmc = match.Cmc,
                                                    TypeLine = match.TypeLine,
                                                    OracleText = match.OracleText,
                                                    Colors = match.Colors != null ? new List<string>(match.Colors) : new List<string>(),
                                                    ColorIdentity = match.ColorIdentity != null ? new List<string>(match.ColorIdentity) : new List<string>(),
                                                    Rarity = match.Rarity,
                                                    NormalImageUrl = match.NormalImageUrl,
                                                    ArtCropImageUrl = match.ArtCropImageUrl,
                                                    PriceUsd = match.PriceUsd,
                                                    Set = match.Set,
                                                    SetName = match.SetName,
                                                    CollectorNumber = match.CollectorNumber,
                                                    Quantity = qty,
                                                    Categories = new List<string>(customCats)
                                                };

                                                if (setAsCommander)
                                                {
                                                    newCard.IsCommander = true;
                                                    newCard.Quantity = 1;
                                                    archidektImportedDeck.Commander = newCard;
                                                }
                                                else
                                                {
                                                    var existing = archidektImportedDeck.Cards.FirstOrDefault(c => c.Name.Equals(newCard.Name, StringComparison.OrdinalIgnoreCase));
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
                                                        archidektImportedDeck.Cards.Add(newCard);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // Fail silently to fall through to text import
                                }

                                if (archidektImportedDeck != null && (archidektImportedDeck.Cards.Count > 0 || archidektImportedDeck.Commander != null))
                                {
                                    ViewModel.StorageService.SaveDeck(archidektImportedDeck);
                                    ViewModel.SavedDecks.Insert(0, archidektImportedDeck);
                                    ViewModel.SelectedDeck = archidektImportedDeck;

                                    MessageBox.Show($"Successfully imported Archidekt JSON deck: \"{archidektImportedDeck.Name}\"!", "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                                    return;
                                }
                            }
                        }
                        catch
                        {
                            // If deserialization fails, we fall back to reading it as text
                        }
                    }

                    // Fallback to text importing (standard card list format)
                    var deckName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                    var newDeck = new Deck { Name = deckName };
                    
                    ViewModel.StorageService.SaveDeck(newDeck);
                    ViewModel.SavedDecks.Insert(0, newDeck);
                    ViewModel.SelectedDeck = newDeck;

                    ViewModel.ActiveDeckViewModel.ImportFromText(fileContent, ViewModel.DbService.Cards);
                    MessageBox.Show($"Successfully imported text deck list as: \"{deckName}\"!", "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import deck file: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ActiveDeckViewModel == null) return;
            _isImportMode = false;
            DialogTitle.Text = "Export Deck list";
            DialogTextBox.Text = ViewModel.ActiveDeckViewModel.ExportToText();
            DialogTextBox.IsReadOnly = true;
            DialogActionBtn.Content = "Copy to Clipboard";
            DialogOverlay.Visibility = Visibility.Visible;
        }

        private void ExportMoxfield_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ActiveDeckViewModel == null) return;
            _isImportMode = false;
            DialogTitle.Text = "Export Deck with Sets (Moxfield)";
            DialogTextBox.Text = ViewModel.ActiveDeckViewModel.ExportToMoxfieldText();
            DialogTextBox.IsReadOnly = true;
            DialogActionBtn.Content = "Copy to Clipboard";
            DialogOverlay.Visibility = Visibility.Visible;
        }

        private void ExportMpcXml_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ActiveDeckViewModel == null) return;

            var activeDeckVm = ViewModel.ActiveDeckViewModel;
            if (activeDeckVm.DeckSize == 0)
            {
                MessageBox.Show("Your deck is currently empty! Add some cards before exporting.", "Empty Deck", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                FileName = "cards.xml",
                Title = "Export MPC XML"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var xmlContent = activeDeckVm.ExportToMpcXml();
                    System.IO.File.WriteAllText(saveFileDialog.FileName, xmlContent, System.Text.Encoding.UTF8);
                    MessageBox.Show("MPC XML export completed successfully!", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export MPC XML: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DialogCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogOverlay.Visibility = Visibility.Collapsed;
        }

        private async void DialogAction_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ActiveDeckViewModel == null) return;

            if (_isImportMode)
            {
                if (ViewModel.DbService != null && ViewModel.DbService.IsLoaded)
                {
                    string text = DialogTextBox.Text.Trim();
                    
                    // Match Archidekt URL or numeric ID
                    var archidektRegex = new System.Text.RegularExpressions.Regex(@"^(?:https?://(?:www\.)?archidekt\.com/decks/)?(\d+)(?:/.*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var match = archidektRegex.Match(text);
                    if (match.Success)
                    {
                        string deckId = match.Groups[1].Value;
                        DialogOverlay.Visibility = Visibility.Collapsed;
                        
                        var originalCursor = this.Cursor;
                        try
                        {
                            this.Cursor = System.Windows.Input.Cursors.Wait;
                            await ViewModel.ActiveDeckViewModel.ImportFromArchidektAsync(deckId, ViewModel.DbService.Cards);
                            MessageBox.Show("Successfully imported deck and custom categories from Archidekt!", "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to import from Archidekt: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        finally
                        {
                            this.Cursor = originalCursor;
                        }
                    }
                    else
                    {
                        ViewModel.ActiveDeckViewModel.ImportFromText(text, ViewModel.DbService.Cards);
                        DialogOverlay.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    MessageBox.Show("Error: Card database is not loaded yet. Please wait.", "Database error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    DialogOverlay.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Clipboard.SetText(DialogTextBox.Text);
                MessageBox.Show("Deck list copied to clipboard!", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogOverlay.Visibility = Visibility.Collapsed;
            }
        }



        private void CreateCustomStaplesTab_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.CardSearchViewModel == null) return;
            var dialog = new MtgCommanderBuilder.Views.InputDialog("Enter the name of the new staples tab:", "Create Custom Staples Tab", "My Custom Staples")
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                string name = dialog.Answer;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    ViewModel.CardSearchViewModel.CreateCustomStaplesTab(name);
                }
            }
        }

        private void RenameCustomStaplesTab_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.CardSearchViewModel == null) return;
            var searchVm = ViewModel.CardSearchViewModel;
            if (!searchVm.IsCustomTabSelected) return;

            var dialog = new MtgCommanderBuilder.Views.InputDialog("Enter the new name of this staples tab:", "Rename Custom Staples Tab", searchVm.SelectedStaplesCategory)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                string newName = dialog.Answer;
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    searchVm.RenameCurrentStaplesTab(newName);
                }
            }
        }

        private void DeleteCustomStaplesTab_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.CardSearchViewModel == null) return;
            ViewModel.CardSearchViewModel.DeleteCurrentStaplesTab();
        }

        private void AddSelectedToStaples_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.CardSearchViewModel == null || _selectedCard == null) return;
            ViewModel.CardSearchViewModel.AddCardToCurrentCustomTab(_selectedCard);
        }

        private void RemoveFromCustomStaples_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.CardSearchViewModel == null) return;
            if (sender is Button btn && btn.DataContext is Card card)
            {
                ViewModel.CardSearchViewModel.RemoveCardFromCurrentCustomTab(card);
            }
        }

        private void SyntaxGuide_Click(object sender, RoutedEventArgs e)
        {
            var guide = new MtgCommanderBuilder.Views.SyntaxGuideWindow
            {
                Owner = this
            };
            guide.Show();
        }

        private void CardGridItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;

            if (sender is ListBoxItem item && item.DataContext is Card card)
            {
                if (ViewModel?.CardSearchViewModel?.AddToDeckCommand.CanExecute(card) == true)
                {
                    ViewModel.CardSearchViewModel.AddToDeckCommand.Execute(card);
                    RefreshInspectorButtons();
                }
                e.Handled = true;
            }
        }

        private void CardGridItem_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is Card card)
            {
                if (ViewModel?.ActiveDeckViewModel != null)
                {
                    ViewModel.ActiveDeckViewModel.RemoveCard(card);
                    RefreshInspectorButtons();
                }
                e.Handled = true;
            }
        }

        private async void PrintProxyDeck_Click(object sender, RoutedEventArgs e)
        {
            var mainVm = ViewModel;
            if (mainVm?.ProxyPrinter == null) return;
            var vm = mainVm.ProxyPrinter;

            var selectedCopies = vm.GetSelectedCopies();
            if (selectedCopies.Count == 0)
            {
                MessageBox.Show("Please select at least one card to print!", "No Cards Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. Download and Cache missing images first to prevent blank prints
            mainVm.StatusText = "Preparing card art for printing (caching images)...";
            mainVm.IsDownloading = true;

            try
            {
                var cache = mainVm.ImageCache;
                int count = 0;
                foreach (var card in selectedCopies)
                {
                    string remoteUrl = !string.IsNullOrEmpty(card.NormalImageUrl) 
                        ? card.NormalImageUrl 
                        : card.ArtCropImageUrl;

                    if (!string.IsNullOrEmpty(remoteUrl))
                    {
                        mainVm.StatusText = $"Caching card art ({++count} / {selectedCopies.Count}): {card.Name}...";
                        await cache.GetImageAsync(card.Id, remoteUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error caching card art: {ex.Message}. Some cards may print as blank placeholders.", "Cache Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                mainVm.IsDownloading = false;
                mainVm.StatusText = "Card art ready!";
            }

            // 2. Trigger WPF Print Dialog
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                mainVm.StatusText = "Generating print layout document...";
                try
                {
                    // Compute exact paper size (96 DPI standard)
                    double pageWidth = 8.5 * 96.0; // Letter Width (816 units)
                    double pageHeight = 11.0 * 96.0; // Letter Height (1056 units)

                    if (vm.PaperSize == "A4")
                    {
                        pageWidth = (210.0 / 25.4) * 96.0; // A4 Width exactly 210mm (approx 793.70 units)
                        pageHeight = (297.0 / 25.4) * 96.0; // A4 Height exactly 297mm (approx 1122.83 units)
                    }

                    // Card physical scale is exactly 63mm x 88mm
                    // At 96 DPI: 1 inch = 96 units, 1 mm = 96 / 25.4 units
                    double cardW = vm.CardW;
                    double cardH = vm.CardH;
                    double bleed = vm.Bleed;
                    double gutter = vm.Gutter;

                    bool isCricut = vm.LayoutMode == "Cricut (6 Cards)";

                    double gridW = vm.GridW;
                    double gridH = vm.GridH;

                    // Grid margins
                    double startX = vm.StartX;
                    double startY = vm.StartY;

                    Brush guideBrush = vm.GuideColorBrush;

                    var fixedDoc = new FixedDocument();

                    int cardsPerPage = isCricut ? 6 : 9;
                    int pageCount = (selectedCopies.Count + cardsPerPage - 1) / cardsPerPage;

                    for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                    {
                        var pageCards = selectedCopies.Skip(pageIndex * cardsPerPage).Take(cardsPerPage).ToList();

                        var fixedPage = new FixedPage
                        {
                            Width = pageWidth,
                            Height = pageHeight
                        };

                        var canvas = new Canvas
                        {
                            Width = pageWidth,
                            Height = pageHeight,
                            Background = Brushes.Transparent
                        };

                        fixedPage.Children.Add(canvas);

                        // Draw card grid
                        for (int i = 0; i < pageCards.Count; i++)
                        {
                            var card = pageCards[i];
                            int col = isCricut ? (i % 2) : (i % 3);
                            int row = isCricut ? (i / 2) : (i / 3);

                            double x = col == 0 ? vm.Col0X : (col == 1 ? vm.Col1X : vm.Col2X);
                            double y = row == 0 ? vm.Row0Y : (row == 1 ? vm.Row1Y : vm.Row2Y);

                            // Expanded Card Container (with bleed, solid black background to mask clipped scan corners)
                            var cardContainer = new Border
                            {
                                Width = cardW + 2 * bleed,
                                Height = cardH + 2 * bleed,
                                Background = Brushes.Black,
                                SnapsToDevicePixels = true,
                                BorderBrush = Brushes.Transparent,
                                BorderThickness = new Thickness(0)
                            };

                            if (isCricut)
                            {
                                cardContainer.LayoutTransform = new RotateTransform(90);
                            }

                            // Load image source
                            ImageSource? imgSource = null;

                            string localPath = System.IO.Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                "MtgCommanderBuilder", "Cache", "Images", $"{card.Id}_large.jpg");

                            if (File.Exists(localPath))
                            {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.UriSource = new Uri(System.IO.Path.GetFullPath(localPath));
                                bitmap.EndInit();
                                bitmap.Freeze();
                                imgSource = bitmap;
                            }
                            else if (!string.IsNullOrEmpty(card.NormalImageUrl))
                            {
                                imgSource = new BitmapImage(new Uri(card.NormalImageUrl));
                            }

                            // Stretch outer pixel borders outwards for bleed edge (mathematical radial projection bleed)
                            var bitmapSource = imgSource as BitmapSource;
                            if (bleed > 0 && bitmapSource != null && bitmapSource.PixelWidth > 10 && bitmapSource.PixelHeight > 10)
                            {
                                var bleedBitmap = MtgCommanderBuilder.Views.CardImagePresenter.CreateBleedBitmap(bitmapSource, bleed, cardW, cardH);
                                var cardImg = new Image
                                {
                                    Width = cardW + 2 * bleed,
                                    Height = cardH + 2 * bleed,
                                    Source = bleedBitmap,
                                    Stretch = Stretch.Fill
                                };
                                cardContainer.Child = cardImg;
                            }
                            else
                            {
                                if (bitmapSource != null && bitmapSource.PixelWidth > 10 && bitmapSource.PixelHeight > 10)
                                {
                                    var normalGrid = new Grid
                                    {
                                        Width = cardW,
                                        Height = cardH
                                    };

                                    int pw = bitmapSource.PixelWidth;
                                    int ph = bitmapSource.PixelHeight;
                                    int ox = Math.Max(1, (int)(pw * 0.03));
                                    int oy = Math.Max(1, (int)(ph * 0.03));

                                    // Extract top-left corner pixel to fill background with card's border color
                                    try
                                    {
                                        var tlCrop = new CroppedBitmap(bitmapSource, new Int32Rect(ox, oy, 1, 1));
                                        var bgImg = new Image { Source = tlCrop, Stretch = Stretch.Fill };
                                        normalGrid.Children.Add(bgImg);
                                    }
                                    catch { }

                                    double radius = (3.3 / 25.4) * 96.0; // exactly 3.3mm radius (approx 12.47 units)
                                    var cardImg = new Image
                                    {
                                        Source = bitmapSource,
                                        Stretch = Stretch.Fill,
                                        Clip = new RectangleGeometry(new Rect(0, 0, cardW, cardH), radius, radius)
                                    };
                                    normalGrid.Children.Add(cardImg);

                                    cardContainer.Child = normalGrid;
                                }
                                else
                                {
                                    // Single image representing the card, standard size fallback
                                    var cardImg = new Image
                                    {
                                        Width = cardW + 2 * bleed,
                                        Height = cardH + 2 * bleed,
                                        Stretch = Stretch.Fill,
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        VerticalAlignment = VerticalAlignment.Center,
                                        Source = imgSource
                                    };
                                    cardContainer.Child = cardImg;
                                }
                            }

                            // Position expanded card container (shifted back by bleed units)
                            Canvas.SetLeft(cardContainer, x - bleed);
                            Canvas.SetTop(cardContainer, y - bleed);
                            canvas.Children.Add(cardContainer);

                            // Guideline Borders overlay (exactly at cut boundary)
                            if (vm.GuideStyle == "Pure Black Borders")
                            {
                                var guideOverlay = new Border
                                {
                                    Width = cardW,
                                    Height = cardH,
                                    BorderBrush = guideBrush,
                                    BorderThickness = new Thickness(0.5),
                                    IsHitTestVisible = false
                                };
                                if (isCricut)
                                {
                                    guideOverlay.LayoutTransform = new RotateTransform(90);
                                }
                                Canvas.SetLeft(guideOverlay, x);
                                Canvas.SetTop(guideOverlay, y);
                                canvas.Children.Add(guideOverlay);
                            }
                            else if (vm.GuideStyle == "Dashed Margins")
                            {
                                var rect = new Rectangle
                                {
                                    Width = cardW,
                                    Height = cardH,
                                    Stroke = guideBrush,
                                    StrokeThickness = 0.5,
                                    StrokeDashArray = new DoubleCollection(new double[] { 4, 4 }),
                                    IsHitTestVisible = false
                                };
                                if (isCricut)
                                {
                                    rect.LayoutTransform = new RotateTransform(90);
                                }
                                Canvas.SetLeft(rect, x);
                                Canvas.SetTop(rect, y);
                                canvas.Children.Add(rect);
                            }
                        }

                        // Crop Marks Style (Disabled in Cricut mode)
                        if (vm.GuideStyle == "Crop Marks" && !isCricut)
                        {
                            var vCuts = new List<double>();
                            if (gutter > 0)
                            {
                                vCuts.Add(startX);
                                vCuts.Add(startX + cardW);
                                vCuts.Add(startX + cardW + gutter);
                                vCuts.Add(startX + 2 * cardW + gutter);
                                vCuts.Add(startX + 2 * cardW + 2 * gutter);
                                vCuts.Add(startX + 3 * cardW + 2 * gutter);
                            }
                            else
                            {
                                vCuts.Add(startX);
                                vCuts.Add(startX + cardW);
                                vCuts.Add(startX + 2 * cardW);
                                vCuts.Add(startX + 3 * cardW);
                            }

                            var hCuts = new List<double>();
                            if (gutter > 0)
                            {
                                hCuts.Add(startY);
                                hCuts.Add(startY + cardH);
                                hCuts.Add(startY + cardH + gutter);
                                hCuts.Add(startY + 2 * cardH + gutter);
                                hCuts.Add(startY + 2 * cardH + 2 * gutter);
                                hCuts.Add(startY + 3 * cardH + 2 * gutter);
                            }
                            else
                            {
                                hCuts.Add(startY);
                                hCuts.Add(startY + cardH);
                                hCuts.Add(startY + 2 * cardH);
                                hCuts.Add(startY + 3 * cardH);
                            }

                            // 1. Draw vertical crop mark lines (guides running top to bottom in margins)
                            foreach (var cx in vCuts)
                            {
                                DrawCropMark(canvas, cx, 0, cx, startY + 12, guideBrush);
                                DrawCropMark(canvas, cx, startY + gridH - 12, cx, pageHeight, guideBrush);
                            }

                            // 2. Draw horizontal crop mark lines (guides running left to right in margins)
                            foreach (var cy in hCuts)
                            {
                                DrawCropMark(canvas, 0, cy, startX, cy, guideBrush);
                                DrawCropMark(canvas, startX + gridW, cy, pageWidth, cy, guideBrush);
                            }

                            // 4. Draw internal inward-pointing green corner L-ticks at every card's 4 corners (always)
                            Brush greenBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0));
                            greenBrush.Freeze();

                            for (int i = 0; i < pageCards.Count; i++)
                            {
                                int col = i % 3;
                                int row = i / 3;
                                double cx = startX + col * (cardW + gutter);
                                double cy = startY + row * (cardH + gutter);

                                // Top-Left
                                DrawCropMark(canvas, cx, cy, cx + 6, cy, greenBrush);
                                DrawCropMark(canvas, cx, cy, cx, cy + 6, greenBrush);

                                // Top-Right
                                DrawCropMark(canvas, cx + cardW, cy, cx + cardW - 6, cy, greenBrush);
                                DrawCropMark(canvas, cx + cardW, cy, cx + cardW, cy + 6, greenBrush);

                                // Bottom-Left
                                DrawCropMark(canvas, cx, cy + cardH, cx + 6, cy + cardH, greenBrush);
                                DrawCropMark(canvas, cx, cy + cardH, cx, cy + cardH - 6, greenBrush);

                                // Bottom-Right
                                DrawCropMark(canvas, cx + cardW, cy + cardH, cx + cardW - 6, cy + cardH, greenBrush);
                                DrawCropMark(canvas, cx + cardW, cy + cardH, cx + cardW, cy + cardH - 6, greenBrush);
                            }
                        }

                        // Cricut mode registration marks
                        if (isCricut)
                        {
                            string pdfPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cricut_template.pdf");
                            var pdfBackground = RenderPdfPageToImage(pdfPath, 0, pageWidth * 4.0); // Render at 4x resolution for printing
                            if (pdfBackground != null)
                            {
                                var bgImg = new Image
                                {
                                    Width = pageWidth,
                                    Height = pageHeight,
                                    Source = pdfBackground,
                                    Stretch = Stretch.Fill
                                };
                                canvas.Children.Insert(0, bgImg); // Position background under card images
                            }
                            else
                            {
                                DrawCricutRegistrationMarks(canvas, pageWidth, pageHeight);
                            }
                        }

                        var pageContent = new PageContent();
                        ((IAddChild)pageContent).AddChild(fixedPage);
                        fixedDoc.Pages.Add(pageContent);
                    }

                    mainVm.StatusText = "Sending document to printer...";
                    printDialog.PrintDocument(fixedDoc.DocumentPaginator, $"{mainVm.ActiveDeckViewModel.Name} Proxies");
                    mainVm.StatusText = "Printing completed successfully!";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Print failed: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    mainVm.StatusText = "Print failed: " + ex.Message;
                }
            }
            else
            {
                mainVm.StatusText = "Print cancelled.";
            }
        }

                private static BitmapSource? RenderPdfPageToImage(string pdfPath, int pageIndex, double targetWidth)
        {
            try
            {
                if (!System.IO.File.Exists(pdfPath)) return null;

                return System.Threading.Tasks.Task.Run(async () =>
                {
                    var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(System.IO.Path.GetFullPath(pdfPath));
                    var pdfDoc = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);
                    if (pdfDoc.PageCount <= pageIndex) return null;

                    using var page = pdfDoc.GetPage((uint)pageIndex);
                    using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    
                    var options = new Windows.Data.Pdf.PdfPageRenderOptions();
                    if (targetWidth > 0)
                    {
                        options.DestinationWidth = (uint)targetWidth;
                    }
                    
                    await page.RenderToStreamAsync(stream, options);
                    
                    using var ioStream = System.IO.WindowsRuntimeStreamExtensions.AsStreamForRead(stream);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ioStream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return (BitmapSource)bitmap;
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to render PDF: {ex.Message}");
                return null;
            }
        }

        private void DrawCropMark(Canvas canvas, double x1, double y1, double x2, double y2, Brush strokeBrush)
        {
            var line = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = strokeBrush,
                StrokeThickness = 0.8
            };
            canvas.Children.Add(line);
        }

        private void DrawCricutRegistrationMarks(Canvas canvas, double pageWidth, double pageHeight)
        {
            // 7.44" x 9.94" centered box coordinates
            double boxW = 7.44 * 96.0;
            double boxH = 9.94 * 96.0;

            double left = (pageWidth - boxW) / 2.0;
            double right = pageWidth - left;
            double top = (pageHeight - boxH) / 2.0;
            double bottom = pageHeight - top;

            double thickness = 2.0;
            double lineLength = 48.0;
            Brush brush = Brushes.Black;

            // Helper to draw a line with thickness 2.0
            Action<double, double, double, double> drawLine = (x1, y1, x2, y2) =>
            {
                var line = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = brush,
                    StrokeThickness = thickness
                };
                canvas.Children.Add(line);
            };

            // Top-Left: L shape pointing down and right
            drawLine(left, top, left + lineLength, top);
            drawLine(left, top, left, top + lineLength);

            // Top-Right: L shape pointing down and left
            drawLine(right, top, right - lineLength, top);
            drawLine(right, top, right, top + lineLength);

            // Bottom-Left: L shape pointing up and right
            drawLine(left, bottom, left + lineLength, bottom);
            drawLine(left, bottom, left, bottom - lineLength);

            // Bottom-Right: L shape pointing up and left
            drawLine(right, bottom, right - lineLength, bottom);
            drawLine(right, bottom, right, bottom - lineLength);
        }

        private void RefreshScreenPreviewCropMarks()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RefreshScreenPreviewCropMarks));
                return;
            }

            if (PreviewCropMarksCanvas == null) return;
            PreviewCropMarksCanvas.Children.Clear();

            var vm = ViewModel?.ProxyPrinter;
            if (vm == null) return;

            double pageWidth = vm.PageWidth;
            double pageHeight = vm.PageHeight;
            double cardW = vm.CardW;
            double cardH = vm.CardH;
            double gutter = vm.Gutter;
            double gridW = vm.GridW;
            double gridH = vm.GridH;
            double startX = vm.StartX;
            double startY = vm.StartY;

            Brush guideBrush = vm.GuideColorBrush;

            // Handle Cricut background image on PrintPreviewCanvas
            Image? existingBg = null;
            if (PrintPreviewCanvas != null)
            {
                foreach (var child in PrintPreviewCanvas.Children)
                {
                    if (child is Image img && img.Tag as string == "CricutBackground")
                    {
                        existingBg = img;
                        break;
                    }
                }
            }

            if (vm.LayoutMode == "Cricut (6 Cards)")
            {
                if (existingBg == null)
                {
                    string pdfPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cricut_template.pdf");
                    var pdfBackground = RenderPdfPageToImage(pdfPath, 0, pageWidth);
                    if (pdfBackground != null && PrintPreviewCanvas != null)
                    {
                        var bgImg = new Image
                        {
                            Width = pageWidth,
                            Height = pageHeight,
                            Source = pdfBackground,
                            Stretch = Stretch.Fill,
                            Tag = "CricutBackground"
                        };
                        PrintPreviewCanvas.Children.Insert(0, bgImg); // Position background under card slots
                    }
                    else
                    {
                        DrawCricutRegistrationMarks(PreviewCropMarksCanvas, pageWidth, pageHeight);
                    }
                }
                else
                {
                    existingBg.Visibility = Visibility.Visible;
                }

                // Draw solid black borders or dashed margins if selected
                if (vm.GuideStyle == "Pure Black Borders" || vm.GuideStyle == "Dashed Margins")
                {
                    for (int i = 0; i < 9; i++)
                    {
                        if (i >= vm.ActivePageCards.Count || vm.ActivePageCards[i] == null) continue;

                        int col = i % 3;
                        int row = i / 3;
                        double x = col == 0 ? vm.Col0X : (col == 1 ? vm.Col1X : vm.Col2X);
                        double y = row == 0 ? vm.Row0Y : (row == 1 ? vm.Row1Y : vm.Row2Y);

                        var rect = new Rectangle
                        {
                            Width = cardH,
                            Height = cardW,
                            Stroke = guideBrush,
                            StrokeThickness = 0.5,
                            IsHitTestVisible = false
                        };
                        if (vm.GuideStyle == "Dashed Margins")
                        {
                            rect.StrokeDashArray = new DoubleCollection(new double[] { 4, 4 });
                        }

                        Canvas.SetLeft(rect, x);
                        Canvas.SetTop(rect, y);
                        PreviewCropMarksCanvas.Children.Add(rect);
                    }
                }
                return;
            }
            else
            {
                if (existingBg != null)
                {
                    existingBg.Visibility = Visibility.Collapsed;
                }
            }

            // Normal Spaced layout preview logic
            if (vm.GuideStyle == "None") return;

            if (vm.GuideStyle == "Pure Black Borders")
            {
                for (int i = 0; i < 9; i++)
                {
                    if (i >= vm.ActivePageCards.Count || vm.ActivePageCards[i] == null) continue;

                    int col = i % 3;
                    int row = i / 3;
                    double x = startX + col * (cardW + gutter);
                    double y = startY + row * (cardH + gutter);

                    var rect = new Rectangle
                    {
                        Width = cardW,
                        Height = cardH,
                        Stroke = guideBrush,
                        StrokeThickness = 0.5,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);
                    PreviewCropMarksCanvas.Children.Add(rect);
                }
            }
            else if (vm.GuideStyle == "Dashed Margins")
            {
                for (int i = 0; i < 9; i++)
                {
                    if (i >= vm.ActivePageCards.Count || vm.ActivePageCards[i] == null) continue;

                    int col = i % 3;
                    int row = i / 3;
                    double x = startX + col * (cardW + gutter);
                    double y = startY + row * (cardH + gutter);

                    var rect = new Rectangle
                    {
                        Width = cardW,
                        Height = cardH,
                        Stroke = guideBrush,
                        StrokeThickness = 0.5,
                        StrokeDashArray = new DoubleCollection(new double[] { 4, 4 }),
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);
                    PreviewCropMarksCanvas.Children.Add(rect);
                }
            }
            else if (vm.GuideStyle == "Crop Marks")
            {
                var vCuts = new List<double>();
                if (gutter > 0)
                {
                    vCuts.Add(startX);
                    vCuts.Add(startX + cardW);
                    vCuts.Add(startX + cardW + gutter);
                    vCuts.Add(startX + 2 * cardW + gutter);
                    vCuts.Add(startX + 2 * cardW + 2 * gutter);
                    vCuts.Add(startX + 3 * cardW + 2 * gutter);
                }
                else
                {
                    vCuts.Add(startX);
                    vCuts.Add(startX + cardW);
                    vCuts.Add(startX + 2 * cardW);
                    vCuts.Add(startX + 3 * cardW);
                }

                var hCuts = new List<double>();
                if (gutter > 0)
                {
                    hCuts.Add(startY);
                    hCuts.Add(startY + cardH);
                    hCuts.Add(startY + cardH + gutter);
                    hCuts.Add(startY + 2 * cardH + gutter);
                    hCuts.Add(startY + 2 * cardH + 2 * gutter);
                    hCuts.Add(startY + 3 * cardH + 2 * gutter);
                }
                else
                {
                    hCuts.Add(startY);
                    hCuts.Add(startY + cardH);
                    hCuts.Add(startY + 2 * cardH);
                    hCuts.Add(startY + 3 * cardH);
                }

                // 1. Draw vertical crop mark lines (guides running top to bottom in margins)
                foreach (var cx in vCuts)
                {
                    DrawCropMark(PreviewCropMarksCanvas, cx, 0, cx, startY + 12, guideBrush);
                    DrawCropMark(PreviewCropMarksCanvas, cx, startY + gridH - 12, cx, pageHeight, guideBrush);
                }

                // 2. Draw horizontal crop mark lines (guides running left to right in margins)
                foreach (var cy in hCuts)
                {
                    DrawCropMark(PreviewCropMarksCanvas, 0, cy, startX, cy, guideBrush);
                    DrawCropMark(PreviewCropMarksCanvas, startX + gridW, cy, pageWidth, cy, guideBrush);
                }

                // 4. Draw internal inward-pointing green corner L-ticks at every card's 4 corners (always)
                Brush greenBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0));
                greenBrush.Freeze();

                for (int i = 0; i < 9; i++)
                {
                    if (i >= vm.ActivePageCards.Count || vm.ActivePageCards[i] == null) continue;

                    int col = i % 3;
                    int row = i / 3;
                    double cx = startX + col * (cardW + gutter);
                    double cy = startY + row * (cardH + gutter);

                    // Top-Left
                    DrawCropMark(PreviewCropMarksCanvas, cx, cy, cx + 6, cy, greenBrush);
                    DrawCropMark(PreviewCropMarksCanvas, cx, cy, cx, cy + 6, greenBrush);

                    // Top-Right
                    DrawCropMark(PreviewCropMarksCanvas, cx + cardW, cy, cx + cardW - 6, cy, greenBrush);
                    DrawCropMark(PreviewCropMarksCanvas, cx + cardW, cy, cx + cardW, cy + 6, greenBrush);

                    // Bottom-Left
                    DrawCropMark(PreviewCropMarksCanvas, cx, cy + cardH, cx + 6, cy + cardH, greenBrush);
                    DrawCropMark(PreviewCropMarksCanvas, cx, cy + cardH, cx, cy + cardH - 6, greenBrush);

                    // Bottom-Right
                    DrawCropMark(PreviewCropMarksCanvas, cx + cardW, cy + cardH, cx + cardW - 6, cy + cardH, greenBrush);
                    DrawCropMark(PreviewCropMarksCanvas, cx + cardW, cy + cardH, cx + cardW, cy + cardH - 6, greenBrush);
                }
            }
        }

        private void QtyDecrement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PrintableCard pc)
            {
                pc.PrintQuantity = Math.Max(0, pc.PrintQuantity - 1);
            }
        }

        private void QtyIncrement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PrintableCard pc)
            {
                pc.PrintQuantity++;
            }
        }

        private void PreviewQtyDecrement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Card card)
            {
                var vm = ViewModel?.ProxyPrinter;
                if (vm != null)
                {
                    var pc = vm.PrintableCards.FirstOrDefault(x => x.Card.Id == card.Id);
                    if (pc != null)
                    {
                        pc.PrintQuantity = Math.Max(0, pc.PrintQuantity - 1);
                    }
                }
            }
        }

        private void PreviewQtyIncrement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Card card)
            {
                var vm = ViewModel?.ProxyPrinter;
                if (vm != null)
                {
                    var pc = vm.PrintableCards.FirstOrDefault(x => x.Card.Id == card.Id);
                    if (pc != null)
                    {
                        pc.PrintQuantity++;
                    }
                }
            }
        }

        private void PreviewQtyRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Card card)
            {
                var vm = ViewModel?.ProxyPrinter;
                if (vm != null)
                {
                    var pc = vm.PrintableCards.FirstOrDefault(x => x.Card.Id == card.Id);
                    if (pc != null)
                    {
                        pc.PrintQuantity = 0;
                        pc.IsSelected = false;
                    }
                }
            }
        }

        private void PreviewCard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Card? card = null;
                if (sender is Border border && border.DataContext is Card c)
                {
                    card = c;
                }
                else if (sender is FrameworkElement fe && fe.DataContext is Card c2)
                {
                    card = c2;
                }

                if (card != null && ViewModel != null)
                {
                    // Switch to Deck Builder Tab (Index 0)
                    if (MainTabControl != null)
                    {
                        MainTabControl.SelectedIndex = 0;
                    }

                    // Select the card in our search or deck list
                    _selectedCard = card;
                    SelectionPanel.DataContext = card;

                    // Update Inspector details
                    InspectorName.Text = card.Name;
                    InspectorType.Text = card.TypeLine;
                    InspectorText.Text = card.OracleText ?? "No rules text.";
                    InspectorCmc.Text = card.Cmc.ToString();
                    InspectorPrice.Text = !string.IsNullOrEmpty(card.PriceUsd) ? $"${card.PriceUsd}" : "N/A";

                    // Show panels
                    NoSelectionPanel.Visibility = Visibility.Collapsed;
                    SelectionPanel.Visibility = Visibility.Visible;

                    // Asynchronously update image
                    InspectorImage.Card = card;

                    // Populate printings combo box
                    if (ViewModel.DbService != null)
                    {
                        var name = card.Name;
                        var printings = ViewModel.DbService.Cards.Where(c3 => c3.Name == name)
                                                 .OrderBy(c3 => c3.SetName)
                                                 .ThenBy(c3 => c3.CollectorNumber)
                                                 .ToList();

                        PrintingsComboBox.SelectionChanged -= PrintingsComboBox_SelectionChanged;
                        PrintingsComboBox.ItemsSource = printings;

                        var active = printings.FirstOrDefault(p => p.Id == card.Id) ?? printings.FirstOrDefault();
                        PrintingsComboBox.SelectedItem = active;

                        PrintingsComboBox.SelectionChanged += PrintingsComboBox_SelectionChanged;
                    }

                    RefreshInspectorButtons();
                    
                    // Focus the printings selector to swap art immediately
                    PrintingsComboBox.Focus();
                }
            }
        }

        private void AddCardToSheet_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ProxySearchTextBox != null)
            {
                ProxySearchTextBox.Focus();
                ProxySearchTextBox.SelectAll();
            }
        }

        private void ProxySearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ProxyPrinter != null && ViewModel.ProxyPrinter.SearchResults.Count > 0)
            {
                ViewModel.ProxyPrinter.IsSearchResultsOpen = true;
            }
        }

        private void ProxySearch_LostFocus(object sender, RoutedEventArgs e)
        {
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                if (ProxySearchResultsPopup != null && !ProxySearchTextBox.IsFocused)
                {
                    if (ViewModel?.ProxyPrinter != null)
                    {
                        ViewModel.ProxyPrinter.IsSearchResultsOpen = false;
                    }
                }
            };
            timer.Start();
        }

        private void ProxySearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is Card card)
            {
                var vm = ViewModel?.ProxyPrinter;
                if (vm != null)
                {
                    vm.AddCardToPrintList(card);
                }

                listBox.SelectedIndex = -1;

                if (ProxySearchResultsPopup != null)
                {
                    ProxySearchResultsPopup.IsOpen = false;
                }
            }
        }

        private void LoadActiveDeck_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ProxyPrinter != null)
            {
                ViewModel.ProxyPrinter.LoadActiveDeck();
            }
        }

        private void ClearSheet_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ProxyPrinter != null)
            {
                ViewModel.ProxyPrinter.ClearCuration();
            }
        }

        private void ToggleLeftSidebar_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = false;
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggleBtn)
            {
                isChecked = toggleBtn.IsChecked == true;
            }
            else if (sender is MenuItem menuItem)
            {
                isChecked = menuItem.IsChecked;
            }
            else
            {
                isChecked = true;
            }
            SetLeftSidebarVisibility(isChecked);
        }

        private void ToggleRightSidebar_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = false;
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggleBtn)
            {
                isChecked = toggleBtn.IsChecked == true;
            }
            else if (sender is MenuItem menuItem)
            {
                isChecked = menuItem.IsChecked;
            }
            else
            {
                isChecked = true;
            }
            SetRightSidebarVisibility(isChecked);
        }

        private void TxtDeckListFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            var filterText = TxtDeckListFilter.Text;
            if (ViewModel?.ActiveDeckViewModel?.Cards == null) return;
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(ViewModel.ActiveDeckViewModel.Cards);
            if (view != null)
            {
                if (string.IsNullOrWhiteSpace(filterText))
                {
                    view.Filter = null;
                }
                else
                {
                    view.Filter = item =>
                    {
                        if (item is Card card)
                        {
                            return card.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                                   (card.TypeLine != null && card.TypeLine.Contains(filterText, StringComparison.OrdinalIgnoreCase));
                        }
                        return false;
                    };
                }
            }
        }

        private void DeckListGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DeckListGrid.SelectedItem is Card card)
            {
                SelectCardInInspector(card);
            }
        }

        private void DeckSpoilerItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;

            if (sender is ListBoxItem item && item.DataContext is Card card)
            {
                SelectCardInInspector(card);
                e.Handled = true;
            }
        }

        private void SetLeftSidebarVisibility(bool isVisible)
        {
            if (LeftActiveDeckColumn != null)
            {
                LeftActiveDeckColumn.Width = isVisible ? new GridLength(320) : new GridLength(0);
            }
            if (LeftSearchPanelGrid != null)
            {
                LeftSearchPanelGrid.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SetRightSidebarVisibility(bool isVisible)
        {
            if (RightStatsColumn != null)
            {
                RightStatsColumn.Width = isVisible ? new GridLength(340) : new GridLength(0);
            }
            if (RightStatsBorder != null)
            {
                RightStatsBorder.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void OpenRecommendations_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ActiveDeckViewModel != null)
            {
                var dialog = new RecommendationsWindow
                {
                    Owner = this,
                    DataContext = ViewModel.ActiveDeckViewModel
                };
                dialog.ShowDialog();
            }
        }

        private void OpenManaDesigner_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ActiveDeckViewModel != null)
            {
                var dialog = new ManaDesignerWindow
                {
                    Owner = this,
                    DataContext = ViewModel.ActiveDeckViewModel
                };
                dialog.ShowDialog();
            }
        }

        private void OpenGoals_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ActiveDeckViewModel != null)
            {
                var dialog = new GoalsWindow
                {
                    Owner = this,
                    DataContext = ViewModel.ActiveDeckViewModel
                };
                dialog.ShowDialog();
            }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            string? viewName = null;
            if (sender is RadioButton rb)
            {
                viewName = rb.CommandParameter as string;
            }
            else if (sender is Button btn)
            {
                viewName = btn.CommandParameter as string;
            }

            if (viewName != null && ViewModel != null)
            {
                ViewModel.ActiveView = viewName;
            }
        }

        private void SaveDeck_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ActiveDeckViewModel != null)
            {
                ViewModel.ActiveDeckViewModel.SaveDeck();
                MessageBox.Show("Deck saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ContextButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void RenameDeck_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.ActiveDeckViewModel == null) return;
            var dialog = new MtgCommanderBuilder.Views.InputDialog("Enter new deck name:", "Rename Deck", ViewModel.ActiveDeckViewModel.Name)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                string name = dialog.Answer;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    ViewModel.ActiveDeckViewModel.Name = name.Trim();
                    ViewModel.ActiveDeckViewModel.SaveDeck();
                }
            }
        }

        private void WizardRecommendedComp_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.DeckWizard != null)
            {
                ViewModel.DeckWizard.LandsCount = 38;
                ViewModel.DeckWizard.CreaturesCount = 30;
                ViewModel.DeckWizard.ArtifactsCount = 10;
                ViewModel.DeckWizard.EnchantmentsCount = 6;
                ViewModel.DeckWizard.InstantsCount = 8;
                ViewModel.DeckWizard.SorceriesCount = 7;
            }
        }

        private void WizardResetGoals_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.DeckWizard != null)
            {
                ViewModel.DeckWizard.RampGoal = 10;
                ViewModel.DeckWizard.DrawGoal = 10;
                ViewModel.DeckWizard.RemovalGoal = 10;
                ViewModel.DeckWizard.WipeGoal = 3;
                ViewModel.DeckWizard.ProtectionGoal = 6;
            }
        }

        private void AnalyticsTab_Selected(object sender, RoutedEventArgs e)
        {
            // Handled automatically via DataContext binding, but method stub must exist
        }

        private void DeckName_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Grid grid && grid.Children.Count > 0 && grid.Children[0] is StackPanel panel && panel.Children.Count > 0 && panel.Children[0] is TextBlock textBlock)
            {
                double availableWidth = grid.ActualWidth;
                double textWidth = textBlock.ActualWidth;

                if (textWidth > availableWidth)
                {
                    double scrollDistance = textWidth - availableWidth + 12;
                    if (textBlock.RenderTransform is TranslateTransform translate)
                    {
                        var animation = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            To = -scrollDistance,
                            Duration = new Duration(TimeSpan.FromSeconds(scrollDistance / 40.0 + 0.5)),
                            BeginTime = TimeSpan.FromMilliseconds(300),
                            AutoReverse = true,
                            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                        };
                        translate.BeginAnimation(TranslateTransform.XProperty, animation);
                    }
                }
            }
        }

        private void DeckName_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Grid grid && grid.Children.Count > 0 && grid.Children[0] is StackPanel panel && panel.Children.Count > 0 && panel.Children[0] is TextBlock textBlock)
            {
                if (textBlock.RenderTransform is TranslateTransform translate)
                {
                    var animation = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        To = 0,
                        Duration = new Duration(TimeSpan.FromSeconds(0.2))
                    };
                    translate.BeginAnimation(TranslateTransform.XProperty, animation);
                }
            }
        }
    }
}