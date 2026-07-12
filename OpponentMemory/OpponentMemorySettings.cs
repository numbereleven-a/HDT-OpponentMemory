using System;
using System.IO;
using System.Xml.Serialization;
using Hearthstone_Deck_Tracker;

namespace OpponentMemory
{
	public enum CounterSide { Left, Right }

	public sealed class OpponentMemorySettings
	{
		private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(OpponentMemorySettings));
		private static readonly object SaveSync = new object();
		public bool Enabled { get; set; } = true;
		public CounterSide CounterSide { get; set; } = CounterSide.Right;
		public double HorizontalOffset { get; set; } = 8;
		public double PerRowHorizontalOffset { get; set; } = -2;
		public double VerticalOffset { get; set; }
		public double NextOpponentExtraOffset { get; set; } = 32;
		public double Scale { get; set; } = 1;
		public bool ShowZeroValues { get; set; } = true;
		public bool ShowEncounterCounts { get; set; } = true;
		public bool HighlightLastOpponent { get; set; } = true;
		public bool CountGhostEncounters { get; set; } = true;
		public string FontFamily { get; set; } = "Segoe UI";
		public double FontSize { get; set; } = 22;
		public bool BoldText { get; set; } = true;
		public string NormalTextColor { get; set; } = "Green";
		public string LastOpponentTextColor { get; set; } = "Red";
		public string BackgroundColor { get; set; } = "Transparent";
		public double TextOpacity { get; set; } = 100;
		public double BackgroundOpacity { get; set; } = 0;

		public void Normalize()
		{
			HorizontalOffset = Clamp(HorizontalOffset, -300, 300, 8);
			PerRowHorizontalOffset = Clamp(PerRowHorizontalOffset, -20, 20, -2);
			VerticalOffset = Clamp(VerticalOffset, -200, 200, 0);
			NextOpponentExtraOffset = Clamp(NextOpponentExtraOffset, -200, 300, 32);
			Scale = Clamp(Scale, .5, 2, 1);
			FontSize = Clamp(FontSize, 8, 72, 22);
			TextOpacity = Clamp(TextOpacity, 0, 100, 100);
			BackgroundOpacity = Clamp(BackgroundOpacity, 0, 100, 0);
			if(string.IsNullOrWhiteSpace(FontFamily)) FontFamily = "Segoe UI";
			if(string.IsNullOrWhiteSpace(NormalTextColor)) NormalTextColor = "Green";
			if(string.IsNullOrWhiteSpace(LastOpponentTextColor)) LastOpponentTextColor = "Red";
			if(string.IsNullOrWhiteSpace(BackgroundColor)) BackgroundColor = "Transparent";
		}

		public void CopyFrom(OpponentMemorySettings source)
		{
			Enabled = source.Enabled;
			CounterSide = source.CounterSide;
			HorizontalOffset = source.HorizontalOffset;
			PerRowHorizontalOffset = source.PerRowHorizontalOffset;
			VerticalOffset = source.VerticalOffset;
			NextOpponentExtraOffset = source.NextOpponentExtraOffset;
			Scale = source.Scale;
			ShowZeroValues = source.ShowZeroValues;
			ShowEncounterCounts = source.ShowEncounterCounts;
			HighlightLastOpponent = source.HighlightLastOpponent;
			CountGhostEncounters = source.CountGhostEncounters;
			FontFamily = source.FontFamily;
			FontSize = source.FontSize;
			BoldText = source.BoldText;
			NormalTextColor = source.NormalTextColor;
			LastOpponentTextColor = source.LastOpponentTextColor;
			BackgroundColor = source.BackgroundColor;
			TextOpacity = source.TextOpacity;
			BackgroundOpacity = source.BackgroundOpacity;
		}

		public static OpponentMemorySettings Load()
		{
			var path = GetPath();
			if(TryLoad(path, out var settings) || TryLoad(path + ".bak", out settings))
				return settings;
			return new OpponentMemorySettings();
		}

		private static bool TryLoad(string path, out OpponentMemorySettings settings)
		{
			settings = new OpponentMemorySettings();
			if(!File.Exists(path))
				return false;
			try
			{
				using(var stream = File.OpenRead(path))
					settings = (OpponentMemorySettings?)Serializer.Deserialize(stream) ?? new OpponentMemorySettings();
				settings.Normalize();
				return true;
			}
			catch(Exception ex)
			{
				PluginLogger.Warn("Could not load " + Path.GetFileName(path) + ": " + ex);
				return false;
			}
		}

		public void Save()
		{
			var path = GetPath();
			string? tempPath = null;
			try
			{
				Normalize();
				var directory = Path.GetDirectoryName(path);
				if(!string.IsNullOrEmpty(directory))
					Directory.CreateDirectory(directory);
				lock(SaveSync)
				{
					tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
					using(var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
					{
						Serializer.Serialize(stream, this);
						stream.Flush(true);
					}
					if(File.Exists(path))
					{
						var backupPath = path + ".bak";
						if(File.Exists(backupPath))
							File.Delete(backupPath);
						File.Replace(tempPath, path, backupPath, true);
					}
					else
						File.Move(tempPath, path);
					tempPath = null;
				}
			}
			catch(Exception ex) { PluginLogger.Warn("Could not save settings: " + ex); }
			finally
			{
				if(tempPath != null)
				{
					try { if(File.Exists(tempPath)) File.Delete(tempPath); }
					catch { }
				}
			}
		}

		public static OpponentMemorySettings CreateDefaults() => new OpponentMemorySettings();
		private static string GetPath() => Path.Combine(Config.Instance.ConfigDir, "OpponentMemory", "settings.xml");
		private static double Clamp(double value, double min, double max, double fallback) => double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max ? fallback : value;
	}
}
