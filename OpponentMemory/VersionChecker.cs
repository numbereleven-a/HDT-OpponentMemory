using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpponentMemory
{
	public sealed class VersionCheckResult
	{
		public VersionCheckResult(string repository, string latestVersion, bool updateAvailable)
		{
			Repository = repository;
			LatestVersion = latestVersion;
			UpdateAvailable = updateAvailable;
		}

		public string Repository { get; }
		public string LatestVersion { get; }
		public bool UpdateAvailable { get; }
	}

	public static class VersionChecker
	{
		public const string DefaultRepository = "numbereleven-a/HDT-OpponentMemory";
		public const string RepositoryEnvironmentVariable = "HDT_OPPONENT_MEMORY_UPDATE_REPOSITORY";
		public const string TokenEnvironmentVariable = "HDT_OPPONENT_MEMORY_UPDATE_TOKEN";

		private static readonly Regex RepositoryPattern = new Regex(
			@"^[A-Za-z0-9](?:[A-Za-z0-9_.-]{0,99})/[A-Za-z0-9](?:[A-Za-z0-9_.-]{0,99})$",
			RegexOptions.CultureInvariant);
		private static readonly Regex VersionPattern = new Regex(
			@"^[vV]?(?<core>[0-9]+(?:\.[0-9]+){1,3})(?:-(?<suffix>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$",
			RegexOptions.CultureInvariant);
		private static readonly HttpClient HttpClient = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(15)
		};

		public static async Task<VersionCheckResult> CheckLatestAsync(
			string repository,
			Version installedVersion,
			string? token,
			CancellationToken cancellationToken)
		{
			if(installedVersion == null)
				throw new ArgumentNullException(nameof(installedVersion));
			repository = ValidateRepository(repository);
			var requestUri = "https://api.github.com/repos/" + repository + "/releases/latest";
			using(var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
			{
				request.Headers.TryAddWithoutValidation("User-Agent", "HDT-OpponentMemory");
				request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
				request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
				var trimmedToken = token?.Trim();
				if(!string.IsNullOrWhiteSpace(trimmedToken))
					request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + trimmedToken);
				using(var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
				{
					response.EnsureSuccessStatusCode();
					using(var stream = await response.Content.ReadAsStreamAsync())
					{
						var serializer = new DataContractJsonSerializer(typeof(LatestReleaseResponse));
						var release = serializer.ReadObject(stream) as LatestReleaseResponse;
						var tagName = release?.TagName;
						if(string.IsNullOrWhiteSpace(tagName))
							throw new FormatException("The release response does not contain a version tag.");
						var latest = Parse(tagName!);
						var installed = ParsedVersion.FromInstalledVersion(installedVersion);
						return new VersionCheckResult(repository, latest.DisplayValue, latest.CompareTo(installed) > 0);
					}
				}
			}
		}

		public static string ValidateRepository(string repository)
		{
			var value = repository?.Trim() ?? string.Empty;
			if(!RepositoryPattern.IsMatch(value) || value.Contains(".."))
				throw new ArgumentException("Invalid repository name.", nameof(repository));
			return value;
		}

		public static int CompareTagToInstalledVersion(string tag, Version installedVersion)
		{
			if(installedVersion == null)
				throw new ArgumentNullException(nameof(installedVersion));
			return Parse(tag).CompareTo(ParsedVersion.FromInstalledVersion(installedVersion));
		}

		public static int CompareTags(string left, string right) => Parse(left).CompareTo(Parse(right));

		public static string NormalizeTag(string tag) => Parse(tag).DisplayValue;

		private static ParsedVersion Parse(string tag)
		{
			var value = tag?.Trim() ?? string.Empty;
			var match = VersionPattern.Match(value);
			if(!match.Success)
				throw new FormatException("Invalid release version tag.");
			var components = match.Groups["core"].Value.Split('.').Select(ParseComponent).ToArray();
			var normalizedComponents = new int[4];
			Array.Copy(components, normalizedComponents, components.Length);
			var suffix = match.Groups["suffix"].Success ? match.Groups["suffix"].Value.Split('.') : Array.Empty<string>();
			var displayValue = match.Groups["core"].Value + (suffix.Length > 0 ? "-" + string.Join(".", suffix) : string.Empty);
			return new ParsedVersion(normalizedComponents, suffix, displayValue);
		}

		private static int ParseComponent(string value)
		{
			if(!int.TryParse(value, out var component) || component < 0)
				throw new FormatException("Invalid release version component.");
			return component;
		}

		[DataContract]
		private sealed class LatestReleaseResponse
		{
			[DataMember(Name = "tag_name")]
			public string? TagName { get; set; }
		}

		private sealed class ParsedVersion : IComparable<ParsedVersion>
		{
			private readonly int[] _components;
			private readonly string[] _suffix;

			internal ParsedVersion(int[] components, string[] suffix, string displayValue)
			{
				_components = components;
				_suffix = suffix;
				DisplayValue = displayValue;
			}

			internal string DisplayValue { get; }

			internal static ParsedVersion FromInstalledVersion(Version version)
			{
				var components = new[]
				{
					version.Major,
					version.Minor,
					version.Build < 0 ? 0 : version.Build,
					version.Revision < 0 ? 0 : version.Revision
				};
				return new ParsedVersion(components, Array.Empty<string>(), version.ToString());
			}

			public int CompareTo(ParsedVersion? other)
			{
				if(other == null)
					return 1;
				for(var index = 0; index < _components.Length; index++)
				{
					var comparison = _components[index].CompareTo(other._components[index]);
					if(comparison != 0)
						return comparison;
				}
				if(_suffix.Length == 0 || other._suffix.Length == 0)
					return _suffix.Length == other._suffix.Length ? 0 : _suffix.Length == 0 ? 1 : -1;
				var commonLength = Math.Min(_suffix.Length, other._suffix.Length);
				for(var index = 0; index < commonLength; index++)
				{
					var comparison = CompareSuffixIdentifier(_suffix[index], other._suffix[index]);
					if(comparison != 0)
						return comparison;
				}
				return _suffix.Length.CompareTo(other._suffix.Length);
			}

			private static int CompareSuffixIdentifier(string left, string right)
			{
				var leftIsNumeric = left.All(char.IsDigit);
				var rightIsNumeric = right.All(char.IsDigit);
				if(leftIsNumeric && rightIsNumeric)
				{
					var normalizedLeft = left.TrimStart('0');
					var normalizedRight = right.TrimStart('0');
					if(normalizedLeft.Length == 0)
						normalizedLeft = "0";
					if(normalizedRight.Length == 0)
						normalizedRight = "0";
					var lengthComparison = normalizedLeft.Length.CompareTo(normalizedRight.Length);
					return lengthComparison != 0 ? lengthComparison : string.Compare(normalizedLeft, normalizedRight, StringComparison.Ordinal);
				}
				if(leftIsNumeric != rightIsNumeric)
					return leftIsNumeric ? -1 : 1;
				return string.Compare(left, right, StringComparison.Ordinal);
			}
		}
	}
}
