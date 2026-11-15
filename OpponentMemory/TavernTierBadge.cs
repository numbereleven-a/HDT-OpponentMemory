using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace OpponentMemory
{
	internal sealed class TavernTierBadge : FrameworkElement
	{
		private static readonly Geometry HexagonGeometry = FrozenGeometry("M 8,2 L 24,2 L 31,16 L 24,30 L 8,30 L 1,16 Z");
		private static readonly Geometry ShieldGeometry = FrozenGeometry("M 3,3 L 9,2 L 16,3 L 23,2 L 29,3 L 27,9 L 26,24 Q 22,28 16,30 Q 10,28 6,24 L 5,9 Z");
		private static readonly Typeface BadgeTypeface = new Typeface("Segoe UI");
		private static readonly Brush ShieldBackingBrush = FrozenBrush(Color.FromRgb(24, 24, 24));
		private static readonly Pen OuterBlackPen = FrozenPen(Brushes.Black, 3);
		private static readonly Pen GoldBorderPen = FrozenPen(Brushes.Gold, 2.5);
		private static readonly Pen DarkGoldBorderPen = FrozenPen(Brushes.DarkGoldenrod, 1.5);
		private static readonly Pen GrayBorderPen = FrozenPen(Brushes.Gray, 1.5);
		private static readonly Pen StarBorderPen = FrozenPen(Brushes.Black, .55);
		private static readonly Pen PipBorderPen = FrozenPen(Brushes.Black, .6);
		private static readonly Geometry[][] TierStars = BuildTierStars();
		private int _tier;
		private double _size;
		private Brush _color = Brushes.Purple;
		private bool _scheduled;
		private TavernTierStyle _style;
		private ScaleTransform _scaleTransform = Transform.Identity as ScaleTransform ?? new ScaleTransform(1, 1);
		public TavernTierBadge() => IsHitTestVisible = false;

		public void Configure(int tier, double size, double opacity, Brush color, bool scheduled, TavernTierStyle style)
		{
			if(Math.Abs(Opacity - opacity) >= .001)
				Opacity = opacity;
			if(_tier == tier && Math.Abs(_size - size) < .001 && ReferenceEquals(_color, color) && _scheduled == scheduled && _style == style) return;
			if(Math.Abs(_size - size) >= .001)
			{
				var transform = new ScaleTransform(size / 32, size / 32);
				if(transform.CanFreeze) transform.Freeze();
				_scaleTransform = transform;
			}
			_tier = tier;
			_size = size;
			_color = color;
			_scheduled = scheduled;
			_style = style;
			Width = Height = size;
			InvalidateVisual();
		}

		protected override void OnRender(DrawingContext context)
		{
			base.OnRender(context);
			context.PushTransform(_scaleTransform);
			if(_style == TavernTierStyle.Dice || _style == TavernTierStyle.DiceNumber)
			{
				DrawDice(context);
				context.Pop();
				return;
			}
			if(_style == TavernTierStyle.Hexagon)
			{
				context.DrawGeometry(Brushes.Black, OuterBlackPen, HexagonGeometry);
				context.DrawGeometry(_color, _scheduled ? GoldBorderPen : DarkGoldBorderPen, HexagonGeometry);
				DrawNumber(context);
				context.Pop();
				return;
			}
			context.DrawGeometry(Brushes.Black, OuterBlackPen, ShieldGeometry);
			context.DrawGeometry(ShieldBackingBrush, _scheduled ? GoldBorderPen : DarkGoldBorderPen, ShieldGeometry);
			context.PushOpacity(.75);
			context.DrawGeometry(_color, null, ShieldGeometry);
			context.Pop();
			if(_tier >= 1 && _tier <= TierStars.Length)
			{
				foreach(var star in TierStars[_tier - 1])
					context.DrawGeometry(Brushes.Gold, StarBorderPen, star);
			}
			else
				DrawNumber(context);
			context.Pop();
		}

		private void DrawNumber(DrawingContext context, Brush? foreground = null)
		{
			var text = new FormattedText(_tier.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
				BadgeTypeface, 17, foreground ?? Brushes.Gold, VisualTreeHelper.GetDpi(this).PixelsPerDip);
			context.DrawText(text, new Point((32 - text.Width) / 2, (32 - text.Height) / 2));
		}

		private void DrawDice(DrawingContext context)
		{
			var bounds = new Rect(2, 2, 28, 28);
			context.DrawRoundedRectangle(Brushes.Black, OuterBlackPen, bounds, 5, 5);
			context.DrawRoundedRectangle(Brushes.Black, _scheduled ? GoldBorderPen : GrayBorderPen, bounds, 5, 5);
			context.PushOpacity(.75);
			context.DrawRoundedRectangle(_color, null, bounds, 5, 5);
			context.Pop();
			if(_style == TavernTierStyle.DiceNumber || _tier > 7) { DrawNumber(context, Brushes.WhiteSmoke); return; }
			if(_tier % 2 == 1) DrawPip(context, 16, 16);
			if(_tier >= 2) { DrawPip(context, 9, 9); DrawPip(context, 23, 23); }
			if(_tier >= 4) { DrawPip(context, 23, 9); DrawPip(context, 9, 23); }
			if(_tier >= 6) { DrawPip(context, 9, 16); DrawPip(context, 23, 16); }
		}

		private static void DrawPip(DrawingContext context, double x, double y)
			=> context.DrawEllipse(Brushes.WhiteSmoke, PipBorderPen, new Point(x, y), 2.6, 2.6);

		private static Geometry[][] BuildTierStars()
		{
			int[][] layouts =
			{
				new[] { 1 },
				new[] { 2 },
				new[] { 2, 1 },
				new[] { 2, 2 },
				new[] { 2, 3 },
				new[] { 3, 3 },
				new[] { 2, 3, 2 }
			};
			var result = new Geometry[layouts.Length][];
			for(var t = 0; t < layouts.Length; t++)
			{
				var rows = layouts[t];
				var stars = new System.Collections.Generic.List<Geometry>();
				for(var row = 0; row < rows.Length; row++)
				for(var col = 0; col < rows[row]; col++)
					stars.Add(CreateStarGeometry(16 + (col - (rows[row] - 1) / 2d) * 7, 16 + (row - (rows.Length - 1) / 2d) * 7));
				result[t] = stars.ToArray();
			}
			return result;
		}

		private static Geometry CreateStarGeometry(double x, double y)
		{
			var star = new StreamGeometry();
			using(var path = star.Open())
			for(var i = 0; i < 10; i++)
			{
				var angle = -Math.PI / 2 + i * Math.PI / 5;
				var radius = i % 2 == 0 ? 4 : 1.8;
				var point = new Point(x + Math.Cos(angle) * radius, y + Math.Sin(angle) * radius);
				if(i == 0) path.BeginFigure(point, true, true); else path.LineTo(point, true, false);
			}
			star.Freeze();
			return star;
		}

		private static Geometry FrozenGeometry(string data)
		{
			var geometry = Geometry.Parse(data);
			geometry.Freeze();
			return geometry;
		}

		private static Brush FrozenBrush(Color color)
		{
			var brush = new SolidColorBrush(color);
			brush.Freeze();
			return brush;
		}

		private static Pen FrozenPen(Brush brush, double thickness)
		{
			var pen = new Pen(brush, thickness);
			pen.Freeze();
			return pen;
		}
	}
}
