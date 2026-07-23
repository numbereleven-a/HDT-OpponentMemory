using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace OpponentMemory
{
	internal sealed class OverlayLabel : FrameworkElement
	{
		private static readonly Brush OutlineBrush = FrozenBrush(Color.FromArgb(225, 0, 0, 0));
		private static readonly Brush ShadowBrush = FrozenBrush(Color.FromArgb(105, 0, 0, 0));
		private static readonly Brush BadgeBackgroundBrush = FrozenBrush(Color.FromArgb(178, 9, 12, 17));
		private static readonly Brush BadgeShadowBrush = FrozenBrush(Color.FromArgb(90, 0, 0, 0));
		private string _text = "";
		private FontFamily _fontFamily = new FontFamily("Segoe UI");
		private double _fontSize = 22;
		private FontWeight _fontWeight = FontWeights.Bold;
		private Brush _foreground = Brushes.White;
		private Brush _background = Brushes.Transparent;
		private Brush _badgeBorderBrush = Brushes.White;
		private OverlayTextStyle _style;
		private FormattedText? _formattedText;

		public OverlayLabel()
		{
			IsHitTestVisible = false;
			SnapsToDevicePixels = true;
			UseLayoutRounding = true;
			TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
			TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);
		}

		public double FontSize => _fontSize;

		public void Configure(
			string text,
			FontFamily fontFamily,
			double fontSize,
			FontWeight fontWeight,
			Brush foreground,
			Brush background,
			OverlayTextStyle style)
		{
			var textFormatChanged = !string.Equals(_text, text, StringComparison.Ordinal)
				|| !string.Equals(_fontFamily.Source, fontFamily.Source, StringComparison.Ordinal)
				|| Math.Abs(_fontSize - fontSize) > .001
				|| _fontWeight != fontWeight
				|| !ReferenceEquals(_foreground, foreground);
			var measureChanged = textFormatChanged || _style != style;
			var renderChanged = measureChanged || !ReferenceEquals(_background, background);

			_text = text;
			_fontFamily = fontFamily;
			_fontSize = fontSize;
			_fontWeight = fontWeight;
			_foreground = foreground;
			_background = background;
			_style = style;
			if(textFormatChanged)
			{
				_formattedText = null;
				_badgeBorderBrush = CreateBadgeBorderBrush(foreground);
			}
			if(measureChanged)
				InvalidateMeasure();
			if(renderChanged)
				InvalidateVisual();
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			var text = GetFormattedText();
			var padding = GetPadding();
			return new Size(
				Math.Ceiling(text.WidthIncludingTrailingWhitespace + padding.Left + padding.Right),
				Math.Ceiling(text.Height + padding.Top + padding.Bottom));
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			var text = GetFormattedText();
			var padding = GetPadding();
			var origin = new Point(padding.Left, padding.Top);
			switch(_style)
			{
				case OverlayTextStyle.Outlined:
					DrawConfiguredBackground(drawingContext, 0);
					DrawOutlinedText(drawingContext, text, origin);
					break;
				case OverlayTextStyle.Badge:
					DrawBadge(drawingContext, text, origin);
					break;
				default:
					DrawConfiguredBackground(drawingContext, 0);
					drawingContext.DrawText(text, origin);
					break;
			}
		}

		private void DrawOutlinedText(DrawingContext drawingContext, FormattedText text, Point origin)
		{
			var geometry = text.BuildGeometry(origin);
			var outlineWidth = Math.Max(1.2, Math.Min(2.4, _fontSize * .075));
			var shadowWidth = outlineWidth + 2.2;
			drawingContext.PushTransform(new TranslateTransform(0, 1.25));
			drawingContext.DrawGeometry(ShadowBrush, new Pen(ShadowBrush, shadowWidth), geometry);
			drawingContext.Pop();
			drawingContext.DrawGeometry(_foreground, new Pen(OutlineBrush, outlineWidth), geometry);
			drawingContext.DrawGeometry(_foreground, null, geometry);
		}

		private void DrawBadge(DrawingContext drawingContext, FormattedText text, Point origin)
		{
			var radius = Math.Max(4, Math.Min(7, _fontSize * .22));
			var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
			var shadowBounds = new Rect(0, 1.25, ActualWidth, ActualHeight);
			drawingContext.DrawRoundedRectangle(BadgeShadowBrush, null, shadowBounds, radius, radius);
			var background = IsVisibleBrush(_background) ? _background : BadgeBackgroundBrush;
			drawingContext.DrawRoundedRectangle(background, new Pen(_badgeBorderBrush, 1), bounds, radius, radius);

			var geometry = text.BuildGeometry(origin);
			drawingContext.DrawGeometry(_foreground, new Pen(OutlineBrush, .65), geometry);
			drawingContext.DrawGeometry(_foreground, null, geometry);
		}

		private void DrawConfiguredBackground(DrawingContext drawingContext, double radius)
		{
			if(!IsVisibleBrush(_background))
				return;
			drawingContext.DrawRoundedRectangle(_background, null, new Rect(0, 0, ActualWidth, ActualHeight), radius, radius);
		}

		private FormattedText GetFormattedText()
		{
			if(_formattedText != null)
				return _formattedText;
			var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
			_formattedText = new FormattedText(
				_text,
				CultureInfo.InvariantCulture,
				FlowDirection.LeftToRight,
				new Typeface(_fontFamily, FontStyles.Normal, _fontWeight, FontStretches.Normal),
				_fontSize,
				_foreground,
				pixelsPerDip);
			return _formattedText;
		}

		private Thickness GetPadding()
		{
			switch(_style)
			{
				case OverlayTextStyle.Outlined: return new Thickness(4, 2, 4, 2);
				case OverlayTextStyle.Badge: return new Thickness(6, 2, 6, 2);
				default: return new Thickness(3, 0, 3, 0);
			}
		}

		private static Brush CreateBadgeBorderBrush(Brush foreground)
		{
			var color = foreground is SolidColorBrush solid ? solid.Color : Colors.White;
			var brush = new SolidColorBrush(color) { Opacity = Math.Min(.55, foreground.Opacity * .45) };
			if(brush.CanFreeze)
				brush.Freeze();
			return brush;
		}

		private static bool IsVisibleBrush(Brush brush)
			=> brush.Opacity > .001 && (!(brush is SolidColorBrush solid) || solid.Color.A > 0);

		private static Brush FrozenBrush(Color color)
		{
			var brush = new SolidColorBrush(color);
			brush.Freeze();
			return brush;
		}
	}
}
