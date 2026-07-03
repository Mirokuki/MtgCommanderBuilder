using System.Windows;

namespace MtgCommanderBuilder.Views
{
    public partial class GoalsWindow : Window
    {
        public GoalsWindow()
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
