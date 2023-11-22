using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace BirthdayPlugin {
#pragma warning disable CA1812 // ASF uses this class during runtime
	[UsedImplicitly]
	internal sealed class BirthdayPlugin : IBotModules, IDisposable {
		internal class Birthday {
			public DateTime Date;
			public string? Name;
			public Birthday(DateTime date, string? name = null) {
				Date = date;
				Name = name;
			}
		}

		private static readonly ConcurrentDictionary<Bot, Birthday> BotsDB = new();

		private static Timer? BirthdayTimer;

		public string Name => nameof(BirthdayPlugin);
		public Version Version => typeof(BirthdayPlugin).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));


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
		public Task OnBotInitModules(Bot bot, IReadOnlyDictionary<string, JToken>? additionalConfigProperties = null) {
			if (additionalConfigProperties == null) {
				return Task.CompletedTask;
			}

			foreach (KeyValuePair<string, JToken> configProperty in additionalConfigProperties) {
				switch (configProperty.Key.ToUpperInvariant()) {
					case "BIRTHDAY" when configProperty.Value.Type == JTokenType.Date: {
						DateTime? birthday = (DateTime?) configProperty.Value;
						if (birthday != null) {
							_ = BotsDB.TryAdd(bot, new Birthday((DateTime) birthday));
						}
						break;
					}

					case "BIRTHDAYNAME" when configProperty.Value.Type == JTokenType.String: {
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

		public void Dispose() {
			if (BirthdayTimer != null) {
				BirthdayTimer.Dispose();
			}
		}
	}
}
#pragma warning restore CA1812 // ASF uses this class during runtime
