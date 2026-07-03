using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MtgCommanderBuilder.Views
{
    public class InputDialog : Window
    {
        private TextBox _textBox;
        private Button _okButton;
        private Button _cancelButton;

        public string Answer => _textBox.Text;

        public InputDialog(string question, string title, string defaultAnswer = "")
        {
            Title = title;
            Width = 350;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(17, 18, 24)); // #111218
            Foreground = Brushes.White;
            BorderBrush = new SolidColorBrush(Color.FromRgb(44, 46, 60)); // #2c2e3c
            BorderThickness = new Thickness(1);
            ShowInTaskbar = false;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Margin = new Thickness(15);

            var textBlock = new TextBlock
            {
                Text = question,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(textBlock, 0);
            grid.Children.Add(textBlock);

            _textBox = new TextBox
            {
                Text = defaultAnswer,
                Margin = new Thickness(0, 0, 0, 15),
                Background = new SolidColorBrush(Color.FromRgb(15, 16, 21)), // #0f1015
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(44, 46, 60)), // #2c2e3c
                SelectionBrush = new SolidColorBrush(Color.FromRgb(202, 161, 62)), // #caa13e (AccentGold)
                CaretBrush = Brushes.White,
                Padding = new Thickness(4),
                Height = 26,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            // Select all text on focus and set focus
            _textBox.Focus();
            _textBox.SelectAll();
            Grid.SetRow(_textBox, 1);
            grid.Children.Add(_textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(buttonPanel, 2);

            _okButton = new Button
            {
                Content = "OK",
                Width = 70,
                Height = 24,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(202, 161, 62)), // #caa13e
                Foreground = Brushes.Black,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                IsDefault = true
            };
            _okButton.Click += (s, e) => { DialogResult = true; Close(); };
            buttonPanel.Children.Add(_okButton);

            _cancelButton = new Button
            {
                Content = "Cancel",
                Width = 70,
                Height = 24,
                Background = new SolidColorBrush(Color.FromRgb(44, 46, 60)), // #2c2e3c
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                IsCancel = true
            };
            _cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
            buttonPanel.Children.Add(_cancelButton);

            grid.Children.Add(buttonPanel);
            Content = grid;
        }
    }
}
