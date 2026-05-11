using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DriveCare.Controls
{
    public partial class TintedImage : UserControl
    {
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(ImageSource),
            typeof(TintedImage),
            new PropertyMetadata(null, OnSourceChanged));

        public ImageSource Source
        {
            get => (ImageSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public TintedImage()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplyMask();
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TintedImage img)
            {
                if (img.MaskBrush != null)
                    img.ApplyMask();
            }
        }

        private void ApplyMask()
        {
            if (MaskBrush == null)
                return;
            MaskBrush.ImageSource = Source;
        }
    }
}
