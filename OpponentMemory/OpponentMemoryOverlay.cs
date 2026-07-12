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
		private readonly Dictionary<int, TextBlock> _rows = new Dictionary<int, TextBlock>();
		private string? _styleKey;
		private FontFamily _fontFamily = new FontFamily("Segoe UI");
		private Brush _normalBrush = Brushes.Green;
		private Brush _lastBrush = Brushes.Red;
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
			_rows.Clear();
			_canvas.Visibility = Visibility.Collapsed;
			_attached = false;
		}

		public void Hide() => _canvas.Visibility = Visibility.Collapsed;

		public void Update(IReadOnlyList<LeaderboardPlayer> players, EncounterTracker tracker, OpponentMemorySettings settings, int? scheduledOpponentId)
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
			foreach(var player in players)
			{
				activeIds.Add(player.PlayerId);
				var row = GetRow(player.PlayerId);
				var count = tracker.GetCount(player.PlayerId);
				var isLastOpponent = player.PlayerId == tracker.LastCompletedOpponentPlayerId;
				var showMarker = !settings.ShowEncounterCounts && settings.HighlightLastOpponent && isLastOpponent;
				var showCount = settings.ShowEncounterCounts && (settings.ShowZeroValues || count != 0);
				var visible = !player.IsLocalPlayer && (showCount || showMarker);
				row.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
				if(!visible) continue;
				row.Text = showMarker ? "●" : count.ToString();
				row.FontFamily = _fontFamily;
				row.FontSize = settings.FontSize * settings.Scale;
				row.FontWeight = settings.BoldText ? FontWeights.Bold : FontWeights.Normal;
				row.Opacity = 1;
				row.Foreground = settings.HighlightLastOpponent && isLastOpponent ? _lastBrush : _normalBrush;
				row.Background = _backgroundBrush;
				row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
				var rowWidth = row.DesiredSize.Width;
				var tileHeight = height * .69 / 8d;
				var rowIndex = player.LeaderboardPlace - 1;
				var top = height * .15 + tileHeight * rowIndex + (tileHeight - row.FontSize) / 2d + settings.VerticalOffset;
				var screenRatio = (4d / 3d) / (width / height);
				var portraitLeft = width * screenRatio * .007 + width * (1 - screenRatio) / 2d;
				var left = settings.CounterSide == CounterSide.Left
					? portraitLeft - settings.HorizontalOffset - rowWidth
					: portraitLeft + tileHeight + settings.HorizontalOffset;
				// The in-game leaderboard is rendered with a small perspective offset.
				// Apply the same correction to both sides so the counter column remains
				// parallel to the portraits instead of drifting to the right downwards.
				left += rowIndex * settings.PerRowHorizontalOffset;
				if(settings.CounterSide == CounterSide.Right && player.PlayerId == scheduledOpponentId)
					left += settings.NextOpponentExtraOffset;
				Canvas.SetTop(row, top);
				Canvas.SetLeft(row, left);
			}
			foreach(var unused in _rows.Where(pair => !activeIds.Contains(pair.Key)).Select(pair => pair.Value)) unused.Visibility = Visibility.Collapsed;
			_canvas.Visibility = Visibility.Visible;
		}

		private void UpdateStyleCache(OpponentMemorySettings settings)
		{
			var styleKey = string.Join("|", settings.FontFamily, settings.NormalTextColor, settings.LastOpponentTextColor, settings.BackgroundColor, settings.TextOpacity, settings.BackgroundOpacity);
			if(string.Equals(styleKey, _styleKey, StringComparison.Ordinal))
				return;
			_styleKey = styleKey;
			_fontFamily = new FontFamily(settings.FontFamily);
			_normalBrush = GetBrush(settings.NormalTextColor, Colors.Green, settings.TextOpacity / 100d);
			_lastBrush = GetBrush(settings.LastOpponentTextColor, Colors.Red, settings.TextOpacity / 100d);
			_backgroundBrush = GetBrush(settings.BackgroundColor, Colors.Transparent, settings.BackgroundOpacity / 100d);
		}

		private TextBlock GetRow(int playerId)
		{
			if(_rows.TryGetValue(playerId, out var row)) return row;
			row = new TextBlock { TextAlignment = TextAlignment.Center, Padding = new Thickness(3, 0, 3, 0), IsHitTestVisible = false };
			_rows.Add(playerId, row);
			_canvas.Children.Add(row);
			return row;
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
