using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using CitizenFX.Core;

using Newtonsoft.Json;

using vMenuShared;

using static CitizenFX.Core.Native.API;
using static vMenuServer.DebugLog;

namespace vMenuServer
{
    public class BanManager : BaseScript
    {
        private const string BAN_KVP_PREFIX = "vmenu_ban_";
        /// <summary>
        /// Struct used to store bans.
        /// </summary>
        public class BanRecord
        {
            public string playerName;
            public List<string> identifiers;
            public DateTime bannedUntil;
            public string banReason;
            public string bannedBy;
            public Guid uuid;

            public BanRecord(string playerName, List<string> identifiers, DateTime bannedUntil, string banReason, string bannedBy, Guid uuid)
            {
                this.playerName = playerName;
                this.identifiers = identifiers;
                this.bannedUntil = bannedUntil;
                this.banReason = banReason;
                var uuidSuffix = $"\nYour ban id: {uuid}";
                if (!this.banReason.Contains(uuidSuffix) && uuid != Guid.Empty)
                {
                    this.banReason += uuidSuffix;
                }
                this.bannedBy = bannedBy;
                this.uuid = uuid;
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public BanManager()
        {
            // These are reached from server/integrations.lua, which relays the client's request together with
            // the real network source. They are exports rather than event handlers on purpose: on this runtime
            // every C# event handler also accepts events sent by clients, so an event taking the acting player's
            // id as a plain argument would let any client ban or unban while posing as an admin. Clients cannot
            // call exports. Arguments come in untyped from Lua, hence the conversions.
            Exports.Add("TempBanPlayer", new Action<object, object, object, object>((playerId, targetPlayer, banDurationHours, banReason) =>
                BanPlayer(ToInt(playerId), ToInt(targetPlayer), ToDouble(banDurationHours), banReason as string, permanent: false)));
            Exports.Add("PermBanPlayer", new Action<object, object, object>((playerId, targetPlayer, banReason) =>
                BanPlayer(ToInt(playerId), ToInt(targetPlayer), 0, banReason as string, permanent: true)));
            Exports.Add("CheckBan", new Func<object, string>((playerId) => CheckForBans(ToInt(playerId))));
            Exports.Add("UnbanPlayer", new Action<object, object>((playerId, uuid) => RemoveBanRecord(ToInt(playerId), uuid as string)));
            Exports.Add("SendBanList", new Action<object>((playerId) => SendBanList(ToInt(playerId))));
        }

        private static int ToInt(object value)
        {
            try
            {
                return value == null ? 0 : Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private static double ToDouble(object value)
        {
            try
            {
                return value == null ? double.NaN : Convert.ToDouble(value);
            }
            catch
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Caps a client-supplied reason and strips line breaks so it can't forge extra lines in the ban log.
        /// </summary>
        internal static string SanitizeReason(string reason, string fallback)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return fallback;
            }
            reason = reason.Replace("\r", " ").Replace("\n", " ").Trim();
            return reason.Length > 256 ? reason.Substring(0, 256) : reason;
        }

        private static bool HasAnyPermission(Player source, params PermissionsManager.Permission[] permissions)
        {
            return permissions.Any(p => PermissionsManager.IsAllowed(p, source))
                || PermissionsManager.IsAllowed(PermissionsManager.Permission.Everything, source);
        }

        /// <summary>
        /// Sends the banlist (as json string) to the client.
        /// </summary>
        /// <param name="source"></param>
        private void SendBanList(int playerId)
        {
            var source = playerId > 0 && DoesPlayerExist(playerId.ToString()) ? Players[playerId] : null;

            // Gate the ban list (contains all identifiers + reasons) behind a permission check.
            if (source == null)
            {
                return;
            }
            if (!HasAnyPermission(source, PermissionsManager.Permission.OPViewBannedPlayers, PermissionsManager.Permission.OPAll))
            {
                BanCheater(source);
                return;
            }

            Log("Updating player with new banlist.\n");
            // IP addresses are only for staff who can actually lift a ban; viewers get everything else.
            var canSeeIps = HasAnyPermission(source, PermissionsManager.Permission.OPUnban, PermissionsManager.Permission.OPAll);
            var bans = canSeeIps ? GetBanList() : GetBanList().Select(b => new BanRecord(
                b.playerName,
                b.identifiers?.Where(id => !id.StartsWith("ip:")).ToList() ?? new List<string>(),
                b.bannedUntil,
                b.banReason,
                b.bannedBy,
                b.uuid)).ToList();
            var data = JsonConvert.SerializeObject(bans).ToString();

            source?.TriggerEvent("vMenu:SetBanList", data);
        }

        private static List<BanRecord> cachedBansList = new();
        private static bool bansHaveChanged = true;

        /// <summary>
        /// Gets the cached ban list or refreshes the bans list from the kvp storage when there has been a change.
        /// </summary>
        /// <returns></returns>
        public static List<BanRecord> GetBanList()
        {
            if (bansHaveChanged)
            {
                bansHaveChanged = false;
                var handle = StartFindKvp(BAN_KVP_PREFIX);
                var kvpIds = new List<string>();
                while (true)
                {
                    var id = FindKvp(handle);
                    if (string.IsNullOrEmpty(id))
                    {
                        break;
                    }

                    kvpIds.Add(id);
                }
                EndFindKvp(handle);

                var banRecords = new List<BanRecord>();

                foreach (var kvpId in kvpIds)
                {
                    try
                    {
                        banRecords.Add(JsonConvert.DeserializeObject<BanRecord>(GetResourceKvpString(kvpId)));
                    }
                    catch (Exception ex)
                    {
                        // A single corrupt ban KVP must not break ban checking for connecting players.
                        Log($"Failed to deserialize ban record '{kvpId}', skipping it. Error: {ex.Message}", LogLevel.error);
                        continue;
                    }
                }
                cachedBansList = banRecords;
                return banRecords;
            }
            else
            {
                return cachedBansList;
            }
        }

        /// <summary>
        /// Checks if the player is banned and if so how long the ban will remain active. 
        /// If the ban expired in the past, then the ban will be removed and the player will be allowed to join again.
        /// If the ban is not expired yet, then the player will be kicked with a message  displaying how long their ban will remain active.
        /// </summary>
        /// <param name="playerId">The (temporary) server id of the connecting player.</param>
        /// <returns>The rejection message if the player is banned, otherwise an empty string.</returns>
        private string CheckForBans(int playerId)
        {
            Player player = null;
            
            if (playerId > 0)
            {
                try
                {
                    player = Players[playerId];
                }
                catch
                {
                    // Player might not be fully connected yet
                }
            }
            
            // Take care of expired bans.
            var oldBans = GetBanList().Where(banRecord =>
            {
                return banRecord.bannedUntil.Subtract(DateTime.Now).TotalSeconds <= 0;
            }).ToList();

            oldBans.ForEach(br =>
            {
                RemoveBan(br);
            });

            // Now look for active bans.
            var records = GetBanList().Where((banRecord) =>
            {
                return banRecord.bannedUntil.Subtract(DateTime.Now).TotalSeconds > 0;
            }).ToList();

            // Find any bans with matching player identifiers.
            var record = records.Find(br =>
            {
                if (player == null) return false;
                return br.identifiers.Any(identifier =>
                {
                    return player.Identifiers.Contains(identifier);
                });
            });

            // If no record is found, stop.
            if (record == null)
            {
                return string.Empty;
            }

            // Build ban message
            string banMessage;
            if (record.bannedUntil.Year >= 3000)
            {
                banMessage = $"You have been permanently banned from this server. Banned by: {record.bannedBy}. Ban reason: {record.banReason}. Additional information: {vMenuShared.ConfigManager.GetSettingsString(vMenuShared.ConfigManager.Setting.vmenu_default_ban_message_information)}.";
            }
            else
            {
                banMessage = $"You are banned from this server. Ban time remaining: {GetRemainingTimeMessage(record.bannedUntil.Subtract(DateTime.Now))}. Additional information: {vMenuShared.ConfigManager.GetSettingsString(vMenuShared.ConfigManager.Setting.vmenu_default_ban_message_information)}.";
            }

            return banMessage;
        }

        /// <summary>
        /// Bans the specified player, either permanently or for the specified amount of hours.
        /// </summary>
        /// <param name="playerId">The ID of the player who triggered the event.</param>
        /// <param name="targetPlayer">Player who needs to be banned.</param>
        /// <param name="banDurationHours">Ban duration in hours. Ignored for permanent bans.</param>
        /// <param name="banReason">Reason for the ban.</param>
        /// <param name="permanent">Whether this is a permanent ban.</param>
        private void BanPlayer(int playerId, int targetPlayer, double banDurationHours, string banReason, bool permanent)
        {
            var source = playerId > 0 && DoesPlayerExist(playerId.ToString()) ? Players[playerId] : null;

            if (source == null)
            {
                Log("BanPlayer: null source, aborting.", LogLevel.error);
                return;
            }

            var requiredPermission = permanent ? PermissionsManager.Permission.OPPermBan : PermissionsManager.Permission.OPTempBan;
            if (HasAnyPermission(source, requiredPermission, PermissionsManager.Permission.OPAll))
            {
                // A temporary ban needs a real duration; anything else would silently become permanent.
                if (!permanent && (double.IsNaN(banDurationHours) || double.IsInfinity(banDurationHours) || banDurationHours <= 0))
                {
                    source.TriggerEvent("vMenu:Notify", "~r~Invalid ban duration, nobody was banned.");
                    return;
                }

                Log("Source player is allowed to ban others.", LogLevel.info);
                var target = targetPlayer > 0 && DoesPlayerExist(targetPlayer.ToString()) ? Players[targetPlayer] : null;
                if (target != null)
                {
                    Log("Target player is not null so moving on.", LogLevel.info);
                    if (!IsPlayerAceAllowed(target.Handle, "vMenu.DontBanMe"))
                    {
                        Log("Target player (Player) does not have the 'dont ban me' permission, so we can continue to ban them.", LogLevel.info);
                        var banduration = permanent ?
                                                /* ban forever */ new DateTime(3000, 1, 1) :
                                                /* ban temporarily */ DateTime.Now.AddHours(Math.Min(banDurationHours, 720.0));

                        var ban = new BanRecord(
                            GetSafePlayerName(target.Name),
                            target.Identifiers.ToList(),
                            banduration,
                            SanitizeReason(banReason, "No reason specified."),
                            GetSafePlayerName(source.Name),
                            Guid.NewGuid()
                        );

                        AddBan(ban);
                        Log("Ban record created.", LogLevel.info);
                        BanLog($"A new ban record has been added. Player: '{ban.playerName}' was banned by " +
                            $"'{ban.bannedBy}' for '{ban.banReason}' until '{ban.bannedUntil}'.");
                        TriggerEvent("vMenu:BanSuccessful", JsonConvert.SerializeObject(ban).ToString());

                        var timeRemaining = GetRemainingTimeMessage(ban.bannedUntil.Subtract(DateTime.Now));
                        target.Drop($"You are banned from this server. Ban time remaining: {timeRemaining}. Banned by: {ban.bannedBy}. Ban reason: {ban.banReason}. Aditional information: {vMenuShared.ConfigManager.GetSettingsString(vMenuShared.ConfigManager.Setting.vmenu_default_ban_message_information)}.");
                        source.TriggerEvent("vMenu:Notify", "~g~Target player successfully banned.");
                    }
                    else
                    {
                        Log("Player could not be banned because he is exempt from being banned.", LogLevel.error);
                        source.TriggerEvent("vMenu:Notify", "~r~Could not ban this player, they are exempt from being banned.");
                    }
                }
                else
                {
                    Log("Player is invalid (no longer online) and therefor the banning has failed.", LogLevel.error);
                    source.TriggerEvent("vMenu:Notify", "Could not ban this player because they already left the server.");
                }
            }
            else
            {
                Log("If enabled, the source player will be banned now because they are cheating!", LogLevel.warning);
                BanCheater(source);
            }
        }

        /// <summary>
        /// Returns a formatted string displaying exactly how many days, hours and/or minutes a ban remains active.
        /// </summary>
        /// <param name="remainingTime"></param>
        /// <returns></returns>
        internal static string GetRemainingTimeMessage(TimeSpan remainingTime)
        {
            var message = "";
            if (remainingTime.Days > 0)
            {
                message += $"{remainingTime.Days} day{(remainingTime.Days > 1 ? "s" : "")} ";
            }
            if (remainingTime.Hours > 0)
            {
                message += $"{remainingTime.Hours} hour{(remainingTime.Hours > 1 ? "s" : "")} ";
            }
            if (remainingTime.Minutes > 0)
            {
                message += $"{remainingTime.Minutes} minute{(remainingTime.Minutes > 1 ? "s" : "")}";
            }
            if (remainingTime.Days < 1 && remainingTime.Hours < 1 && remainingTime.Minutes < 1)
            {
                message = "Less than 1 minute";
            }
            return message;
        }

        /// <summary>
        /// Adds a ban manually.
        /// </summary>
        /// <param name="ban"></param>
        /// <returns></returns>
        internal static void AddBan(BanRecord ban)
        {
            var existingRecord = GetResourceKvpString(BAN_KVP_PREFIX + ban.uuid.ToString());
            if (string.IsNullOrEmpty(existingRecord))
            {
                SetResourceKvp(BAN_KVP_PREFIX + ban.uuid.ToString(), JsonConvert.SerializeObject(ban));
                bansHaveChanged = true;
            }
            else
            {
                Log("Ban record already exists, this is very odd.", LogLevel.error);
            }
        }

        /// <summary>
        /// Removes a ban record from the banned players list.
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public static void RemoveBan(BanRecord record)
        {
            DeleteResourceKvp(BAN_KVP_PREFIX + record.uuid.ToString());
            bansHaveChanged = true;
        }

        /// <summary>
        /// Removes a ban record.
        /// </summary>
        /// <param name="playerId"></param>
        /// <param name="banRecordJsonString"></param>
        private void RemoveBanRecord(int playerId, string uuid)
        {
            var source = playerId > 0 && DoesPlayerExist(playerId.ToString()) ? Players[playerId] : null;

            if (source != null && !string.IsNullOrEmpty(source.Name) && source.Name.ToLower() != "**invalid**" && source.Name.ToLower() != "** invalid **")
            {
                if (HasAnyPermission(source, PermissionsManager.Permission.OPUnban, PermissionsManager.Permission.OPAll))
                {
                    var banRecord = GetBanList().Find((ban) =>
                    {
                        return ban.uuid.ToString() == uuid;
                    });
                    if (banRecord != null)
                    {
                        RemoveBan(banRecord);

                        BanLog($"The following ban record has been removed (player unbanned). " +
                            $"[Player: {banRecord.playerName} was banned by {banRecord.bannedBy} for {banRecord.banReason} until {banRecord.bannedUntil}.]");
                        TriggerEvent("vMenu:UnbanSuccessful", JsonConvert.SerializeObject(banRecord).ToString());
                    }
                }
                else
                {
                    BanCheater(source);
                    Debug.WriteLine($"^3[vMenu] [WARNING] [BAN] ^7Player {JsonConvert.SerializeObject(source)} did not have the required permissions, but somehow triggered the unban event. Missing permissions: vMenu.OnlinePlayers.Unban (is ace allowed: {IsPlayerAceAllowed(source.Handle, "vMenu.OnlinePlayers.Unban")})\n");
                }
            }
            else
            {
                Debug.WriteLine("^3[vMenu] [WARNING] ^7The unban event was triggered, but no valid source was provided. Nobody has been unbanned.");
            }
        }

        /// <summary>
        /// Someone trying to trigger fake server events? Well, goodbye idiots.
        /// </summary>
        /// <param name="source"></param>
        public static void BanCheater(Player source)
        {
            var enabled = vMenuShared.ConfigManager.GetSettingsBool(vMenuShared.ConfigManager.Setting.vmenu_auto_ban_cheaters);
            if (enabled)
            {
                var reason = vMenuShared.ConfigManager.GetSettingsString(vMenuShared.ConfigManager.Setting.vmenu_auto_ban_cheaters_ban_message);

                if (string.IsNullOrEmpty(reason))
                {
                    reason = $"You have been automatically banned. If you believe this was done by error, please contact the server owner for support. Aditional information: {vMenuShared.ConfigManager.GetSettingsString(vMenuShared.ConfigManager.Setting.vmenu_default_ban_message_information)}.";
                }
                var ban = new BanRecord(
                    GetSafePlayerName(source.Name),
                    source.Identifiers.ToList(),
                    new DateTime(3000, 1, 1),
                    reason,
                    "vMenu Auto Ban",
                    Guid.NewGuid()
                );

                AddBan(ban);

                TriggerEvent("vMenu:BanCheaterSuccessful", JsonConvert.SerializeObject(ban).ToString());
                BanLog($"A cheater has been banned. {JsonConvert.SerializeObject(ban)}");

                source.TriggerEvent("vMenu:GoodBye"); // this is much more fun than just kicking them.
                Log("A cheater has been banned because they attempted to trigger a fake event.", LogLevel.warning);
            }
        }

        /// <summary>
        /// Returns the safe playername string to be used in json converter to prevent fuckups when saving the bans file.
        /// </summary>
        /// <param name="playerName"></param>
        /// <returns></returns>
        public static string GetSafePlayerName(string playerName)
        {
            if (!string.IsNullOrEmpty(playerName))
            {
                var safeName = playerName.Replace("^", "").Replace("<", "").Replace(">", "").Replace("~", "");
                safeName = Regex.Replace(safeName, @"[^\u0000-\u007F]+", string.Empty);
                safeName = safeName.Trim(new char[] { '.', ',', ' ', '!', '?' });
                if (string.IsNullOrEmpty(safeName))
                {
                    safeName = "InvalidPlayerName";
                }
                return safeName;
            }
            return "InvalidPlayerName";
        }

        /// <summary>
        /// If enabled using convars, will log all ban actions to the server console as well as an external file.
        /// </summary>
        /// <param name="banActionMessage"></param>
        public static void BanLog(string banActionMessage)
        {
            if (vMenuShared.ConfigManager.GetSettingsBool(vMenuShared.ConfigManager.Setting.vmenu_log_ban_actions))
            {
                var date = DateTime.Now;
                var formattedDate = (date.Day < 10 ? "0" : "") + date.Day + "-" +
                    (date.Month < 10 ? "0" : "") + date.Month + "-" +
                    (date.Year < 10 ? "0" : "") + date.Year + " " +
                    (date.Hour < 10 ? "0" : "") + date.Hour + ":" +
                    (date.Minute < 10 ? "0" : "") + date.Minute + ":" +
                    (date.Second < 10 ? "0" : "") + date.Second;
                AppendToLogFile($"[\t{formattedDate}\t] [BAN ACTION] {banActionMessage}\n");
                Debug.WriteLine("^2[vMenu] [SUCCESS] [BAN]^7 " + banActionMessage);
            }
        }

        /// <summary>
        /// Appends a line to vmenu.log in the resource folder. SaveResourceFile can only replace the
        /// whole file, which meant reading and rewriting the entire (ever growing) log for every action.
        /// </summary>
        /// <param name="line"></param>
        internal static void AppendToLogFile(string line)
        {
            try
            {
                File.AppendAllText(Path.Combine(GetResourcePath(GetCurrentResourceName()), "vmenu.log"), line);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"^1[vMenu] [ERROR]^7 Could not write to vmenu.log: {e.Message}");
            }
        }

        /// <summary>
        /// Gets the formatted date to be converted into a proper datetime type for the SQLite DB.
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static string GetFormattedDate(DateTime date)
        {
            return $"{date.Year}-{(date.Month < 10 ? "0" + date.Month.ToString() : date.Month.ToString())}-{(date.Day < 10 ? "0" + date.Day.ToString() : date.Day.ToString())} {(date.Hour < 10 ? "0" + date.Hour.ToString() : date.Hour.ToString())}:{(date.Minute < 10 ? "0" + date.Minute.ToString() : date.Minute.ToString())}:{(date.Second < 10 ? "0" + date.Second.ToString() : date.Second.ToString())}";
        }
    }
}
