using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Localization;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Web.GitHub;
using ArchiSteamFarm.Web.GitHub.Data;
using JetBrains.Annotations;

namespace BirthdayPlugin {
#pragma warning disable CA1863 // Use 'CompositeFormat'
#pragma warning disable CA1812 // ASF uses this class during runtime
	[UsedImplicitly]
	internal sealed class BirthdayPlugin : IBotModules, IDisposable, IGitHubPluginUpdates {
		internal sealed class Birthday(DateTimeOffset date, string? name = null) {
			public DateTimeOffset Date = date;
			public string? Name = name;
		}

		private static readonly ConcurrentDictionary<Bot, Birthday> BotsDB = new();

		private static Timer? BirthdayTimer;

		public string Name => nameof(BirthdayPlugin);
		public Version Version => typeof(BirthdayPlugin).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));

		public string RepositoryName => "CatPoweredPlugins/BirthdayPlugin";

		public async Task<Uri?> GetTargetReleaseURL(Version asfVersion, string asfVariant, bool asfUpdate, bool stable, bool forced) {
			ArgumentNullException.ThrowIfNull(asfVersion);
			ArgumentException.ThrowIfNullOrEmpty(asfVariant);

			if (string.IsNullOrEmpty(RepositoryName)) {

				ASF.ArchiLogger.LogGenericError(string.Format(CultureInfo.CurrentCulture, Strings.WarningFailedWithError, nameof(RepositoryName)));

				return null;
			}

			ReleaseResponse? releaseResponse = await GitHubService.GetLatestRelease(RepositoryName, stable).ConfigureAwait(false);

			if (releaseResponse == null) {
				return null;
			}

			Version newVersion = new(releaseResponse.Tag);

			if (!(Version.Major == newVersion.Major && Version.Minor == newVersion.Minor && Version.Build == newVersion.Build) && !(asfUpdate || forced)) {
				ASF.ArchiLogger.LogGenericInfo(string.Format(CultureInfo.CurrentCulture, "New {0} plugin version {1} is only compatible with latest ASF version", Name, newVersion));
				return null;
			}


			if (Version >= newVersion & !forced) {
				ASF.ArchiLogger.LogGenericInfo(string.Format(CultureInfo.CurrentCulture, Strings.PluginUpdateNotFound, Name, Version, newVersion));

				return null;
			}

			if (releaseResponse.Assets.Count == 0) {
				ASF.ArchiLogger.LogGenericWarning(string.Format(CultureInfo.CurrentCulture, Strings.PluginUpdateNoAssetFound, Name, Version, newVersion));

				return null;
			}

			ReleaseAsset? asset = await ((IGitHubPluginUpdates) this).GetTargetReleaseAsset(asfVersion, asfVariant, newVersion, releaseResponse.Assets).ConfigureAwait(false);

			if ((asset == null) || !releaseResponse.Assets.Contains(asset)) {
				ASF.ArchiLogger.LogGenericWarning(string.Format(CultureInfo.CurrentCulture, Strings.PluginUpdateNoAssetFound, Name, Version, newVersion));

				return null;
			}

			ASF.ArchiLogger.LogGenericInfo(string.Format(CultureInfo.CurrentCulture, Strings.PluginUpdateFound, Name, Version, newVersion));

			return asset.DownloadURL;
		}


		internal static readonly string[] ISO8601format = ["yyyy-MM-dd'T'HH:mm:ss.FFFK"];

		private static void CalculateNextEvent() {
			DateTime? nextTime = null;


			foreach (KeyValuePair<Bot, Birthday> record in BotsDB) {

				DateTime curBotTime = new(DateTime.Now.Year, DateTime.Now.Month, record.Value.Date.Day, record.Value.Date.Hour, record.Value.Date.Minute, record.Value.Date.Second);
				if (curBotTime < DateTime.Now) {
					curBotTime = curBotTime.AddMonths(1);
				}

				if (nextTime == null || nextTime > curBotTime) {
					nextTime = curBotTime;
				}
			}
			if (nextTime != null) {
				_ = BirthdayTimer!.Change(nextTime.Value - DateTime.Now, nextTime.Value - DateTime.Now);
			}

		}
		public Task OnBotInitModules(Bot bot, IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
			if (additionalConfigProperties == null) {
				return Task.CompletedTask;
			}

			foreach (KeyValuePair<string, JsonElement> configProperty in additionalConfigProperties) {
				switch (configProperty.Key.ToUpperInvariant()) {
					case "BIRTHDAY" when configProperty.Value.ValueKind == JsonValueKind.String: {

						if (DateTimeOffset.TryParseExact(configProperty.Value.ToString(),
													 ISO8601format,
													 CultureInfo.InvariantCulture,
													 DateTimeStyles.None,
													 out DateTimeOffset birthday)) {
							_ = BotsDB.TryAdd(bot, new Birthday(birthday));
						}
						break;
					}

					case "BIRTHDAYNAME" when configProperty.Value.ValueKind == JsonValueKind.String: {
						if (configProperty.Value.ToString() != null) {
							if (BotsDB.TryGetValue(bot, out Birthday? birthday)) {
								Birthday newBirthday = new(birthday.Date, configProperty.Value.ToString());
								_ = BotsDB.TryUpdate(bot, newBirthday, birthday);
							}
						}
						break;
					}

					default:
						break;
				}
			}
			if (BotsDB.IsEmpty) {
				return Task.CompletedTask;
			}

			CalculateNextEvent();

			return Task.CompletedTask;

		}
		private static async Task Congratulations() {
			foreach (KeyValuePair<Bot, Birthday> record in BotsDB) {
				if (record.Key.IsConnectedAndLoggedOn) { //Yes Abry, I did it this time
					if (record.Value.Date.Day == DateTime.Now.Day && record.Value.Date.Month == DateTime.Now.Month && record.Value.Date.Hour == DateTime.Now.Hour && record.Value.Date.Minute == DateTime.Now.Minute) {
						string greetings = "Happy Birthday!";
						if (record.Value.Name != null) {
							greetings = $"Happy Birthday, dear {record.Value.Name}!";
						}
						if (!await record.Key.SendMessage(record.Key.Actions.GetFirstSteamMasterID(), greetings).ConfigureAwait(false)) {
							record.Key.ArchiLogger.LogGenericError("Failed to wish happy birthday to master, bottu sad(");
						}
					}
				} else {
					record.Key.ArchiLogger.LogGenericError("I'm offline, so I can't wish happy birthday to master, bottu sad(");
				}
			}

			CalculateNextEvent();
		}
		public Task OnLoaded() {
			ASF.ArchiLogger.LogGenericInfo($"{Name} by Rudokhvist, powered by ginger cats");
			BirthdayTimer = new(async e => await Congratulations().ConfigureAwait(false));
			return Task.CompletedTask;
		}

		public void Dispose() => BirthdayTimer?.Dispose();
	}
}
#pragma warning restore CA1812 // ASF uses this class during runtime
#pragma warning restore CA1863 // Use 'CompositeFormat'
