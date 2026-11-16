using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hearthstone_Deck_Tracker;
using HdtApi = Hearthstone_Deck_Tracker.API.Core;

namespace OpponentMemory
{
	public sealed class OpponentMemoryOverlay
	{
		private readonly Canvas _canvas = new Canvas { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
		private readonly Dictionary<int, OverlayLabel> _countRows = new Dictionary<int, OverlayLabel>();
		private readonly Dictionary<int, OverlayLabel> _damageRows = new Dictionary<int, OverlayLabel>();
		private readonly Dictionary<int, TavernTierBadge> _tierRows = new Dictionary<int, TavernTierBadge>();
		private string? _styleKey;
		private FontFamily _fontFamily = new FontFamily("Segoe UI");
		private Brush _normalBrush = Brushes.Green;
		private Brush _tierBackgroundBrush = Brushes.Purple;
		private Brush _lastBrush = Brushes.Red;
		private Brush _tierLastBrush = Brushes.Red;
		private Brush _winBrush = Brushes.Blue;
		private Brush _tierWinBrush = Brushes.Blue;
		private Brush _lossBrush = Brushes.Red;
		private Brush _tierLossBrush = Brushes.Red;
		private Brush _drawBrush = Brushes.Yellow;
		private Brush _tierDrawBrush = Brushes.Yellow;
		private Brush _backgroundBrush = Brushes.Transparent;
		private bool _attached;

		public void Attach()
		{
			if(_attached) return;
			HdtApi.OverlayCanvas.Children.Add(_canvas);
			_attached = true;
		}

		public void Detach()
		{
			if(!_attached) return;
			StopTierPulses();
			HdtApi.OverlayCanvas.Children.Remove(_canvas);
			_canvas.Children.Clear();
			_countRows.Clear();
			_damageRows.Clear();
			_tierRows.Clear();
			_canvas.Visibility = Visibility.Collapsed;
			_attached = false;
		}

		public void Hide()
		{
			StopTierPulses();
			_canvas.Visibility = Visibility.Collapsed;
		}

		public void Update(
			IReadOnlyList<LeaderboardPlayer> players,
			EncounterTracker tracker,
			TavernTierPulseTracker pulseTracker,
			CombatOutcome lastCombatOutcome,
			int? lastCombatDamage,
			OpponentMemorySettings settings,
			int? scheduledOpponentId,
			DateTime nowUtc)
		{
			var overlayCanvas = HdtApi.OverlayCanvas;
			var width = overlayCanvas.ActualWidth;
			var height = overlayCanvas.ActualHeight;
			if(!_attached || !settings.Enabled || width <= 0 || height <= 0 || double.IsNaN(width) || double.IsNaN(height) || double.IsInfinity(width) || double.IsInfinity(height))
			{
				Hide();
				return;
			}
			settings.Normalize();
			if(players.Count == 0 || players.Select(player => player.LeaderboardPlace).Distinct().Count() != players.Count || !players.Any(player => player.IsLocalPlayer))
			{
				Hide();
				return;
			}

			UpdateStyleCache(settings);
			var activeIds = new HashSet<int>();
			var tileHeight = height * .69 / 8d;
			var screenRatio = (4d / 3d) / (width / height);
			var portraitLeft = width * screenRatio * .007 + width * (1 - screenRatio) / 2d;
			foreach(var player in players)
			{
				activeIds.Add(player.PlayerId);
				var rowIndex = player.LeaderboardPlace - 1;
				var isLastOpponent = player.PlayerId == tracker.LastCompletedOpponentPlayerId;
				var isScheduledOpponent = player.PlayerId == scheduledOpponentId;
				var tierVisible = !player.IsLocalPlayer && settings.ShowOpponentTavernTier && player.TavernTier.HasValue;
				var replaceCounts = !settings.ShowEncounterCounts && tierVisible;

				var countRow = GetRow(_countRows, player.PlayerId);
				var count = tracker.GetCount(player.PlayerId);
				var showMarker = !settings.ShowEncounterCounts && !replaceCounts && settings.HighlightLastOpponent && isLastOpponent;
				var showCount = settings.ShowEncounterCounts && (settings.ShowZeroValues || count != 0);
				var countVisible = !player.IsLocalPlayer && (showCount || showMarker);
				countRow.Visibility = countVisible ? Visibility.Visible : Visibility.Collapsed;
				if(countVisible)
				{
					ConfigureRow(countRow, showMarker ? "●" : count.ToString(), settings, settings.HighlightLastOpponent && isLastOpponent ? GetLastOpponentBrush(settings, lastCombatOutcome) : _normalBrush);
					PositionRow(countRow, settings.CounterSide, portraitLeft, tileHeight, rowIndex, isScheduledOpponent, settings);
				}

				var infoSide = replaceCounts ? settings.CounterSide : settings.CounterSide == CounterSide.Left ? CounterSide.Right : CounterSide.Left;
				if(!_tierRows.TryGetValue(player.PlayerId, out var tierRow))
				{
					tierRow = new TavernTierBadge();
					_tierRows.Add(player.PlayerId, tierRow);
					_canvas.Children.Add(tierRow);
				}
				tierRow.Visibility = tierVisible ? Visibility.Visible : Visibility.Collapsed;
				if(tierVisible)
				{
					var size = settings.OpponentInfoSize * settings.Scale;
					var color = settings.HighlightLastOpponent && isLastOpponent ? GetLastOpponentBrush(settings, lastCombatOutcome, opaque: true)
						: settings.TierStyle == TavernTierStyle.Dice || settings.TierStyle == TavernTierStyle.DiceNumber ? Brushes.Black : _tierBackgroundBrush;
					var isPulsing = settings.PulseOnTavernTierUpgrade && pulseTracker.IsPulsing(player.PlayerId, nowUtc);
					tierRow.Configure(player.TavernTier!.Value, size, settings.TextOpacity / 100d, color, isScheduledOpponent, settings.TierStyle, isPulsing, settings.StrongTavernTierPulse);
					var left = infoSide == CounterSide.Left ? portraitLeft - settings.HorizontalOffset - size : portraitLeft + tileHeight + settings.HorizontalOffset;
					left += rowIndex * settings.PerRowHorizontalOffset;
					if(infoSide == CounterSide.Right && isScheduledOpponent) left += settings.NextOpponentExtraOffset;
					Canvas.SetLeft(tierRow, left);
					Canvas.SetTop(tierRow, settings.VerticalOffset + tileHeight * rowIndex + (tileHeight - size) / 2 + height * .15);
				}
				else
					tierRow.StopPulse();

				var damageRow = GetRow(_damageRows, player.PlayerId);
				var damageVisible = !player.IsLocalPlayer
					&& settings.ShowLastCombatDamage
					&& isLastOpponent
					&& lastCombatOutcome != CombatOutcome.Unknown
					&& lastCombatDamage.HasValue;
				damageRow.Visibility = damageVisible ? Visibility.Visible : Visibility.Collapsed;
				if(damageVisible)
				{
					ConfigureRow(damageRow, lastCombatDamage.GetValueOrDefault().ToString(), settings, GetCombatOutcomeBrush(lastCombatOutcome));
					var damageSide = settings.CounterSide == CounterSide.Left ? CounterSide.Right : CounterSide.Left;
					PositionRow(damageRow, damageSide, portraitLeft, tileHeight, rowIndex, isScheduledOpponent, settings);
					if(tierVisible && !replaceCounts)
						Canvas.SetLeft(damageRow, Canvas.GetLeft(damageRow) + (damageSide == CounterSide.Left ? -1 : 1) * (settings.OpponentInfoSize + settings.OpponentInfoSpacing) * settings.Scale);
				}
			}

			HideUnused(_countRows, activeIds);
			HideUnused(_damageRows, activeIds);
			HideUnused(_tierRows, activeIds);
			_canvas.Visibility = Visibility.Visible;
		}

		private void ConfigureRow(OverlayLabel row, string text, OpponentMemorySettings settings, Brush foreground)
		{
			row.Configure(
				text,
				_fontFamily,
				settings.FontSize * settings.Scale,
				settings.BoldText ? FontWeights.Bold : FontWeights.Normal,
				foreground,
				_backgroundBrush,
				settings.TextStyle);
			row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		}

		private static void PositionRow(OverlayLabel row, CounterSide side, double portraitLeft, double tileHeight, int rowIndex, bool isScheduledOpponent, OpponentMemorySettings settings)
		{
			var top = settings.VerticalOffset + tileHeight * rowIndex + (tileHeight - row.DesiredSize.Height) / 2d;
			top += HdtApi.OverlayCanvas.ActualHeight * .15;
			var left = side == CounterSide.Left
				? portraitLeft - settings.HorizontalOffset - row.DesiredSize.Width
				: portraitLeft + tileHeight + settings.HorizontalOffset;
			left += rowIndex * settings.PerRowHorizontalOffset;
			if(side == CounterSide.Right && isScheduledOpponent)
				left += settings.NextOpponentExtraOffset;
			Canvas.SetTop(row, top);
			Canvas.SetLeft(row, left);
		}

		private void UpdateStyleCache(OpponentMemorySettings settings)
		{
			var styleKey = string.Join("|", settings.FontFamily, settings.NormalTextColor, settings.LastOpponentTextColor, settings.WinTextColor, settings.LossTextColor, settings.DrawTextColor, settings.BackgroundColor, settings.TextOpacity, settings.BackgroundOpacity, settings.TierBackgroundColor);
			if(string.Equals(styleKey, _styleKey, StringComparison.Ordinal))
				return;
			_styleKey = styleKey;
			var textOpacity = settings.TextOpacity / 100d;
			_tierBackgroundBrush = GetBrush(settings.TierBackgroundColor, Color.FromRgb(104, 35, 111), 1);
			_fontFamily = new FontFamily(settings.FontFamily);
			_normalBrush = GetBrush(settings.NormalTextColor, Colors.Green, textOpacity);
			_lastBrush = GetBrush(settings.LastOpponentTextColor, Colors.Red, textOpacity);
			_tierLastBrush = GetOpaqueBrush(_lastBrush);
			_winBrush = GetBrush(settings.WinTextColor, Colors.Blue, textOpacity);
			_tierWinBrush = GetOpaqueBrush(_winBrush);
			_lossBrush = GetBrush(settings.LossTextColor, Colors.Red, textOpacity);
			_tierLossBrush = GetOpaqueBrush(_lossBrush);
			_drawBrush = GetBrush(settings.DrawTextColor, Colors.Yellow, textOpacity);
			_tierDrawBrush = GetOpaqueBrush(_drawBrush);
			_backgroundBrush = GetBrush(settings.BackgroundColor, Colors.Transparent, settings.BackgroundOpacity / 100d);
		}

		private Brush GetLastOpponentBrush(OpponentMemorySettings settings, CombatOutcome outcome, bool opaque = false)
			=> settings.ColorLastOpponentByCombatResult ? GetCombatOutcomeBrush(outcome, opaque) : (opaque ? _tierLastBrush : _lastBrush);

		private Brush GetCombatOutcomeBrush(CombatOutcome outcome, bool opaque = false)
		{
			switch(outcome)
			{
				case CombatOutcome.Win: return opaque ? _tierWinBrush : _winBrush;
				case CombatOutcome.Loss: return opaque ? _tierLossBrush : _lossBrush;
				case CombatOutcome.Draw: return opaque ? _tierDrawBrush : _drawBrush;
				default: return opaque ? _tierLastBrush : _lastBrush;
			}
		}

		private OverlayLabel GetRow(IDictionary<int, OverlayLabel> rows, int playerId)
		{
			if(rows.TryGetValue(playerId, out var row)) return row;
			row = new OverlayLabel();
			rows.Add(playerId, row);
			_canvas.Children.Add(row);
			return row;
		}

		private static void HideUnused<TElement>(IEnumerable<KeyValuePair<int, TElement>> rows, ISet<int> activeIds) where TElement : UIElement
		{
			foreach(var unused in rows.Where(pair => !activeIds.Contains(pair.Key)).Select(pair => pair.Value))
			{
				if(unused is TavernTierBadge badge)
					badge.StopPulse();
				unused.Visibility = Visibility.Collapsed;
			}
		}

		private void StopTierPulses()
		{
			foreach(var badge in _tierRows.Values)
				badge.StopPulse();
		}

		private static Brush GetOpaqueBrush(Brush brush)
		{
			if(brush.Opacity >= .999 || !(brush is SolidColorBrush solid))
				return brush;
			var opaque = new SolidColorBrush(solid.Color);
			if(opaque.CanFreeze)
				opaque.Freeze();
			return opaque;
		}

		private static Brush GetBrush(string value, Color fallback, double opacity)
		{
			Color color;
			try
			{
				var converted = ColorConverter.ConvertFromString(value);
				color = converted is Color parsed ? parsed : fallback;
			}
			catch(Exception) { color = fallback; }
			var brush = new SolidColorBrush(color) { Opacity = Math.Max(0, Math.Min(1, opacity)) };
			if(brush.CanFreeze)
				brush.Freeze();
			return brush;
		}
	}
}
