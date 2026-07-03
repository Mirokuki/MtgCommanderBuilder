using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MtgCommanderBuilder.ViewModels;

namespace MtgCommanderBuilder.Views
{
    public partial class RecommendationsView : UserControl
    {
        public RecommendationsView()
        {
            InitializeComponent();
        }

        private void AddCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string cardName)
            {
                var mainWin = Application.Current.MainWindow as MainWindow;
                var mainVm = mainWin?.DataContext as MainViewModel;
                if (mainVm?.DbService != null && mainVm.ActiveDeckViewModel != null)
                {
                    if (!mainVm.DbService.IsLoaded)
                    {
                        MessageBox.Show("Card database is currently loading. Please wait a moment and try again.", "Database Loading", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var card = mainVm.DbService.Cards.FirstOrDefault(c => c.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase));
                    if (card != null)
                    {
                        mainVm.ActiveDeckViewModel.AddCard(card, false, false);
                        MessageBox.Show($"Added '{cardName}' to your deck!", "Card Added", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Could not find card '{cardName}' in the database.", "Card Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }
    }
}
