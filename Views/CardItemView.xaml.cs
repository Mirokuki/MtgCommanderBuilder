using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MtgCommanderBuilder.Models;
using MtgCommanderBuilder.ViewModels;

namespace MtgCommanderBuilder.Views
{
    public partial class CardItemView : UserControl
    {
        private Card? Card => DataContext as Card;

        public CardItemView()
        {
            InitializeComponent();
        }

        private void UserControl_MouseEnter(object sender, MouseEventArgs e)
        {
            ControlsPanel.Visibility = Visibility.Visible;
            ApplyGlowEffect(true);
        }

        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            ControlsPanel.Visibility = Visibility.Collapsed;
            ApplyGlowEffect(false);
        }

        private void ApplyGlowEffect(bool enabled)
        {
            if (Card == null) return;

            if (enabled)
            {
                Color glowColor = Colors.Gold;
                SolidColorBrush? brush = null;

                if (Card.Colors.Count > 1)
                {
                    brush = Application.Current.Resources["MtgGoldGlow"] as SolidColorBrush;
                }
                else if (Card.Colors.Count == 0)
                {
                    brush = Application.Current.Resources["MtgColorlessGlow"] as SolidColorBrush;
                }
                else
                {
                    string colorChar = Card.Colors[0].ToUpperInvariant();
                    string resourceKey = colorChar switch
                    {
                        "W" => "MtgWhiteGlow",
                        "U" => "MtgBlueGlow",
                        "B" => "MtgBlackGlow",
                        "R" => "MtgRedGlow",
                        "G" => "MtgGreenGlow",
                        _ => "MtgColorlessGlow"
                    };
                    brush = Application.Current.Resources[resourceKey] as SolidColorBrush;
                }

                if (brush != null)
                {
                    glowColor = brush.Color;
                }

                GlowEffect.Color = glowColor;
                GlowEffect.Opacity = 0.8;
                CardBorder.BorderBrush = new SolidColorBrush(glowColor);
            }
            else
            {
                GlowEffect.Opacity = 0.0;
                CardBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(44, 46, 60)); // Reset to default #2c2e3c
            }
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (Card == null) return;
            var window = Window.GetWindow(this) as MainWindow;
            var mainVm = window?.DataContext as MainViewModel;
            mainVm?.ActiveDeckViewModel?.RemoveCard(Card);
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (Card == null) return;
            var window = Window.GetWindow(this) as MainWindow;
            var mainVm = window?.DataContext as MainViewModel;
            mainVm?.ActiveDeckViewModel?.AddCard(Card, false);
        }

        private void RemoveAll_Click(object sender, RoutedEventArgs e)
        {
            if (Card == null) return;
            var window = Window.GetWindow(this) as MainWindow;
            var mainVm = window?.DataContext as MainViewModel;
            mainVm?.ActiveDeckViewModel?.RemoveAllCopies(Card);
        }

        private void UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Card == null) return;
            var window = Window.GetWindow(this) as MainWindow;
            if (window != null)
            {
                window.SelectCardInInspector(Card);
            }
        }
    }
}
