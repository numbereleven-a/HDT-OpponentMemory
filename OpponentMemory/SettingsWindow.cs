using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpponentMemory
{
	internal sealed class SettingsWindow : Window
	{
		private readonly OpponentMemorySettings _settings;
		private readonly Action _apply;
		private OpponentMemorySettings _draft;
		private CheckBox _enabled = null!, _counts = null!, _damage = null!, _highlight = null!, _resultColors = null!, _resultColorsInColors = null!, _zeros = null!, _ghosts = null!, _bold = null!;
		private ComboBox _side = null!;
		private Slider _horizontal = null!, _perRowHorizontal = null!, _vertical = null!, _nextOffset = null!, _scale = null!, _fontSize = null!, _textOpacity = null!, _backgroundOpacity = null!;
		private ComboBox _font = null!;
		private ComboBox _normal = null!, _last = null!, _win = null!, _loss = null!, _draw = null!, _background = null!;
		private TextBlock _colorWarning = null!;

		public SettingsWindow(OpponentMemorySettings settings, Action apply)
		{
			_settings = settings;
			_apply = apply;
			_draft = Clone(settings);
			Title = "Opponent Memory settings";
			Width = 440;
			Height = 610;
			MinWidth = 440;
			MinHeight = 610;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Content = BuildContent();
		}

		private UIElement BuildContent()
		{
			var root = new Grid { Margin = new Thickness(14) };
			root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			var tabs = new TabControl();
			tabs.Items.Add(new TabItem { Header = "General", Content = BuildGeneralTab() });
			tabs.Items.Add(new TabItem { Header = "Visual", Content = BuildVisualTab() });
			tabs.Items.Add(new TabItem { Header = "Colors", Content = BuildColorsTab() });
			UpdateColorControls();
			Grid.SetRow(tabs, 0); root.Children.Add(tabs);
			var bottom = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
			bottom.Children.Add(new TextBlock { Text = "Version " + OpponentMemoryPlugin.DisplayVersion, VerticalAlignment = VerticalAlignment.Center, Opacity = .65 });
			var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
			var apply = new Button { Content = "Apply", MinWidth = 78, Margin = new Thickness(4, 0, 0, 0) }; apply.Click += (_, __) => ApplyDraft();
			var ok = new Button { Content = "OK", MinWidth = 78, Margin = new Thickness(4, 0, 0, 0) }; ok.Click += (_, __) => { ApplyDraft(); Close(); };
			var cancel = new Button { Content = "Cancel", MinWidth = 78, Margin = new Thickness(4, 0, 0, 0) }; cancel.Click += (_, __) => Close();
			buttons.Children.Add(apply); buttons.Children.Add(ok); buttons.Children.Add(cancel);
			DockPanel.SetDock(buttons, Dock.Right); bottom.Children.Add(buttons);
			Grid.SetRow(bottom, 1); root.Children.Add(bottom);
			return root;
		}

		private UIElement BuildGeneralTab()
		{
			var panel = NewPanel();
			_enabled = AddCheck(panel, "Enabled", _draft.Enabled);
			_side = new ComboBox { ItemsSource = Enum.GetValues(typeof(CounterSide)), SelectedItem = _draft.CounterSide, Margin = new Thickness(0, 0, 0, 12) };
			AddLabel(panel, "Counter side"); panel.Children.Add(_side);
			_counts = AddCheck(panel, "Show encounter counts", _draft.ShowEncounterCounts);
			_damage = AddCheck(panel, "Show last combat damage", _draft.ShowLastCombatDamage);
			_damage.Checked += (_, __) => UpdateColorControls(); _damage.Unchecked += (_, __) => UpdateColorControls();
			_highlight = AddCheck(panel, "Highlight last opponent", _draft.HighlightLastOpponent);
			_resultColors = AddCheck(panel, "Color last opponent by combat result", _draft.ColorLastOpponentByCombatResult);
			_highlight.Checked += (_, __) => UpdateColorControls(); _highlight.Unchecked += (_, __) => UpdateColorControls();
			_resultColors.Checked += (_, __) => SyncResultColorControls(_resultColors); _resultColors.Unchecked += (_, __) => SyncResultColorControls(_resultColors);
			_zeros = AddCheck(panel, "Show zero values", _draft.ShowZeroValues);
			_ghosts = AddCheck(panel, "Count ghost encounters", _draft.CountGhostEncounters);
			var defaults = new Button { Content = "Reset settings", HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 120, Margin = new Thickness(0, 4, 0, 0) };
			defaults.Click += (_, __) => { _draft = OpponentMemorySettings.CreateDefaults(); Populate(); };
			panel.Children.Add(defaults);
			panel.Children.Add(new TextBlock { Text = "Encounter data is kept only for the current match.", Opacity = .65, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });
			return new ScrollViewer { Content = panel };
		}

		private UIElement BuildVisualTab()
		{
			var panel = NewPanel();
			_horizontal = AddSlider(panel, "Horizontal offset", -80, 160, 1, _draft.HorizontalOffset);
			_perRowHorizontal = AddSlider(panel, "Per-row horizontal adjustment", -15, 15, .25, _draft.PerRowHorizontalOffset);
			_vertical = AddSlider(panel, "Vertical adjustment", -80, 80, 1, _draft.VerticalOffset);
			_nextOffset = AddSlider(panel, "Next opponent extra offset", -80, 160, 1, _draft.NextOpponentExtraOffset);
			_scale = AddSlider(panel, "Scale", 50, 200, 5, _draft.Scale * 100, "%");
			_fontSize = AddSlider(panel, "Font size", 8, 48, 1, _draft.FontSize);
			_textOpacity = AddSlider(panel, "Text opacity", 0, 100, 1, _draft.TextOpacity, "%");
			_backgroundOpacity = AddSlider(panel, "Background opacity", 0, 100, 1, _draft.BackgroundOpacity, "%");
			_font = AddFontPicker(panel, "Font", _draft.FontFamily);
			_bold = AddCheck(panel, "Bold text", _draft.BoldText);
			return new ScrollViewer { Content = panel };
		}

		private UIElement BuildColorsTab()
		{
			var panel = NewPanel();
			AddLabel(panel, "Text colors");
			_normal = AddColorPicker(panel, "Normal text color", _draft.NormalTextColor);
			_last = AddColorPicker(panel, "Last opponent text color", _draft.LastOpponentTextColor);
			_resultColorsInColors = AddCheck(panel, "Color last opponent by combat result", _draft.ColorLastOpponentByCombatResult);
			_resultColorsInColors.Checked += (_, __) => SyncResultColorControls(_resultColorsInColors); _resultColorsInColors.Unchecked += (_, __) => SyncResultColorControls(_resultColorsInColors);
			panel.Children.Add(new TextBlock { Text = "Used when combat-result coloring is off or the result is unknown.", Opacity = .65, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, -6, 0, 10) });
			panel.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 12) });
			AddLabel(panel, "Combat result colors");
			_win = AddColorPicker(panel, "Win color", _draft.WinTextColor);
			_loss = AddColorPicker(panel, "Loss color", _draft.LossTextColor);
			_draw = AddColorPicker(panel, "Draw color", _draft.DrawTextColor);
			panel.Children.Add(new TextBlock { Text = "These colors are also used for last combat damage.", Opacity = .65, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, -6, 0, 10) });
			_colorWarning = new TextBlock { Text = "Some selected colors match. Combat results or the last opponent may be difficult to distinguish.", Foreground = Brushes.DarkOrange, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
			panel.Children.Add(_colorWarning);
			panel.Children.Add(new Separator { Margin = new Thickness(0, 14, 0, 12) });
			AddLabel(panel, "Background");
			_background = AddColorPicker(panel, "Background color", _draft.BackgroundColor);
			return new ScrollViewer { Content = panel };
		}

		private void ApplyDraft()
		{
			_draft.Enabled = _enabled.IsChecked == true;
			_draft.CounterSide = _side.SelectedItem is CounterSide side ? side : CounterSide.Right;
			_draft.ShowEncounterCounts = _counts.IsChecked == true; _draft.ShowLastCombatDamage = _damage.IsChecked == true; _draft.HighlightLastOpponent = _highlight.IsChecked == true;
			_draft.ColorLastOpponentByCombatResult = _resultColors.IsChecked == true;
			_draft.ShowZeroValues = _zeros.IsChecked == true; _draft.CountGhostEncounters = _ghosts.IsChecked == true;
			_draft.HorizontalOffset = _horizontal.Value; _draft.PerRowHorizontalOffset = _perRowHorizontal.Value; _draft.VerticalOffset = _vertical.Value; _draft.NextOpponentExtraOffset = _nextOffset.Value;
			_draft.Scale = _scale.Value / 100d; _draft.FontSize = _fontSize.Value; _draft.TextOpacity = _textOpacity.Value; _draft.BackgroundOpacity = _backgroundOpacity.Value;
			_draft.FontFamily = _font.Text; _draft.BoldText = _bold.IsChecked == true; _draft.NormalTextColor = ColorValue(_normal); _draft.LastOpponentTextColor = ColorValue(_last); _draft.WinTextColor = ColorValue(_win); _draft.LossTextColor = ColorValue(_loss); _draft.DrawTextColor = ColorValue(_draw); _draft.BackgroundColor = ColorValue(_background);
			_draft.Normalize(); _settings.CopyFrom(_draft); _settings.Save(); _apply();
		}

		private void Populate()
		{
			_enabled.IsChecked = _draft.Enabled; _side.SelectedItem = _draft.CounterSide; _counts.IsChecked = _draft.ShowEncounterCounts; _damage.IsChecked = _draft.ShowLastCombatDamage; _highlight.IsChecked = _draft.HighlightLastOpponent; _resultColors.IsChecked = _draft.ColorLastOpponentByCombatResult; _resultColorsInColors.IsChecked = _draft.ColorLastOpponentByCombatResult; _zeros.IsChecked = _draft.ShowZeroValues; _ghosts.IsChecked = _draft.CountGhostEncounters;
			_horizontal.Value = _draft.HorizontalOffset; _perRowHorizontal.Value = _draft.PerRowHorizontalOffset; _vertical.Value = _draft.VerticalOffset; _nextOffset.Value = _draft.NextOpponentExtraOffset; _scale.Value = _draft.Scale * 100; _fontSize.Value = _draft.FontSize; _textOpacity.Value = _draft.TextOpacity; _backgroundOpacity.Value = _draft.BackgroundOpacity;
			_font.Text = _draft.FontFamily; _bold.IsChecked = _draft.BoldText; SelectColor(_normal, _draft.NormalTextColor); SelectColor(_last, _draft.LastOpponentTextColor); SelectColor(_win, _draft.WinTextColor); SelectColor(_loss, _draft.LossTextColor); SelectColor(_draw, _draft.DrawTextColor); SelectColor(_background, _draft.BackgroundColor); UpdateColorControls();
		}

		private static StackPanel NewPanel() => new StackPanel { Margin = new Thickness(12) };
		private static void AddLabel(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
		private static CheckBox AddCheck(Panel panel, string label, bool value) { var check = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 0, 0, 10) }; panel.Children.Add(check); return check; }
		private static TextBox AddField(Panel panel, string label, string value) { AddLabel(panel, label); var field = new TextBox { Text = value, Margin = new Thickness(0, 0, 0, 10) }; panel.Children.Add(field); return field; }
		private static ComboBox AddFontPicker(Panel panel, string label, string value)
		{
			AddLabel(panel, label);
			var installed = new System.Collections.Generic.HashSet<string>(Fonts.SystemFontFamilies.Select(font => font.Source), StringComparer.OrdinalIgnoreCase);
			var choices = PopularFonts.Where(installed.Contains).ToArray();
			var picker = new ComboBox { IsEditable = true, ItemsSource = choices, Text = value, Margin = new Thickness(0, 0, 0, 10) };
			panel.Children.Add(picker); return picker;
		}
		private static readonly string[] PopularFonts = { "Segoe UI", "Arial", "Calibri", "Tahoma", "Verdana", "Trebuchet MS", "Georgia", "Times New Roman", "Consolas", "Courier New" };
		private ComboBox AddColorPicker(Panel panel, string label, string value)
		{
			AddLabel(panel, label);
			var picker = new ComboBox { IsEditable = true, ItemsSource = ColorOptions, Text = value, Margin = new Thickness(0, 0, 0, 10) };
			picker.SelectionChanged += (_, __) => UpdateColorControls();
			picker.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, __) => UpdateColorControls()));
			panel.Children.Add(picker); return picker;
		}
		private static readonly string[] ColorOptions = { "Transparent", "White", "Black", "Red", "Green", "Blue", "Yellow", "Orange", "Purple", "Pink", "Cyan", "Lime", "Gold" };
		private static string ColorValue(ComboBox picker) => string.IsNullOrWhiteSpace(picker.Text) ? "Transparent" : picker.Text;
		private static void SelectColor(ComboBox picker, string value) { picker.SelectedItem = value; picker.Text = value; }
		private void UpdateColorControls()
		{
			if(_damage == null || _highlight == null || _resultColors == null || _resultColorsInColors == null || _last == null || _win == null || _loss == null || _draw == null || _colorWarning == null)
				return;
			var highlightEnabled = _highlight.IsChecked == true;
			var resultColorsEnabled = highlightEnabled && _resultColors.IsChecked == true;
			var outcomeColorsUsed = resultColorsEnabled || _damage.IsChecked == true;
			_resultColors.IsEnabled = highlightEnabled;
			_resultColorsInColors.IsEnabled = highlightEnabled;
			_last.IsEnabled = highlightEnabled && !resultColorsEnabled;
			_win.IsEnabled = outcomeColorsUsed;
			_loss.IsEnabled = outcomeColorsUsed;
			_draw.IsEnabled = outcomeColorsUsed;
			_colorWarning.Visibility = outcomeColorsUsed && HasColorConflict() ? Visibility.Visible : Visibility.Collapsed;
		}

		private void SyncResultColorControls(CheckBox source)
		{
			var value = source.IsChecked == true;
			if(_resultColors != null && !ReferenceEquals(source, _resultColors))
				_resultColors.IsChecked = value;
			if(_resultColorsInColors != null && !ReferenceEquals(source, _resultColorsInColors))
				_resultColorsInColors.IsChecked = value;
			UpdateColorControls();
		}

		private bool HasColorConflict()
		{
			var normal = ParsedColor(_normal.Text);
			var outcomes = new[] { ParsedColor(_win.Text), ParsedColor(_loss.Text), ParsedColor(_draw.Text) };
			return outcomes.Any(color => color == normal) || outcomes.Distinct().Count() < outcomes.Length;
		}

		private static string ParsedColor(string value)
		{
			try { return ((Color)ColorConverter.ConvertFromString(value)).ToString(); }
			catch { return value.Trim().ToUpperInvariant(); }
		}
		private static Slider AddSlider(Panel panel, string label, double min, double max, double tick, double value, string suffix = "")
		{
			var line = new DockPanel { Margin = new Thickness(0, 0, 0, 3), LastChildFill = true };
			var current = new TextBlock { Text = FormatSliderValue(value, suffix), Opacity = .7, HorizontalAlignment = HorizontalAlignment.Right };
			DockPanel.SetDock(current, Dock.Right); line.Children.Add(current);
			line.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold }); panel.Children.Add(line);
			var slider = new Slider { Minimum = min, Maximum = max, TickFrequency = tick, IsSnapToTickEnabled = true, Value = Math.Max(min, Math.Min(max, value)), Margin = new Thickness(0, 0, 0, 10) };
			slider.ValueChanged += (_, __) => current.Text = FormatSliderValue(slider.Value, suffix); panel.Children.Add(slider); return slider;
		}
		private static string FormatSliderValue(double value, string suffix) => value.ToString("0.##", CultureInfo.InvariantCulture) + suffix;
		private static OpponentMemorySettings Clone(OpponentMemorySettings source) { var clone = new OpponentMemorySettings(); clone.CopyFrom(source); return clone; }
	}
}
