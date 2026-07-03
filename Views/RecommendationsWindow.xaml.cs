using System.Windows;

namespace MtgCommanderBuilder.Views
{
    public partial class RecommendationsWindow : Window
    {
        public RecommendationsWindow()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
