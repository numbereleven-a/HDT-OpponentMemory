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
		private readonly Dictionary<int, TextBlock> _countRows = new Dictionary<int, TextBlock>();
		private readonly Dictionary<int, TextBlock> _damageRows = new Dictionary<int, TextBlock>();
		private string? _styleKey;
		private FontFamily _fontFamily = new FontFamily("Segoe UI");
		private Brush _normalBrush = Brushes.Green;
		private Brush _lastBrush = Brushes.Red;
		private Brush _winBrush = Brushes.Blue;
		private Brush _lossBrush = Brushes.Red;
		private Brush _drawBrush = Brushes.Yellow;
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
			HdtApi.OverlayCanvas.Children.Remove(_canvas);
			_canvas.Children.Clear();
			_countRows.Clear();
			_damageRows.Clear();
			_canvas.Visibility = Visibility.Collapsed;
			_attached = false;
		}

		public void Hide() => _canvas.Visibility = Visibility.Collapsed;

		public void Update(
			IReadOnlyList<LeaderboardPlayer> players,
			EncounterTracker tracker,
			CombatOutcome lastCombatOutcome,
			int? lastCombatDamage,
			OpponentMemorySettings settings,
			int? scheduledOpponentId)
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

				var countRow = GetRow(_countRows, player.PlayerId);
				var count = tracker.GetCount(player.PlayerId);
				var showMarker = !settings.ShowEncounterCounts && settings.HighlightLastOpponent && isLastOpponent;
				var showCount = settings.ShowEncounterCounts && (settings.ShowZeroValues || count != 0);
				var countVisible = !player.IsLocalPlayer && (showCount || showMarker);
				countRow.Visibility = countVisible ? Visibility.Visible : Visibility.Collapsed;
				if(countVisible)
				{
					countRow.Text = showMarker ? "●" : count.ToString();
					ConfigureRow(countRow, settings, settings.HighlightLastOpponent && isLastOpponent ? GetLastOpponentBrush(settings, lastCombatOutcome) : _normalBrush);
					PositionRow(countRow, settings.CounterSide, portraitLeft, tileHeight, rowIndex, isScheduledOpponent, settings);
				}

				var damageRow = GetRow(_damageRows, player.PlayerId);
				var damageVisible = !player.IsLocalPlayer
					&& settings.ShowLastCombatDamage
					&& isLastOpponent
					&& lastCombatOutcome != CombatOutcome.Unknown
					&& lastCombatDamage.HasValue;
				damageRow.Visibility = damageVisible ? Visibility.Visible : Visibility.Collapsed;
				if(damageVisible)
				{
					damageRow.Text = lastCombatDamage.GetValueOrDefault().ToString();
					ConfigureRow(damageRow, settings, GetCombatOutcomeBrush(lastCombatOutcome));
					var damageSide = settings.CounterSide == CounterSide.Left ? CounterSide.Right : CounterSide.Left;
					PositionRow(damageRow, damageSide, portraitLeft, tileHeight, rowIndex, isScheduledOpponent, settings);
				}
			}

			HideUnused(_countRows, activeIds);
			HideUnused(_damageRows, activeIds);
			_canvas.Visibility = Visibility.Visible;
		}

		private void ConfigureRow(TextBlock row, OpponentMemorySettings settings, Brush foreground)
		{
			row.FontFamily = _fontFamily;
			row.FontSize = settings.FontSize * settings.Scale;
			row.FontWeight = settings.BoldText ? FontWeights.Bold : FontWeights.Normal;
			row.Opacity = 1;
			row.Foreground = foreground;
			row.Background = _backgroundBrush;
			row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		}

		private static void PositionRow(TextBlock row, CounterSide side, double portraitLeft, double tileHeight, int rowIndex, bool isScheduledOpponent, OpponentMemorySettings settings)
		{
			var top = settings.VerticalOffset + tileHeight * rowIndex + (tileHeight - row.FontSize) / 2d;
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
			var styleKey = string.Join("|", settings.FontFamily, settings.NormalTextColor, settings.LastOpponentTextColor, settings.WinTextColor, settings.LossTextColor, settings.DrawTextColor, settings.BackgroundColor, settings.TextOpacity, settings.BackgroundOpacity);
			if(string.Equals(styleKey, _styleKey, StringComparison.Ordinal))
				return;
			_styleKey = styleKey;
			_fontFamily = new FontFamily(settings.FontFamily);
			_normalBrush = GetBrush(settings.NormalTextColor, Colors.Green, settings.TextOpacity / 100d);
			_lastBrush = GetBrush(settings.LastOpponentTextColor, Colors.Red, settings.TextOpacity / 100d);
			_winBrush = GetBrush(settings.WinTextColor, Colors.Blue, settings.TextOpacity / 100d);
			_lossBrush = GetBrush(settings.LossTextColor, Colors.Red, settings.TextOpacity / 100d);
			_drawBrush = GetBrush(settings.DrawTextColor, Colors.Yellow, settings.TextOpacity / 100d);
			_backgroundBrush = GetBrush(settings.BackgroundColor, Colors.Transparent, settings.BackgroundOpacity / 100d);
		}

		private Brush GetLastOpponentBrush(OpponentMemorySettings settings, CombatOutcome outcome)
			=> settings.ColorLastOpponentByCombatResult ? GetCombatOutcomeBrush(outcome) : _lastBrush;

		private Brush GetCombatOutcomeBrush(CombatOutcome outcome)
		{
			switch(outcome)
			{
				case CombatOutcome.Win: return _winBrush;
				case CombatOutcome.Loss: return _lossBrush;
				case CombatOutcome.Draw: return _drawBrush;
				default: return _lastBrush;
			}
		}

		private TextBlock GetRow(IDictionary<int, TextBlock> rows, int playerId)
		{
			if(rows.TryGetValue(playerId, out var row)) return row;
			row = new TextBlock { TextAlignment = TextAlignment.Center, Padding = new Thickness(3, 0, 3, 0), IsHitTestVisible = false };
			rows.Add(playerId, row);
			_canvas.Children.Add(row);
			return row;
		}

		private static void HideUnused(IEnumerable<KeyValuePair<int, TextBlock>> rows, ISet<int> activeIds)
		{
			foreach(var unused in rows.Where(pair => !activeIds.Contains(pair.Key)).Select(pair => pair.Value))
				unused.Visibility = Visibility.Collapsed;
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
