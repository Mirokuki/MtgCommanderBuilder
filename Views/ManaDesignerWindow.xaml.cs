using System.Windows;

namespace MtgCommanderBuilder.Views
{
    public partial class ManaDesignerWindow : Window
    {
        public ManaDesignerWindow()
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
