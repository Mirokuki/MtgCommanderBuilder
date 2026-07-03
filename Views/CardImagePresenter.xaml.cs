using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using MtgCommanderBuilder.Models;
using MtgCommanderBuilder.ViewModels;

namespace MtgCommanderBuilder.Views
{
    public partial class CardImagePresenter : UserControl
    {
        public static readonly DependencyProperty CardProperty =
            DependencyProperty.Register(nameof(Card), typeof(Card), typeof(CardImagePresenter),
                new PropertyMetadata(null, OnCardChanged));

        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register(nameof(Stretch), typeof(System.Windows.Media.Stretch), typeof(CardImagePresenter),
                new PropertyMetadata(System.Windows.Media.Stretch.Uniform));

        public static readonly DependencyProperty BleedAmountProperty =
            DependencyProperty.Register(nameof(BleedAmount), typeof(double), typeof(CardImagePresenter),
                new PropertyMetadata(0.0, OnBleedAmountChanged));

        public Card? Card
        {
            get => (Card?)GetValue(CardProperty);
            set => SetValue(CardProperty, value);
        }

        public System.Windows.Media.Stretch Stretch
        {
            get => (System.Windows.Media.Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        public double BleedAmount
        {
            get => (double)GetValue(BleedAmountProperty);
            set => SetValue(BleedAmountProperty, value);
        }

        private static void OnBleedAmountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CardImagePresenter presenter)
            {
                presenter.UpdateBleedRendering();
            }
        }

        public CardImagePresenter()
        {
            InitializeComponent();
            Loaded += (s, e) => { _ = LoadCardImageAsync(); };
        }

