using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OpponentMemory
{
	internal sealed class TavernTierBadge : Canvas
	{
		private enum PulseState
		{
			Idle,
			Pulsing,
			Finishing
		}

		private sealed class RenderLayer : FrameworkElement
		{
			private readonly Action<DrawingContext> _render;

			public RenderLayer(Action<DrawingContext> render)
			{
				_render = render;
				IsHitTestVisible = false;
			}

			protected override void OnRender(DrawingContext context)
			{
				base.OnRender(context);
				_render(context);
			}
		}

		private static readonly Geometry HexagonGeometry = FrozenGeometry("M 8,2 L 24,2 L 31,16 L 24,30 L 8,30 L 1,16 Z");
		private static readonly Geometry ShieldGeometry = FrozenGeometry("M 3,3 L 9,2 L 16,3 L 23,2 L 29,3 L 27,9 L 26,24 Q 22,28 16,30 Q 10,28 6,24 L 5,9 Z");
		private static readonly Typeface BadgeTypeface = new Typeface("Segoe UI");
		private static readonly Brush ShieldBackingBrush = FrozenBrush(Color.FromRgb(24, 24, 24));
		private static readonly Brush StrongFlashBrush = FrozenBrush(Color.FromArgb(125, 255, 240, 155));
		private static readonly Pen OuterBlackPen = FrozenPen(Brushes.Black, 3);
		private static readonly Pen GoldBorderPen = FrozenPen(Brushes.Gold, 2.5);
		private static readonly Pen DarkGoldBorderPen = FrozenPen(Brushes.DarkGoldenrod, 1.5);
		private static readonly Pen GrayBorderPen = FrozenPen(Brushes.Gray, 1.5);
		private static readonly Pen StrongHaloPen = FrozenPen(FrozenBrush(Color.FromArgb(165, 255, 215, 45)), 8);
		private static readonly Pen StrongRimPen = FrozenPen(FrozenBrush(Color.FromArgb(255, 255, 248, 195)), 2.8);
		private static readonly Pen StarBorderPen = FrozenPen(Brushes.Black, .55);
		private static readonly Pen PipBorderPen = FrozenPen(Brushes.Black, .6);
		private static readonly Geometry[][] TierStars = BuildTierStars();
		private static readonly IEasingFunction PulseEase = FrozenEase();
		private static readonly TimeSpan StandardHalfDuration = TimeSpan.FromMilliseconds(350);
		private static readonly TimeSpan StandardFullDuration = TimeSpan.FromMilliseconds(700);
		private static readonly TimeSpan StrongHalfDuration = TimeSpan.FromMilliseconds(275);
		private static readonly TimeSpan StrongFullDuration = TimeSpan.FromMilliseconds(550);
		private static readonly DoubleAnimationUsingKeyFrames StandardScaleLoop = CreatePulseTimeline(1, 1.28, StandardHalfDuration, true);
		private static readonly DoubleAnimationUsingKeyFrames StandardScaleFinish = CreatePulseTimeline(1, 1.28, StandardHalfDuration, false);
		private static readonly DoubleAnimationUsingKeyFrames StrongScaleLoop = CreatePulseTimeline(1, 1.50, StrongHalfDuration, true);
		private static readonly DoubleAnimationUsingKeyFrames StrongScaleFinish = CreatePulseTimeline(1, 1.50, StrongHalfDuration, false);
		private static readonly DoubleAnimationUsingKeyFrames StrongGlowLoop = CreatePulseTimeline(0, 1, StrongHalfDuration, true);
		private static readonly DoubleAnimationUsingKeyFrames StrongGlowFinish = CreatePulseTimeline(0, 1, StrongHalfDuration, false);
		private readonly ScaleTransform _pulseTransform = new ScaleTransform(1, 1);
		private readonly RenderLayer _badgeLayer;
		private readonly RenderLayer _glowLayer;
		private int _tier;
		private double _size;
		private double _baseOpacity = 1;
		private Brush _color = Brushes.Purple;
		private bool _scheduled;
		private PulseState _pulseState;
		private bool _pulseStrong;
		private TavernTierStyle _style;
		private ScaleTransform _scaleTransform = Transform.Identity as ScaleTransform ?? new ScaleTransform(1, 1);
		private AnimationClock? _scaleXClock;
		private AnimationClock? _scaleYClock;
		private AnimationClock? _opacityClock;
		private AnimationClock? _glowClock;
		private AnimationClock? _finishClock;
		private double _cachedOpacityValue = double.NaN;
		private DoubleAnimationUsingKeyFrames? _cachedOpacityLoop;
		private DoubleAnimationUsingKeyFrames? _cachedOpacityFinish;

		public TavernTierBadge()
		{
			IsHitTestVisible = false;
			ClipToBounds = false;
			RenderTransformOrigin = new Point(.5, .5);
			RenderTransform = _pulseTransform;
			_badgeLayer = new RenderLayer(RenderBadge);
			_glowLayer = new RenderLayer(RenderGlow) { Opacity = 0, Visibility = Visibility.Collapsed };
			Children.Add(_badgeLayer);
			Children.Add(_glowLayer);
		}

		public void Configure(int tier, double size, double opacity, Brush color, bool scheduled, TavernTierStyle style, bool isPulsing = false, bool strongPulse = false)
		{
			UpdatePulse(opacity, isPulsing, strongPulse);
			if(_tier == tier && Math.Abs(_size - size) < .001 && ReferenceEquals(_color, color) && _scheduled == scheduled && _style == style) return;
			if(Math.Abs(_size - size) >= .001)
			{
				var transform = new ScaleTransform(size / 32, size / 32);
				if(transform.CanFreeze) transform.Freeze();
				_scaleTransform = transform;
				Width = Height = size;
				_badgeLayer.Width = _badgeLayer.Height = size;
				_glowLayer.Width = _glowLayer.Height = size;
			}
			_tier = tier;
			_size = size;
			_color = color;
			_scheduled = scheduled;
			_style = style;
			_badgeLayer.InvalidateVisual();
			_glowLayer.InvalidateVisual();
		}

		public void StopPulse()
		{
			if(_pulseState == PulseState.Idle)
				return;
			ClearPulseClocks();
			_pulseState = PulseState.Idle;
			_pulseStrong = false;
			_pulseTransform.ScaleX = 1;
			_pulseTransform.ScaleY = 1;
			Opacity = _baseOpacity;
			_glowLayer.Opacity = 0;
			_glowLayer.Visibility = Visibility.Collapsed;
		}

		private void UpdatePulse(double opacity, bool isPulsing, bool strongPulse)
		{
			var opacityChanged = Math.Abs(_baseOpacity - opacity) >= .001;
			_baseOpacity = opacity;

			if(!isPulsing)
			{
				if(_pulseState == PulseState.Pulsing)
					FinishPulse();
				else if(_pulseState == PulseState.Finishing && opacityChanged)
					UpdateFinishingOpacity();
				else if(_pulseState == PulseState.Idle && Math.Abs(Opacity - opacity) >= .001)
					Opacity = opacity;
				return;
			}

			if(_pulseState == PulseState.Idle || _pulseStrong != strongPulse)
			{
				ApplyPulseAnimation(strongPulse, true, TimeSpan.Zero);
				return;
			}

			if(_pulseState == PulseState.Finishing)
			{
				var cycleTime = GetCycleTime(GetFullDuration(strongPulse));
				ApplyPulseAnimation(strongPulse, true, cycleTime);
				return;
			}

			if(opacityChanged)
			{
				if(_pulseStrong)
					Opacity = _baseOpacity;
				else
					ReplaceOpacityClock(true, GetCycleTime(StandardFullDuration));
			}
		}

		private void FinishPulse()
		{
			var cycleTime = GetCycleTime(GetFullDuration(_pulseStrong));
			if(cycleTime <= TimeSpan.Zero)
			{
				StopPulse();
				return;
			}
			ApplyPulseAnimation(_pulseStrong, false, cycleTime);
		}

		private void UpdateFinishingOpacity()
		{
			if(_pulseStrong)
			{
				Opacity = _baseOpacity;
				return;
			}
			var cycleTime = GetCycleTime(StandardFullDuration);
			if(cycleTime <= TimeSpan.Zero)
			{
				StopPulse();
				return;
			}
			ReplaceOpacityClock(false, cycleTime);
		}

		private void ApplyPulseAnimation(bool strongPulse, bool repeatForever, TimeSpan seekTime)
		{
			ClearPulseClocks();
			_pulseStrong = strongPulse;
			_pulseState = repeatForever ? PulseState.Pulsing : PulseState.Finishing;
			_pulseTransform.ScaleX = 1;
			_pulseTransform.ScaleY = 1;
			Opacity = _baseOpacity;

			var scaleTimeline = strongPulse
				? (repeatForever ? StrongScaleLoop : StrongScaleFinish)
				: (repeatForever ? StandardScaleLoop : StandardScaleFinish);
			_scaleXClock = ApplyClock(_pulseTransform, ScaleTransform.ScaleXProperty, scaleTimeline, seekTime);
			_scaleYClock = ApplyClock(_pulseTransform, ScaleTransform.ScaleYProperty, scaleTimeline, seekTime);

			if(strongPulse)
			{
				_glowLayer.Opacity = 0;
				_glowLayer.Visibility = Visibility.Visible;
				_glowClock = ApplyClock(_glowLayer, OpacityProperty, repeatForever ? StrongGlowLoop : StrongGlowFinish, seekTime);
			}
			else
			{
				_glowLayer.Opacity = 0;
				_glowLayer.Visibility = Visibility.Collapsed;
				_opacityClock = ApplyClock(this, OpacityProperty, GetOpacityAnimation(_baseOpacity, repeatForever), seekTime);
			}

			if(!repeatForever)
			{
				_finishClock = _scaleXClock;
				_finishClock.Completed += OnFinishCompleted;
				if(_finishClock.CurrentState != ClockState.Active)
					StopPulse();
			}
		}

		private void ReplaceOpacityClock(bool repeatForever, TimeSpan seekTime)
		{
			if(_opacityClock != null)
			{
				ApplyAnimationClock(OpacityProperty, null);
				_opacityClock = null;
			}
			Opacity = _baseOpacity;
			_opacityClock = ApplyClock(this, OpacityProperty, GetOpacityAnimation(_baseOpacity, repeatForever), seekTime);
		}

		private void ClearPulseClocks()
		{
			if(_finishClock != null)
			{
				_finishClock.Completed -= OnFinishCompleted;
				_finishClock = null;
			}
			if(_scaleXClock != null)
			{
				_pulseTransform.ApplyAnimationClock(ScaleTransform.ScaleXProperty, null);
				_scaleXClock = null;
			}
			if(_scaleYClock != null)
			{
				_pulseTransform.ApplyAnimationClock(ScaleTransform.ScaleYProperty, null);
				_scaleYClock = null;
			}
			if(_opacityClock != null)
			{
				ApplyAnimationClock(OpacityProperty, null);
				_opacityClock = null;
			}
			if(_glowClock != null)
			{
				_glowLayer.ApplyAnimationClock(OpacityProperty, null);
				_glowClock = null;
			}
		}

		private void OnFinishCompleted(object? sender, EventArgs args)
		{
			if(!ReferenceEquals(sender, _finishClock) || _pulseState != PulseState.Finishing)
				return;
			StopPulse();
		}

		private TimeSpan GetCycleTime(TimeSpan fullDuration)
		{
			var current = _scaleXClock?.CurrentTime;
			if(!current.HasValue || current.Value <= TimeSpan.Zero || current.Value >= fullDuration)
				return TimeSpan.Zero;
			return current.Value;
		}

		private static TimeSpan GetFullDuration(bool strongPulse)
			=> strongPulse ? StrongFullDuration : StandardFullDuration;

		private static AnimationClock ApplyClock(IAnimatable target, DependencyProperty property, AnimationTimeline timeline, TimeSpan seekTime)
		{
			var clock = timeline.CreateClock();
			target.ApplyAnimationClock(property, clock);
			if(seekTime > TimeSpan.Zero)
				clock.Controller?.SeekAlignedToLastTick(seekTime, TimeSeekOrigin.BeginTime);
			return clock;
		}

		private DoubleAnimationUsingKeyFrames GetOpacityAnimation(double baseOpacity, bool repeatForever)
		{
			if(Math.Abs(_cachedOpacityValue - baseOpacity) >= .001 || _cachedOpacityLoop == null || _cachedOpacityFinish == null)
			{
				_cachedOpacityValue = baseOpacity;
				_cachedOpacityLoop = CreatePulseTimeline(baseOpacity, baseOpacity * .25, StandardHalfDuration, true);
				_cachedOpacityFinish = CreatePulseTimeline(baseOpacity, baseOpacity * .25, StandardHalfDuration, false);
			}
			return repeatForever ? _cachedOpacityLoop : _cachedOpacityFinish;
		}

		private static IEasingFunction FrozenEase()
		{
			var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
			ease.Freeze();
			return ease;
		}

		private static DoubleAnimationUsingKeyFrames CreatePulseTimeline(double restValue, double peakValue, TimeSpan halfDuration, bool repeatForever)
		{
			var fullDuration = TimeSpan.FromTicks(halfDuration.Ticks * 2);
			var animation = new DoubleAnimationUsingKeyFrames
			{
				Duration = new Duration(fullDuration),
				RepeatBehavior = repeatForever ? RepeatBehavior.Forever : new RepeatBehavior(1),
				FillBehavior = FillBehavior.Stop
			};
			animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(restValue, KeyTime.FromTimeSpan(TimeSpan.Zero)));
			animation.KeyFrames.Add(new EasingDoubleKeyFrame(peakValue, KeyTime.FromTimeSpan(halfDuration), PulseEase));
			animation.KeyFrames.Add(new EasingDoubleKeyFrame(restValue, KeyTime.FromTimeSpan(fullDuration), PulseEase));
			animation.Freeze();
			return animation;
		}

		private void RenderBadge(DrawingContext context)
		{
			if(_size <= 0)
				return;
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

		private void RenderGlow(DrawingContext context)
		{
			if(_size <= 0)
				return;
			context.PushTransform(_scaleTransform);
			if(_style == TavernTierStyle.Dice || _style == TavernTierStyle.DiceNumber)
			{
				var bounds = new Rect(2, 2, 28, 28);
				context.DrawRoundedRectangle(StrongFlashBrush, StrongHaloPen, bounds, 5, 5);
				context.DrawRoundedRectangle(null, StrongRimPen, bounds, 5, 5);
				context.Pop();
				return;
			}
			var geometry = _style == TavernTierStyle.Hexagon ? HexagonGeometry : ShieldGeometry;
			context.DrawGeometry(StrongFlashBrush, StrongHaloPen, geometry);
			context.DrawGeometry(null, StrongRimPen, geometry);
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
