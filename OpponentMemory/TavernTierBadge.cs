using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace OpponentMemory
{
	internal sealed class TavernTierBadge : FrameworkElement
	{
		private int _tier;
		private double _size;
		private Brush _color = Brushes.Purple;
		private bool _scheduled;
		private TavernTierStyle _style;
		public TavernTierBadge() => IsHitTestVisible = false;

		public void Configure(int tier, double size, double opacity, Brush color, bool scheduled, TavernTierStyle style)
		{
			Opacity = opacity;
			if(_tier == tier && Math.Abs(_size - size) < .001 && ReferenceEquals(_color, color) && _scheduled == scheduled && _style == style) return;
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
			context.PushTransform(new ScaleTransform(_size / 32, _size / 32));
			if(_style == TavernTierStyle.Dice || _style == TavernTierStyle.DiceNumber)
			{
				DrawDice(context);
				context.Pop();
				return;
			}
			if(_style == TavernTierStyle.Hexagon)
			{
				var hexagon = Geometry.Parse("M 8,2 L 24,2 L 31,16 L 24,30 L 8,30 L 1,16 Z");
				context.DrawGeometry(Brushes.Black, new Pen(Brushes.Black, 3), hexagon);
				context.DrawGeometry(_color, new Pen(_scheduled ? Brushes.Gold : Brushes.DarkGoldenrod, _scheduled ? 2.5 : 1.5), hexagon);
				DrawNumber(context);
				context.Pop();
				return;
			}
			var shield = Geometry.Parse("M 3,3 L 9,2 L 16,3 L 23,2 L 29,3 L 27,9 L 26,24 Q 22,28 16,30 Q 10,28 6,24 L 5,9 Z");
			context.DrawGeometry(Brushes.Black, new Pen(Brushes.Black, 3), shield);
			context.DrawGeometry(new SolidColorBrush(Color.FromRgb(40, 18, 43)), new Pen(_scheduled ? Brushes.Gold : Brushes.DarkGoldenrod, _scheduled ? 2.5 : 1.5), shield);
			context.PushOpacity(.65);
			context.DrawGeometry(_color, null, shield);
			context.Pop();
			int[] rows;
			switch(_tier)
			{
				case 1: rows = new[] { 1 }; break;
				case 2: rows = new[] { 2 }; break;
				case 3: rows = new[] { 2, 1 }; break;
				case 4: rows = new[] { 2, 2 }; break;
				case 5: rows = new[] { 2, 3 }; break;
				case 6: rows = new[] { 3, 3 }; break;
				case 7: rows = new[] { 2, 3, 2 }; break;
				default: rows = Array.Empty<int>(); break;
			}
			for(var row = 0; row < rows.Length; row++)
			for(var col = 0; col < rows[row]; col++)
				DrawStar(context, 16 + (col - (rows[row] - 1) / 2d) * 7, 16 + (row - (rows.Length - 1) / 2d) * 7);
			if(rows.Length == 0)
				DrawNumber(context);
			context.Pop();
		}

		private void DrawNumber(DrawingContext context, Brush? foreground = null)
		{
			var text = new FormattedText(_tier.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
				new Typeface("Segoe UI"), 17, foreground ?? Brushes.Gold, VisualTreeHelper.GetDpi(this).PixelsPerDip);
			context.DrawText(text, new Point((32 - text.Width) / 2, (32 - text.Height) / 2));
		}

		private void DrawDice(DrawingContext context)
		{
			var bounds = new Rect(2, 2, 28, 28);
			context.DrawRoundedRectangle(Brushes.Black, new Pen(Brushes.Black, 3), bounds, 5, 5);
			context.DrawRoundedRectangle(Brushes.Black, new Pen(_scheduled ? Brushes.Gold : Brushes.Gray, _scheduled ? 2.5 : 1.5), bounds, 5, 5);
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
			=> context.DrawEllipse(Brushes.WhiteSmoke, new Pen(Brushes.Black, .6), new Point(x, y), 2.6, 2.6);

		private static void DrawStar(DrawingContext context, double x, double y)
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
			context.DrawGeometry(Brushes.Gold, new Pen(Brushes.Black, .55), star);
		}
	}
}
