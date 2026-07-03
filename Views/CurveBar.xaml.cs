using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using MtgCommanderBuilder.ViewModels;

namespace MtgCommanderBuilder.Views
{
    public partial class CurveBar : UserControl
    {
        public static readonly DependencyProperty ValueCountProperty =
            DependencyProperty.Register(nameof(ValueCount), typeof(int), typeof(CurveBar),
                new PropertyMetadata(0, OnParametersChanged));

        public static readonly DependencyProperty MaxCountProperty =
            DependencyProperty.Register(nameof(MaxCount), typeof(int), typeof(CurveBar),
                new PropertyMetadata(15, OnParametersChanged));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(CurveBar),
                new PropertyMetadata(string.Empty, OnParametersChanged));

        public int ValueCount
        {
            get => (int)GetValue(ValueCountProperty);
            set => SetValue(ValueCountProperty, value);
        }

        public int MaxCount
        {
            get => (int)GetValue(MaxCountProperty);
            set => SetValue(MaxCountProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        private DeckViewModel? ViewModel => DataContext as DeckViewModel;

        public CurveBar()
        {
            InitializeComponent();
            DataContextChanged += CurveBar_DataContextChanged;
            Loaded += CurveBar_Loaded;
        }

        private void CurveBar_Loaded(object sender, RoutedEventArgs e)
        {
            RecalculateHeight();
        }

        private void CurveBar_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DeckViewModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is DeckViewModel newVm)
            {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
            }

            RecalculateHeight();
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DeckViewModel.Cards) || e.PropertyName == nameof(DeckViewModel.DeckSize))
            {
                RecalculateHeight();
            }
        }

        private static void OnParametersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CurveBar bar)
            {
                bar.RecalculateHeight();
            }
        }

        private void RecalculateHeight()
        {
            if (ViewModel == null) return;

            int count = 0;
            var nonLands = ViewModel.Cards.Where(c => !c.IsLand);

            foreach (var card in nonLands)
            {
                int cmcBucket = GetCmcBucket(card.Cmc);
                if (cmcBucket == ValueCount)
                {
                    count += card.Quantity;
                }
            }

            TxtCount.Text = count.ToString();

            // Calculate height ratio (max visual height = 110px)
            double maxVisualHeight = 110.0;
            double targetHeight = 0;
            
            if (MaxCount > 0)
            {
                double ratio = (double)count / MaxCount;
                if (ratio > 1.0) ratio = 1.0;
                targetHeight = ratio * maxVisualHeight;
            }

            // Animate height change for fluid modern feels
            var heightAnimation = new DoubleAnimation
            {
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BarVisual.BeginAnimation(HeightProperty, heightAnimation);
        }

        private int GetCmcBucket(double cmc)
        {
            int intCmc = (int)Math.Floor(cmc);
            if (intCmc < 0) return 0;
            if (intCmc >= 7) return 7; // 7 represents "7+"
            return intCmc;
        }
    }
}
