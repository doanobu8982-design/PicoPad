namespace GamepadEmulator.UI
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Media;

    public class OutlinedTextBlock : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(new FontFamily("Segoe UI"), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight), typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(FontWeights.Bold, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaxTextWidthProperty =
            DependencyProperty.Register(nameof(MaxTextWidth), typeof(double), typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public Brush Fill
        {
            get => (Brush)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        // 既存 MainWindow.xaml.cs との互換性用プロパティ（Foreground を Fill にルーティング）
        public Brush Foreground
        {
            get => Fill;
            set => Fill = value;
        }

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public FontFamily FontFamily
        {
            get => (FontFamily)GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public FontWeight FontWeight
        {
            get => (FontWeight)GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        public double MaxTextWidth
        {
            get => (double)GetValue(MaxTextWidthProperty);
            set => SetValue(MaxTextWidthProperty, value);
        }

        // 親レイアウトスロットによる強制矩形クリッピングを完全に無効化
        protected override Geometry? GetLayoutClip(Size layoutSlotSize)
        {
            return null;
        }

        private FormattedText? CreateFormattedText()
        {
            if (string.IsNullOrEmpty(Text))
            {
                return null;
            }

            double effectiveMaxWidth = double.PositiveInfinity;
            if (MaxTextWidth > 0)
            {
                effectiveMaxWidth = MaxTextWidth;
            }
            else if (RenderSize.Width > 0)
            {
                effectiveMaxWidth = RenderSize.Width;
            }
            else if (!double.IsNaN(Width) && Width > 0)
            {
                effectiveMaxWidth = Width;
            }

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            Typeface typeface = new(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal);
            var formatted = new FormattedText(
                Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                FontSize,
                Fill ?? Brushes.White,
                pixelsPerDip)
            {
                TextAlignment = TextAlignment.Center
            };

            if (!double.IsInfinity(effectiveMaxWidth) && effectiveMaxWidth > 0)
            {
                formatted.MaxTextWidth = effectiveMaxWidth;
            }

            return formatted;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            FormattedText? formatted = CreateFormattedText();
            if (formatted == null)
            {
                return new Size(0, 0);
            }

            double desiredWidth = formatted.WidthIncludingTrailingWhitespace + StrokeThickness;
            if (!double.IsNaN(Width) && Width > 0)
            {
                desiredWidth = Width;
            }

            return new Size(
                desiredWidth,
                formatted.Height + StrokeThickness);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            FormattedText? formatted = CreateFormattedText();
            if (formatted == null)
            {
                return;
            }

            double originY = Math.Max(0, (RenderSize.Height - formatted.Height) / 2.0);

            Geometry geometry = formatted.BuildGeometry(new Point(0, originY));

            // ステップA（下層：黒フチ描画）
            if (Stroke != null && StrokeThickness > 0)
            {
                Pen pen = new(Stroke, StrokeThickness)
                {
                    LineJoin = PenLineJoin.Round
                };
                drawingContext.DrawGeometry(null, pen, geometry);
            }

            // ステップB（上層：白文字本体の上乗せ描画）
            if (Fill != null)
            {
                drawingContext.DrawGeometry(Fill, null, geometry);
            }
        }
    }
}