        private static void OnCardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CardImagePresenter presenter)
            {
                _ = presenter.LoadCardImageAsync();
            }
        }

        private Grid? _bleedGrid = null;
        private BitmapSource? _originalBitmap = null;

        private async Task LoadCardImageAsync()
        {
            if (!IsLoaded) return;

            ImgPresenter.Source = null;
            _originalBitmap = null;
            ImgPresenter.Opacity = 0;
            LoadingOverlay.Visibility = Visibility.Collapsed;
            PlaceholderOverlay.Visibility = Visibility.Collapsed;

            if (_bleedGrid != null)
            {
                RootGrid.Children.Remove(_bleedGrid);
                _bleedGrid = null;
            }

            var activeCard = Card;
            if (activeCard == null)
            {
                PlaceholderOverlay.Visibility = Visibility.Visible;
                _originalBitmap = null;
                return;
            }

            // Determine remote url (prefer normal face image, fallback to art crop)
            string remoteUrl = !string.IsNullOrEmpty(activeCard.NormalImageUrl) 
                ? activeCard.NormalImageUrl 
                : activeCard.ArtCropImageUrl;

            if (string.IsNullOrEmpty(remoteUrl))
            {
                PlaceholderOverlay.Visibility = Visibility.Visible;
                return;
            }

            // Resolve ImageCacheService from MainWindow context
            var window = Window.GetWindow(this) as MainWindow;
            var mainVm = window?.DataContext as MainViewModel;
            var cacheService = mainVm?.ImageCache;

            if (cacheService == null)
            {
                // Fallback to loading directly from url online if service isn't found
                try
                {
                    var bitmap = new BitmapImage(new Uri(remoteUrl));
                    _originalBitmap = bitmap;
                    ImgPresenter.Source = bitmap;
                    ImgPresenter.Opacity = 1;
                    UpdateBleedRendering();
                }
                catch
                {
                    _originalBitmap = null;
                    PlaceholderOverlay.Visibility = Visibility.Visible;
                }
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;

            string localPath = await cacheService.GetImageAsync(activeCard.Id, remoteUrl);

            // Double check if the card is still the active card to handle list scrolling race conditions
            if (Card != activeCard) return;

            LoadingOverlay.Visibility = Visibility.Collapsed;

            if (!string.IsNullOrEmpty(localPath) && (File.Exists(localPath) || localPath.StartsWith("http")))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = localPath.StartsWith("http") ? new Uri(localPath) : new Uri(Path.GetFullPath(localPath));
                    bitmap.EndInit();
                    bitmap.Freeze(); // Crucial for thread safety and memory in WPF

                    _originalBitmap = bitmap;
                    ImgPresenter.Source = bitmap;
                    
                    // Beautiful fade-in animation
                    var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(250));
                    ImgPresenter.BeginAnimation(OpacityProperty, fadeIn);

                    UpdateBleedRendering();
                }
                catch
                {
                    _originalBitmap = null;
                    PlaceholderOverlay.Visibility = Visibility.Visible;
                }
            }
            else
            {
                _originalBitmap = null;
                PlaceholderOverlay.Visibility = Visibility.Visible;
            }
        }

        private void OnImgPresenterSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Image img)
            {
                double radius = (3.3 / 25.4) * 96.0; // exactly 3.3mm radius (approx 12.47 units)
                img.Clip = new RectangleGeometry(new Rect(0, 0, e.NewSize.Width, e.NewSize.Height), radius, radius);
            }
        }

        private void UpdateBleedRendering()
        {
            // Remove previous bleed grid if it exists
            if (_bleedGrid != null)
            {
                RootGrid.Children.Remove(_bleedGrid);
                _bleedGrid = null;
            }

            // Unregister first to prevent any size change from applying the clip during source update
            ImgPresenter.SizeChanged -= OnImgPresenterSizeChanged;

            var bitmapSource = _originalBitmap;
            double bleed = BleedAmount;

            if (bleed > 0 && bitmapSource != null)
            {
                ImgPresenter.Visibility = Visibility.Visible;

                // Get control dimensions or slot dimensions
                double cardW = Width > 0 ? Width : ActualWidth;
                double cardH = Height > 0 ? Height : ActualHeight;

                if (cardW <= 0) cardW = (63.0 / 25.4) * 96.0; // fallback to standard width (approx 238.11 units)
                if (cardH <= 0) cardH = (88.0 / 25.4) * 96.0; // fallback to standard height (approx 332.60 units)

                try
                {
                    var bleedBitmap = CreateBleedBitmap(bitmapSource, bleed, cardW, cardH);
                    ImgPresenter.Source = bleedBitmap;
                    ImgPresenter.Clip = null; // No clip needed, since bleed bitmap has its own corners
                }
                catch
                {
                    ImgPresenter.Source = bitmapSource;
                }
            }
            else
            {
                ImgPresenter.Visibility = Visibility.Visible;
                ImgPresenter.Source = bitmapSource;

                if (bitmapSource != null)
                {
                    ImgPresenter.SizeChanged += OnImgPresenterSizeChanged;
                    double w = ImgPresenter.ActualWidth > 0 ? ImgPresenter.ActualWidth : Width;
                    double h = ImgPresenter.ActualHeight > 0 ? ImgPresenter.ActualHeight : Height;
                    if (w > 0 && h > 0)
                    {
                        double radius = (3.3 / 25.4) * 96.0; // exactly 3.3mm radius (approx 12.47 units)
                        ImgPresenter.Clip = new RectangleGeometry(new Rect(0, 0, w, h), radius, radius);
                    }
                }
                else
                {
                    ImgPresenter.Clip = null;
                }
            }
        }

        public static BitmapSource CreateBleedBitmap(BitmapSource source, double bleedPx, double cardW, double cardH)
        {
            if (source == null) return null!;

            int pw = source.PixelWidth;
            int ph = source.PixelHeight;

            // Convert bleed from WPF units to source image pixels
            int bleedX = (int)Math.Round(pw * (bleedPx / cardW));
            int bleedY = (int)Math.Round(ph * (bleedPx / cardH));

            if (bleedX <= 0 && bleedY <= 0)
            {
                return source;
            }

            int W = pw + 2 * bleedX;
            int H = ph + 2 * bleedY;

            // Convert source to Bgra32 for guaranteed byte layout
            FormatConvertedBitmap formatted = new FormatConvertedBitmap();
            formatted.BeginInit();
            formatted.Source = source;
            formatted.DestinationFormat = PixelFormats.Bgra32;
            formatted.EndInit();

            byte[] srcPixels = new byte[ph * pw * 4];
            formatted.CopyPixels(srcPixels, pw * 4, 0);

            byte[] destPixels = new byte[H * W * 4];

            // Corner radii in pixels (3.3 mm corner radius)
            double rx = pw * (3.3 / 63.0);
            double ry = ph * (3.3 / 88.0);

            for (int Y = 0; Y < H; Y++)
            {
                for (int X = 0; X < W; X++)
                {
                    int x = X - bleedX;
                    int y = Y - bleedY;

                    int srcX = 0;
                    int srcY = 0;

                    bool isLeft = x < rx;
                    bool isRight = x > pw - 1 - rx;
                    bool isTop = y < ry;
                    bool isBottom = y > ph - 1 - ry;

                    if (isLeft && isTop)
                    {
                        double dx = x - rx;
                        double dy = y - ry;
                        if ((dx * dx) / (rx * rx) + (dy * dy) / (ry * ry) <= 1.0 && x >= 0 && y >= 0)
                        {
                            srcX = x;
                            srcY = y;
                        }
                        else
                        {
                            double theta = Math.Atan2(dy, dx);
                            srcX = (int)Math.Round(rx + rx * Math.Cos(theta));
                            srcY = (int)Math.Round(ry + ry * Math.Sin(theta));
                        }
                    }
                    else if (isRight && isTop)
                    {
                        double dx = x - (pw - 1 - rx);
                        double dy = y - ry;
                        if ((dx * dx) / (rx * rx) + (dy * dy) / (ry * ry) <= 1.0 && x < pw && y >= 0)
                        {
                            srcX = x;
                            srcY = y;
                        }
                        else
                        {
                            double theta = Math.Atan2(dy, dx);
                            srcX = (int)Math.Round((pw - 1 - rx) + rx * Math.Cos(theta));
                            srcY = (int)Math.Round(ry + ry * Math.Sin(theta));
                        }
                    }
                    else if (isLeft && isBottom)
                    {
                        double dx = x - rx;
                        double dy = y - (ph - 1 - ry);
                        if ((dx * dx) / (rx * rx) + (dy * dy) / (ry * ry) <= 1.0 && x >= 0 && y < ph)
                        {
                            srcX = x;
                            srcY = y;
                        }
                        else
                        {
                            double theta = Math.Atan2(dy, dx);
                            srcX = (int)Math.Round(rx + rx * Math.Cos(theta));
                            srcY = (int)Math.Round((ph - 1 - ry) + ry * Math.Sin(theta));
                        }
                    }
                    else if (isRight && isBottom)
                    {
                        double dx = x - (pw - 1 - rx);
                        double dy = y - (ph - 1 - ry);
                        if ((dx * dx) / (rx * rx) + (dy * dy) / (ry * ry) <= 1.0 && x < pw && y < ph)
                        {
                            srcX = x;
                            srcY = y;
                        }
                        else
                        {
                            double theta = Math.Atan2(dy, dx);
                            srcX = (int)Math.Round((pw - 1 - rx) + rx * Math.Cos(theta));
                            srcY = (int)Math.Round((ph - 1 - ry) + ry * Math.Sin(theta));
                        }
                    }
                    else
                    {
                        srcX = Math.Clamp(x, 0, pw - 1);
                        srcY = Math.Clamp(y, 0, ph - 1);
                    }

                    srcX = Math.Clamp(srcX, 0, pw - 1);
                    srcY = Math.Clamp(srcY, 0, ph - 1);

                    int srcIdx = (srcY * pw + srcX) * 4;
                    int destIdx = (Y * W + X) * 4;

                    destPixels[destIdx] = srcPixels[srcIdx];
                    destPixels[destIdx + 1] = srcPixels[srcIdx + 1];
                    destPixels[destIdx + 2] = srcPixels[srcIdx + 2];
                    destPixels[destIdx + 3] = srcPixels[srcIdx + 3];
                }
            }

            WriteableBitmap result = new WriteableBitmap(W, H, source.DpiX, source.DpiY, PixelFormats.Bgra32, null);
            result.WritePixels(new Int32Rect(0, 0, W, H), destPixels, W * 4, 0);
            result.Freeze();
            return result;
        }
    }
}
