using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MtgCommanderBuilder.Views
{
    public class SyntaxGuideWindow : Window
    {
        public SyntaxGuideWindow()
        {
            Title = "Scryfall Search Syntax Guide";
            Width = 480;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;
            Background = new SolidColorBrush(Color.FromRgb(17, 18, 24)); // #111218
            Foreground = Brushes.White;
            BorderBrush = new SolidColorBrush(Color.FromRgb(44, 46, 60)); // #2c2e3c
            BorderThickness = new Thickness(1);
            Topmost = true; // Keeps it floating on top so you can reference it while typing

            // Main Grid
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Scrollable Content
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(16)
            };

            var contentPanel = new StackPanel();

            // Title Header
            contentPanel.Children.Add(new TextBlock
            {
                Text = "SCRYFALL SEARCH SYNTAX GUIDE",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(202, 161, 62)), // #caa13e
                Margin = new Thickness(0, 0, 0, 8)
            });

            contentPanel.Children.Add(new TextBlock
            {
                Text = "The offline search engine supports advanced, chained Scryfall syntax filters. Combine tags to perform highly specific offline searches.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 170)), // Muted text
                Margin = new Thickness(0, 0, 0, 20)
            });

            // Helper to add filter section
            void AddFilterSection(string category, string syntax, string explanation, string example)
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = category,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 10, 0, 4)
                });

                contentPanel.Children.Add(new TextBlock
                {
                    Text = explanation,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                    Margin = new Thickness(0, 0, 0, 6)
                });

                // Example Code Box
                var codeBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(11, 12, 16)), // #0b0c10
                    BorderBrush = new SolidColorBrush(Color.FromRgb(44, 46, 60)), // #2c2e3c
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, 0, 0, 16)
                };

                var codeGrid = new Grid();
                codeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                codeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                codeGrid.Children.Add(new TextBlock
                {
                    Text = "Syntax:  ",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(202, 161, 62)) // Gold
                });

                var exampleText = new TextBlock
                {
                    Text = example,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Foreground = Brushes.LightGreen,
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetColumn(exampleText, 1);
                codeGrid.Children.Add(exampleText);

                codeBorder.Child = codeGrid;
                contentPanel.Children.Add(codeBorder);
            }

            // Syntax guide content sections
            AddFilterSection(
                "1. Card Types (t: or type:)",
                "t: or type:",
                "Filters cards by their card type, supertype, or subtype.",
                "t:creature   OR   t:\"legendary enchantment\""
            );

            AddFilterSection(
                "2. Oracle Rules Text (o: or oracle:)",
                "o: or oracle:",
                "Searches the text printed inside the card's rules text box. Enclose multi-word terms in double quotes.",
                "o:destroy   OR   o:\"draw a card\""
            );

            AddFilterSection(
                "3. Mana Value / CMC (cmc: or mv:)",
                "cmc: or mv:",
                "Matches the card's converted mana cost using mathematical operators: =, >=, <=, >, <.",
                "cmc=3   OR   mv>=4   OR   cmc<2"
            );

            AddFilterSection(
                "4. Card Colors (c: or color:)",
                "c: or color:",
                "Matches standard WUBRG colors. Supports operators (=, >=, <=, >, <). Use C for Colorless or M for Multicolor.",
                "c:uw (exact Blue/White)   OR   c<=r (Red/Colorless)   OR   c:c (Colorless)"
            );

            AddFilterSection(
                "5. Color Identity (id: or identity:)",
                "id: or identity:",
                "Matches standard card color identity (crucial for Commander legality checks). Supports subset/superset operators.",
                "id<=gr (fits Gruul decks)   OR   id:c (colorless identity)"
            );

            AddFilterSection(
                "6. Rarity (r: or rarity:)",
                "r: or rarity:",
                "Filters by card printing rarity: common, uncommon, rare, or mythic.",
                "r:rare   OR   rarity:mythic"
            );

            AddFilterSection(
                "7. Set Code (s: or set:)",
                "s: or set:",
                "Finds cards printed in a specific expansion set using its 3 or 4 letter set code.",
                "set:aer   OR   s:dom"
            );

            AddFilterSection(
                "8. Market Price (p:, price:, or usd:)",
                "p:, price:, or usd:",
                "Matches Scryfall's market USD price using mathematical operators.",
                "price<5.00   OR   usd>=10.00"
            );

            // Chaining Tips
            contentPanel.Children.Add(new TextBlock
            {
                Text = "⚡ Pro Search Tips",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(202, 161, 62)),
                Margin = new Thickness(0, 15, 0, 6)
            });

            var tipsBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 23, 30)), // #16171e
                BorderBrush = new SolidColorBrush(Color.FromRgb(44, 46, 60)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var tipsText = new TextBlock
            {
                Text = "• You can chain search queries together! For example, typing:\n  t:creature cmc=3 o:\"draw a card\" c:u\n  instantly filters the database to blue creature cards that cost exactly 3 mana and can draw you a card.\n\n• Mixed Search: You can combine tags with standard card name searching, like typing:\n  Cultivate t:sorcery\n\n• Colors & Identities can be combined easily, e.g., id<=uw matches blue, white, blue/white, and colorless cards.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                TextWrapping = TextWrapping.Wrap
            };
            tipsBorder.Child = tipsText;
            contentPanel.Children.Add(tipsBorder);

            scrollViewer.Content = contentPanel;
            Grid.SetRow(scrollViewer, 0);
            mainGrid.Children.Add(scrollViewer);

            // Close Button Panel
            var footerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(11, 12, 16)), // #0b0c10
                BorderBrush = new SolidColorBrush(Color.FromRgb(44, 46, 60)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 12, 16, 12)
            };

            var closeButton = new Button
            {
                Content = "CLOSE GUIDE",
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(202, 161, 62)), // #caa13e
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                IsDefault = true,
                IsCancel = true
            };
            closeButton.Click += (s, e) => Close();
            footerBorder.Child = closeButton;

            Grid.SetRow(footerBorder, 1);
            mainGrid.Children.Add(footerBorder);

            Content = mainGrid;
        }
    }
}
