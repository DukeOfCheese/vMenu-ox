using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CitizenFX.Core;

using Newtonsoft.Json;

using vMenuShared;

using static CitizenFX.Core.Native.API;
using static vMenuServer.DebugLog;
using static vMenuShared.ConfigManager;

namespace vMenuServer
{

    public static class DebugLog
    {
        public enum LogLevel
        {
            error = 1,
            success = 2,
            info = 4,
            warning = 3,
            none = 0
        }

        /// <summary>
        /// Global log data function, only logs when debugging is enabled.
        /// </summary>
        /// <param name="data"></param>
        public static void Log(dynamic data, LogLevel level = LogLevel.none)
        {
            if (MainServer.DebugMode || level == LogLevel.error || level == LogLevel.warning)
            {
                var prefix = "[vMenu] ";
                if (level == LogLevel.error)
                {
                    prefix = "^1[vMenu] [ERROR]^7 ";
                }
                else if (level == LogLevel.info)
                {
                    prefix = "^5[vMenu] [INFO]^7 ";
                }
                else if (level == LogLevel.success)
                {
                    prefix = "^2[vMenu] [SUCCESS]^7 ";
                }
                else if (level == LogLevel.warning)
                {
                    prefix = "^3[vMenu] [WARNING]^7 ";
                }
                Debug.WriteLine($"{prefix}[DEBUG LOG] {data.ToString()}");
            }
        }
    }

    public class MainServer : BaseScript
    {
        #region vars
        // Debug shows more information when doing certain things. Leave it off to improve performance!
        public static bool DebugMode = GetResourceMetadata(GetCurrentResourceName(), "server_debug_mode", 0) == "true";

        public static string Version { get { return GetResourceMetadata(GetCurrentResourceName(), "version", 0); } }

        // Time
        private int CurrentHours
        {
            get { return MathUtil.Clamp(GetSettingsInt(Setting.vmenu_current_hour), 0, 23); }
            set { SetConvarReplicated(Setting.vmenu_current_hour.ToString(), MathUtil.Clamp(value, 0, 23).ToString()); }
        }
        private int CurrentMinutes
        {
            get { return MathUtil.Clamp(GetSettingsInt(Setting.vmenu_current_minute), 0, 59); }
            set { SetConvarReplicated(Setting.vmenu_current_minute.ToString(), MathUtil.Clamp(value, 0, 59).ToString()); }
        }
        private int MinuteClockSpeed
        {
            get
            {
                var value = GetSettingsInt(Setting.vmenu_ingame_minute_duration);
                if (value < 100)
                {
                    value = 2000;
                }

                return value;
            }
        }
        private bool FreezeTime
        {
            get { return GetSettingsBool(Setting.vmenu_freeze_time); }
            set { SetConvarReplicated(Setting.vmenu_freeze_time.ToString(), value.ToString().ToLower()); }
        }
        private bool IsServerTimeSynced { get { return GetSettingsBool(Setting.vmenu_sync_to_machine_time); } }


        // Weather
        private string CurrentWeather
        {
            get
            {
                var value = GetSettingsString(Setting.vmenu_current_weather, "CLEAR")?.ToUpper();
                if (value == null || !WeatherTypes.Contains(value))
                {
                    return "CLEAR";
                }
                return value;
            }
            set
            {
                // Replicated to every client, which passes it straight to the weather natives, so only known types get through.
                var weather = value?.ToUpper();
                SetConvarReplicated(Setting.vmenu_current_weather.ToString(), weather != null && WeatherTypes.Contains(weather) ? weather : "CLEAR");
            }
        }
        private bool DynamicWeatherEnabled
        {
            get { return GetSettingsBool(Setting.vmenu_enable_dynamic_weather); }
            set { SetConvarReplicated(Setting.vmenu_enable_dynamic_weather.ToString(), value.ToString().ToLower()); }
        }
        private bool ManualSnowEnabled
        {
            get { return GetSettingsBool(Setting.vmenu_enable_snow); }
            set { SetConvarReplicated(Setting.vmenu_enable_snow.ToString(), value.ToString().ToLower()); }
        }
        private bool BlackoutEnabled
        {
            get { return GetSettingsBool(Setting.vmenu_blackout_enabled); }
            set { SetConvarReplicated(Setting.vmenu_blackout_enabled.ToString(), value.ToString().ToLower()); }
        }
        private bool VehicleBlackoutEnabled
        {
            get { return GetSettingsBool(Setting.vmenu_vehicle_blackout_enabled); }
            set { SetConvarReplicated(Setting.vmenu_vehicle_blackout_enabled.ToString(), value.ToString().ToLower()); }
        }
        private int DynamicWeatherMinutes
        {
            get { return Math.Max(GetSettingsInt(Setting.vmenu_dynamic_weather_timer), 1); }
        }
        private long lastWeatherChange = 0;

        // Single shared RNG. Creating a new Random() per call seeds from the clock and yields
        // identical values when called multiple times within the same tick.
        private static readonly Random _rng = new();

        private readonly List<string> CloudTypes = new()
        {
            "Cloudy 01",
            "RAIN",
            "horizonband1",
            "horizonband2",
            "Puffs",
            "Wispy",
            "Horizon",
            "Stormy 01",
            "Clear 01",
            "Snowy 01",
            "Contrails",
            "altostratus",
            "Nimbus",
            "Cirrus",
            "cirrocumulus",
            "stratoscumulus",
            "horizonband3",
            "Stripey",
            "horsey",
            "shower",
        };
        private readonly List<string> WeatherTypes = new()
        {
            "EXTRASUNNY",
            "CLEAR",
            "NEUTRAL",
            "SMOG",
            "FOGGY",
            "CLOUDS",
            "OVERCAST",
            "CLEARING",
            "RAIN",
            "THUNDER",
            "BLIZZARD",
            "SNOW",
            "SNOWLIGHT",
            "XMAS",
            "HALLOWEEN"
        };
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor.
        /// </summary>
        public MainServer()
        {
            // name check
            if (GetCurrentResourceName() != "vMenu")
            {
                var InvalidNameException = new Exception("\r\n\r\n^1[vMenu] INSTALLATION ERROR!\r\nThe name of the resource is not valid. " +
                    "Please change the folder name from '^3" + GetCurrentResourceName() + "^1' to '^2vMenu^1' (case sensitive) instead!\r\n\r\n\r\n^7");
                try
                {
                    throw InvalidNameException;
                }
                catch (Exception e)
                {
                    Debug.Write(e.Message);
                }
            }
            else
            {
                // check if permissions are setup (correctly)
                if (!GetSettingsBool(Setting.vmenu_use_permissions))
                {
                    Debug.WriteLine("^3[vMenu] [WARNING] vMenu is set up to ignore permissions!\nIf you did this on purpose then you can ignore this warning.\nIf you did not set this on purpose, then you must have made a mistake while setting up vMenu.\nPlease read the vMenu documentation (^5https://docs.vespura.com/vmenu^3).\nMost likely you are not executing the permissions.cfg (correctly).^7");
                }

                Tick += PlayersFirstTick;

                // Start the loops
                if (GetSettingsBool(Setting.vmenu_enable_weather_sync))
                {
                    Tick += WeatherLoop;
                }

                if (GetSettingsBool(Setting.vmenu_enable_time_sync))
                {
                    Tick += TimeLoop;
                }
            }
        }
        #endregion

        #region command handler
        [Command("vmenuserver", Restricted = true)]
        internal void ServerCommandHandler(int source, List<object> args, string _)
        {
            if (args != null)
            {
                if (args.Count > 0)
                {
                    if (args[0].ToString().ToLower() == "debug")
                    {
                        DebugMode = !DebugMode;
                        if (source < 1)
                        {
                            Debug.WriteLine($"Debug mode is now set to: {DebugMode}.");
                        }
                        else
                        {
                            Players[source].TriggerEvent("chatMessage", $"vMenu Debug mode is now set to: {DebugMode}.");
                        }
                        return;
                    }
                    else if (args[0].ToString().ToLower() == "unban" && (source < 1))
                    {
                        if (args.Count() > 1 && !string.IsNullOrEmpty(args[1].ToString()))
                        {
                            var uuid = args[1].ToString().Trim();
                            var bans = BanManager.GetBanList();
                            var banRecord = bans.Find(b => { return b.uuid.ToString() == uuid; });
                            if (banRecord != null)
                            {
                                BanManager.RemoveBan(banRecord);
                                Debug.WriteLine("Player has been successfully unbanned.");
                            }
                            else
                            {
                                Debug.WriteLine($"Could not find a banned player with the provided uuid '{uuid}'.");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("You did not specify a player to unban, you must enter the FULL playername. Usage: vmenuserver unban \"playername\"");
                        }
                        return;
                    }
                    else if (args[0].ToString().ToLower() == "weather")
                    {
                        if (args.Count < 2 || string.IsNullOrEmpty(args[1].ToString()))
                        {
                            Debug.WriteLine("[vMenu] Invalid command syntax. Use 'vmenuserver weather <weatherType>' instead.");
                        }
                        else
                        {
                            var wtype = args[1].ToString().ToUpper();
                            if (WeatherTypes.Contains(wtype))
                            {
                                ApplyWeather(wtype, BlackoutEnabled, DynamicWeatherEnabled, ManualSnowEnabled);
                                Debug.WriteLine($"[vMenu] Weather is now set to: {wtype}");
                            }
                            else if (wtype.ToLower() == "dynamic")
                            {
                                if (args.Count == 3 && !string.IsNullOrEmpty(args[2].ToString()))
                                {
                                    if ((args[2].ToString().ToLower() ?? $"{DynamicWeatherEnabled}") == "true")
                                    {
                                        ApplyWeather(CurrentWeather, BlackoutEnabled, true, ManualSnowEnabled);
                                        Debug.WriteLine("[vMenu] Dynamic weather is now turned on.");
                                    }
                                    else if ((args[2].ToString().ToLower() ?? $"{DynamicWeatherEnabled}") == "false")
                                    {
                                        ApplyWeather(CurrentWeather, BlackoutEnabled, false, ManualSnowEnabled);
                                        Debug.WriteLine("[vMenu] Dynamic weather is now turned off.");
                                    }
                                    else
                                    {
                                        Debug.WriteLine("[vMenu] Invalid command usage. Correct syntax: vmenuserver weather dynamic <true|false>");
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine("[vMenu] Invalid command usage. Correct syntax: vmenuserver weather dynamic <true|false>");
                                }

                            }
                            else
                            {
                                Debug.WriteLine("[vMenu] This weather type is not valid!");
                            }
                        }
                    }
                    else if (args[0].ToString().ToLower() == "time")
                    {
                        if (args.Count == 2)
                        {
                            if (args[1].ToString().ToLower() == "freeze")
                            {
                                FreezeTime = !FreezeTime;
                                Debug.WriteLine($"Time is now {(FreezeTime ? "frozen" : "not frozen")}.");
                            }
                            else
                            {
                                Debug.WriteLine("Invalid syntax. Use: ^5vmenuserver time <freeze|<hour> <minute>>^7 instead.");
                            }
                        }
                        else if (args.Count > 2)
                        {
                            if (int.TryParse(args[1].ToString(), out var hour))
                            {
                                if (int.TryParse(args[2].ToString(), out var minute))
                                {
                                    if (hour is >= 0 and < 24)
                                    {
                                        if (minute is >= 0 and < 60)
                                        {
                                            var setTimeTask = SetTimeAsync(hour, minute);
                                            Debug.WriteLine($"Time is now {(hour < 10 ? ("0" + hour.ToString()) : hour.ToString())}:{(minute < 10 ? ("0" + minute.ToString()) : minute.ToString())}.");
                                        }
                                        else
                                        {
                                            Debug.WriteLine("Invalid minute provided. Value must be between 0 and 59.");
                                        }
                                    }
                                    else
                                    {
                                        Debug.WriteLine("Invalid hour provided. Value must be between 0 and 23.");
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine("Invalid syntax. Use: ^5vmenuserver time <freeze|<hour> <minute>>^7 instead.");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("Invalid syntax. Use: ^5vmenuserver time <freeze|<hour> <minute>>^7 instead.");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Invalid syntax. Use: ^5vmenuserver time <freeze|<hour> <minute>>^7 instead.");
                        }
                    }
                    else if (args[0].ToString().ToLower() == "ban" && source < 1)  // only do this via server console (server id < 1)
                    {
                        if (args.Count > 3)
                        {
                            Player p = null;

                            var findByServerId = args[1].ToString().ToLower() == "id";
                            var identifier = args[2].ToString().ToLower();

                            if (findByServerId)
                            {
                                if (Players.Any(player => player.Handle == identifier))
                                {
                                    p = Players.Single(pl => pl.Handle == identifier);
                                }
                                else
                                {
                                    Debug.WriteLine("[vMenu] Could not find this player, make sure they are online.");
                                    return;
                                }
                            }
                            else
                            {
                                if (Players.Any(player => player.Name.ToLower() == identifier.ToLower()))
                                {
                                    p = Players.Single(pl => pl.Name.ToLower() == identifier.ToLower());
                                }
                                else
                                {
                                    Debug.WriteLine("[vMenu] Could not find this player, make sure they are online.");
                                    return;
                                }
                            }

                            var reason = "Banned by staff for:";
                            args.GetRange(3, args.Count - 3).ForEach(arg => reason += " " + arg);

                            if (p != null)
                            {
                                var ban = new BanManager.BanRecord(
                                    BanManager.GetSafePlayerName(p.Name),
                                    p.Identifiers.ToList(),
                                    new DateTime(3000, 1, 1),
                                    reason,
                                    "Server Console",
                                    Guid.NewGuid()
                                );

                                BanManager.AddBan(ban);
                                BanManager.BanLog($"[vMenu] Player {p.Name}^7 has been banned by Server Console for [{reason}].");
                                TriggerEvent("vMenu:BanSuccessful", JsonConvert.SerializeObject(ban).ToString());
                                var timeRemaining = BanManager.GetRemainingTimeMessage(ban.bannedUntil.Subtract(DateTime.Now));
                                p.Drop($"You are banned from this server. Ban time remaining: {timeRemaining}. Banned by: {ban.bannedBy}. Ban reason: {ban.banReason}. Additional information: {vMenuShared.ConfigManager.GetSettingsString(vMenuShared.ConfigManager.Setting.vmenu_default_ban_message_information)}.");
                            }
                            else
                            {
                                Debug.WriteLine("[vMenu] Player not found, could not ban player.");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("[vMenu] Not enough arguments, syntax: ^5vmenuserver ban <id|name> <server id|username> <reason>^7.");
                        }
                    }
                    else if (args[0].ToString().ToLower() == "help")
                    {
                        Debug.WriteLine("Available commands:");
                        Debug.WriteLine("(server console only): vmenuserver ban <id|name> <server id|username> <reason> (player must be online!)");
                        Debug.WriteLine("(server console only): vmenuserver unban <uuid>");
                        Debug.WriteLine("vmenuserver weather <new weather type | dynamic <true | false>>");
                        Debug.WriteLine("vmenuserver time <freeze|<hour> <minute>>");
                        Debug.WriteLine("vmenuserver migrate (This copies all banned players in the bans.json file to the new ban system in vMenu v3.3.0, you only need to do this once)");
                    }
                    else if (args[0].ToString().ToLower() == "migrate" && source < 1)
                    {
                        var file = LoadResourceFile(GetCurrentResourceName(), "bans.json");
                        if (string.IsNullOrEmpty(file) || file == "[]")
                        {
                            Debug.WriteLine("&1[vMenu] [ERROR]^7 No bans.json file found or it's empty.");
                            return;
                        }
                        Debug.WriteLine("^5[vMenu] [INFO]^7 Importing all ban records from the bans.json file into the new storage system. ^3This may take some time...^7");
                        List<BanManager.BanRecord> bans;
                        try
                        {
                            bans = JsonConvert.DeserializeObject<List<BanManager.BanRecord>>(file);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"^1[vMenu] [ERROR]^7 Could not parse bans.json, migration aborted. Error: {ex.Message}");
                            return;
                        }
                        if (bans == null)
                        {
                            Debug.WriteLine("^1[vMenu] [ERROR]^7 bans.json parsed to null, migration aborted.");
                            return;
                        }
                        bans.ForEach((br) =>
                        {
                            var record = new BanManager.BanRecord(br.playerName, br.identifiers, br.bannedUntil, br.banReason, br.bannedBy, Guid.NewGuid());
                            BanManager.AddBan(record);
                        });
                        Debug.WriteLine("^2[vMenu] [SUCCESS]^7 All ban records have been imported. You now no longer need the bans.json file.");
                    }
                    else
                    {
                        Debug.WriteLine($"vMenu is currently running version: {Version}. Try ^5vmenuserver help^7 for info.");
                    }
                }
                else
                {
                    Debug.WriteLine($"vMenu is currently running version: {Version}. Try ^5vmenuserver help^7 for info.");
                }
            }
            else
            {
                Debug.WriteLine($"vMenu is currently running version: {Version}. Try ^5vmenuserver help^7 for info.");
            }
        }
        #endregion

        #region kick players from personal vehicle
        /// <summary>
        /// Makes the player leave the personal vehicle.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="vehicleNetId"></param>
        [EventHandler("vMenu:GetOutOfCar")]
        internal void GetOutOfCar([FromSource] Player source, int vehicleNetId)
        {
            if (!PermissionsManager.IsAllowed(PermissionsManager.Permission.PVKickPassengers, source) && !PermissionsManager.IsAllowed(PermissionsManager.Permission.PVAll, source))
            {
                BanManager.BanCheater(source);
                return;
            }

            Entity vehicle = Entity.FromNetworkId(vehicleNetId);

            if (vehicle is null)
            {
                return;
            }

            int vehicleHandle = vehicle.Handle;

            // Only the player who spawned the vehicle (or is driving it) may empty it.
            if (!IsVehicleOwnedOrDrivenBy(source, vehicleHandle, requireDriver: true))
            {
                return;
            }

            for (int i = -1; i < 15; i++)
            {
                int pedHandle = GetPedInVehicleSeat(vehicleHandle, i);

                if (pedHandle == 0 || !IsPedAPlayer(pedHandle))
                {
                    continue;
                }

                int playerHandle = NetworkGetEntityOwner(pedHandle);
                Player player = GetPlayerFromServerId(playerHandle);

                if (player is null || player == source)
                {
                    continue;
                }

                // The occupant's own client throws them out: it owns its ped, so it can force them out
                // if the leave task alone does not get them out of the seat.
                player.TriggerEvent("vMenu:ForceOutOfVehicle", vehicleNetId);
            }
        }
        #endregion

        #region clear area near pos
        /// <summary>
        /// Clear the area near this point for all players.
        /// </summary>
        [EventHandler("vMenu:ClearArea")]
        internal void ClearAreaNearPos([FromSource] Player source)
        {
            if (!PermissionsManager.IsAllowed(PermissionsManager.Permission.MSClearArea, source) && !PermissionsManager.IsAllowed(PermissionsManager.Permission.MSAll, source))
            {
                BanManager.BanCheater(source);
                return;
            }

            if (source?.Character == null || RateLimiter.IsLimited(source, "ClearArea", 5000))
            {
                return;
            }

            Ped ped = source.Character;
            Vector3 position = ped.Position;

            TriggerClientEvent("vMenu:ClearArea", position);
        }
        #endregion

        #region Manage weather and time changes.
        /// <summary>
        /// Loop used for syncing and keeping track of the time in-game.
        /// </summary>
        /// <returns></returns>
        private async Task TimeLoop()
        {
            if (IsServerTimeSynced)
            {
                var currentTime = DateTime.Now;
                CurrentMinutes = currentTime.Minute;
                CurrentHours = currentTime.Hour;

                // Update this once every 60 seconds.
                await Delay(60000);
            }
            else
            {
                if (!FreezeTime)
                {
                    if ((CurrentMinutes + 1) > 59)
                    {
                        CurrentMinutes = 0;
                        if ((CurrentHours + 1) > 23)
                        {
                            CurrentHours = 0;
                        }
                        else
                        {
                            CurrentHours++;
                        }
                    }
                    else
                    {
                        CurrentMinutes++;
                    }
                }
                await Delay(MinuteClockSpeed);
            }
        }

        /// <summary>
        /// Task used for syncing and changing weather dynamically.
        /// </summary>
        /// <returns></returns>
        private async Task WeatherLoop()
        {
            if (DynamicWeatherEnabled)
            {
                await Delay(DynamicWeatherMinutes * 60000);

                if (GetSettingsBool(Setting.vmenu_enable_weather_sync))
                {
                    // Manage dynamic weather changes.

                    {
                        // Disable dynamic weather because these weather types shouldn't randomly change.
                        if (CurrentWeather is "XMAS" or "HALLOWEEN" or "NEUTRAL")
                        {
                            DynamicWeatherEnabled = false;
                            return;
                        }

                        // Is it time to generate a new weather type?
                        if (GetGameTimer() - lastWeatherChange > (DynamicWeatherMinutes * 60000))
                        {
                            // Choose a new semi-random weather type.
                            RefreshWeather();

                            // Log if debug mode is on how long the change has taken and what the new weather type will be.
                            if (DebugMode)
                            {
                                Log($"Changing weather, new weather: {CurrentWeather}");
                            }
                        }
                    }
                }
            }
            else
            {
                await Delay(5000);
            }
        }

        /// <summary>
        /// Select a new random weather type, based on the current weather and some patterns.
        /// </summary>
        private void RefreshWeather()
        {
            var random = _rng.Next(20);
            if (CurrentWeather is "RAIN" or "THUNDER")
            {
                CurrentWeather = "CLEARING";
            }
            else if (CurrentWeather == "CLEARING")
            {
                CurrentWeather = "CLOUDS";
            }
            else
            {
                CurrentWeather = random switch
                {
                    0 or 1 or 2 or 3 or 4 or 5 => CurrentWeather == "EXTRASUNNY" ? "CLEAR" : "EXTRASUNNY",
                    6 or 7 or 8 => CurrentWeather == "SMOG" ? "FOGGY" : "SMOG",
                    9 or 10 or 11 => CurrentWeather == "CLOUDS" ? "OVERCAST" : "CLOUDS",
                    12 or 13 or 14 => CurrentWeather == "CLOUDS" ? "OVERCAST" : "CLOUDS",
                    15 => CurrentWeather == "OVERCAST" ? "THUNDER" : "OVERCAST",
                    16 => CurrentWeather == "CLOUDS" ? "EXTRASUNNY" : "RAIN",
                    _ => CurrentWeather == "FOGGY" ? "SMOG" : "FOGGY",
                };
            }

        }
        #endregion

        #region Sync weather & time with clients
        /// <summary>
        /// Update the weather for all clients.
        /// </summary>
        /// <param name="newWeather"></param>
        /// <param name="dynamicWeatherNew"></param>
        [EventHandler("vMenu:UpdateServerWeather")]
        internal void UpdateWeather([FromSource] Player source, string newWeather, bool blackoutNew, bool dynamicWeatherNew, bool enableSnow)
        {
            newWeather = newWeather?.ToUpper();
            if (newWeather == null || !WeatherTypes.Contains(newWeather))
            {
                return;
            }

            // The menu sends every weather setting at once, so each one that actually changes needs its own permission.
            if (source != null)
            {
                var allowed = PermissionsManager.IsAllowed(PermissionsManager.Permission.WOMenu, source)
                    && (newWeather == CurrentWeather || PermissionsManager.IsAllowed(PermissionsManager.Permission.WOSetWeather, source))
                    && (enableSnow == ManualSnowEnabled || PermissionsManager.IsAllowed(PermissionsManager.Permission.WOSetWeather, source))
                    && (blackoutNew == BlackoutEnabled || PermissionsManager.IsAllowed(PermissionsManager.Permission.WOBlackout, source))
                    && (dynamicWeatherNew == DynamicWeatherEnabled || PermissionsManager.IsAllowed(PermissionsManager.Permission.WODynamic, source));
                if (!allowed)
                {
                    BanManager.BanCheater(source);
                    return;
                }
            }

            ApplyWeather(newWeather, blackoutNew, dynamicWeatherNew, enableSnow);
        }

        private void ApplyWeather(string newWeather, bool blackoutNew, bool dynamicWeatherNew, bool enableSnow)
        {
            // Automatically enable snow effects whenever one of the snow weather types is selected.
            if (newWeather is "XMAS" or "SNOWLIGHT" or "SNOW" or "BLIZZARD")
            {
                enableSnow = true;
            }

            // Update the new weather related variables.
            CurrentWeather = newWeather;
            BlackoutEnabled = blackoutNew;
            DynamicWeatherEnabled = dynamicWeatherNew;
            ManualSnowEnabled = enableSnow;

            // Reset the dynamic weather loop timer to another (default) 10 mintues.
            lastWeatherChange = GetGameTimer();
        }

        [EventHandler("vMenu:UpdateServerBlackout")]
        internal void UpdateBlackout([FromSource] Player source, bool value)
        {
            if (!PermissionsManager.IsAllowed(PermissionsManager.Permission.WOBlackout, source) && !PermissionsManager.IsAllowed(PermissionsManager.Permission.WOAll, source))
            {
                BanManager.BanCheater(source);
                return;
            }

            BlackoutEnabled = value;
        }

        [EventHandler("vMenu:UpdateServerVehicleBlackout")]
        internal void UpdateVehicleBlackout([FromSource] Player source, bool value)
        {
            if (!PermissionsManager.IsAllowed(PermissionsManager.Permission.WOVehBlackout, source) && !PermissionsManager.IsAllowed(PermissionsManager.Permission.WOAll, source))
            {
                BanManager.BanCheater(source);
                return;
            }

            VehicleBlackoutEnabled = value;
        }

        /// <summary>
        /// Set a new random clouds type and opacity for all clients.
        /// </summary>
        /// <param name="removeClouds"></param>
        [EventHandler("vMenu:UpdateServerWeatherCloudsType")]
        internal void UpdateWeatherCloudsType([FromSource] Player source, bool removeClouds)
        {
            if (removeClouds)
            {
                if (source != null && !PermissionsManager.IsAllowed(PermissionsManager.Permission.WORemoveClouds, source))
                {
                    BanManager.BanCheater(source);
                    return;
                }

                TriggerClientEvent("vMenu:SetClouds", 0f, "removed");
            }
            else
            {
                if (source != null && !PermissionsManager.IsAllowed(PermissionsManager.Permission.WORandomizeClouds, source))
                {
                    BanManager.BanCheater(source);
                    return;
                }

                var opacity = (float)_rng.NextDouble();
                var type = CloudTypes[_rng.Next(0, CloudTypes.Count)];
                TriggerClientEvent("vMenu:SetClouds", opacity, type);
            }
        }

        /// <summary>
        /// Set and sync the time to all clients.
        /// </summary>
        /// <param name="newHours"></param>
        /// <param name="newMinutes"></param>
        [EventHandler("vMenu:UpdateServerTime")]
        internal async void UpdateTime([FromSource] Player source, int newHours, int newMinutes)
        {
            try
            {
                if (source != null && !PermissionsManager.IsAllowed(PermissionsManager.Permission.TOSetTime, source))
                {
                    BanManager.BanCheater(source);
                    return;
                }

                await SetTimeAsync(newHours, newMinutes);
            }
            catch (Exception ex)
            {
                Log($"UpdateServerTime failed: {ex.Message}", LogLevel.error);
            }
        }

        private int timeTransitionId = 0;
        // The frozen state from before a smooth transition started; null when none is running.
        private bool? frozenBeforeTransition = null;

        /// <summary>
        /// Sets the server time. With vmenu_smooth_time_transitions on, the clock is run forward to the new
        /// time over about a second instead of jumping (port of upstream's smooth time transitions).
        /// Leaves the frozen state as it was.
        /// </summary>
        private async Task SetTimeAsync(int newHours, int newMinutes)
        {
            newHours = MathUtil.Clamp(newHours, 0, 23);
            newMinutes = MathUtil.Clamp(newMinutes, 0, 59);
            var transitionId = ++timeTransitionId;

            if (GetSettingsBool(Setting.vmenu_smooth_time_transitions))
            {
                frozenBeforeTransition ??= FreezeTime;
                FreezeTime = true;

                var current = CurrentHours * 60 + CurrentMinutes;
                var target = newHours * 60 + newMinutes;
                var minutesToGo = (target - current + 24 * 60) % (24 * 60);
                const int steps = 30;
                for (var i = 1; i <= steps && transitionId == timeTransitionId; i++)
                {
                    var t = (current + minutesToGo * i / steps) % (24 * 60);
                    CurrentHours = t / 60;
                    CurrentMinutes = t % 60;
                    await Delay(33);
                }

                if (transitionId != timeTransitionId)
                {
                    // A newer time change took over and will restore the frozen state itself.
                    return;
                }
                FreezeTime = frozenBeforeTransition ?? FreezeTime;
                frozenBeforeTransition = null;
            }

            CurrentHours = newHours;
            CurrentMinutes = newMinutes;
        }

        /// <summary>
        /// Set and sync if time is frozen for all clients.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="freezeTime"></param>
        [EventHandler("vMenu:FreezeServerTime")]
        internal void FreezeServerTime([FromSource] Player source, bool freezeTime)
        {
            if (!PermissionsManager.IsAllowed(PermissionsManager.Permission.TOFreezeTime, source))
            {
                BanManager.BanCheater(source);
                return;
            }

            if (frozenBeforeTransition != null)
            {
                // Applied when the running smooth time transition finishes.
                frozenBeforeTransition = freezeTime;
                return;
            }
            FreezeTime = freezeTime;
        }
        #endregion

        #region Online Players Menu Actions
        /// <summary>
        /// Kick a specific player.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="kickReason"></param>
        [EventHandler("vMenu:KickPlayer")]
        internal void KickPlayer([FromSource] Player source, int target, string kickReason = "You have been kicked from the server.")
        {
            if (!PermissionsManager.IsAllowed(PermissionsManager.Permission.OPKick, source) && !PermissionsManager.IsAllowed(PermissionsManager.Permission.OPAll, source))
            {
                BanManager.BanCheater(source);
                return;
            }

            if (RateLimiter.IsOnlinePlayerActionLimited(source))
            {
                return;
            }

            Player targetPlayer = GetPlayerFromServerId(target);

            if (targetPlayer is null)
            {
                source.TriggerEvent("vMenu:Notify", "Failed to kick target, because the target could not be found. Did they already leave?");
                return;
            }

            if (PermissionsManager.IsAllowed(PermissionsManager.Permission.DontKickMe, targetPlayer))
            {
                source.TriggerEvent("vMenu:Notify", "Sorry, this player can ~r~not ~w~be kicked.");
                return;
            }

            kickReason = BanManager.SanitizeReason(kickReason, "You have been kicked from the server.");
            KickLog($"Player: {source.Name} has kicked: {targetPlayer.Name} for: {kickReason}.");

            source.TriggerEvent("vMenu:Notify", $"The target player (<C>{targetPlayer.Name}</C>) has been kicked.");

            targetPlayer.Drop(kickReason);
        }

        /// <summary>
        /// Kill a specific player.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        [EventHandler("vMenu:KillPlayer")]
        internal void KillPlayer([FromSource] Player source, int target)
        {
            if (!PermissionsManager.IsAllowed(PermissionsManager.Permission.OPKill, source) && !PermissionsManager.IsAllowed(PermissionsManager.Permission.OPAll, source))
            {
                BanManager.BanCheater(source);
                return;
            }

            if (RateLimiter.IsOnlinePlayerActionLimited(source))
            {
                return;
            }

            Player targetPlayer = GetPlayerFromServerId(target);

            if (targetPlayer is null)
            {
                return;
            }

            if (PermissionsManager.IsAllowed(PermissionsManager.Permission.DontKickMe, targetPlayer))
            {
                source.TriggerEvent("vMenu:Notify", "Sorry, this player can ~r~not ~w~be killed.");
                return;
            }

            targetPlayer.TriggerEvent("vMenu:KillMe", source.Name);
        }

        /// <summary>
        /// Teleport a specific player to another player.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        [EventHandler("vMenu:SummonPlayer")]
        internal async void SummonPlayer([FromSource] Player source, int target, int numberOfSeats)
        {
            // CitizenFX's [EventHandler] attribute binds the method to a void-returning delegate,
            // so the registered entry point must be `async void` (a Task-returning method fails to
            // bind: "Couldn't bind to method 'SummonPlayer'"). Wrap the awaitable logic so an
            // exception raised after the first await can't go unobserved and crash the script.
            try
            {
                await SummonPlayerInternal(source, target, numberOfSeats);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"^1[vMenu] [ERROR]^7 SummonPlayer failed: {ex.Message}");
            }
        }

        private async Task SummonPlayerInternal(Player source, int target, int numberOfSeats)
        {
            if (!PermissionsManager.IsAllowed(PermissionsManager.Permission.OPSummon, source) && !PermissionsManager.IsAllowed(PermissionsManager.Permission.OPAll, source))
            {
                BanManager.BanCheater(source);
                return;
            }

            if (RateLimiter.IsOnlinePlayerActionLimited(source))
            {
                return;
            }

            Player targetPlayer = GetPlayerFromServerId(target);

            if (targetPlayer is null)
            {
                return;
            }

            int targetPedHandle;
            Ped targetPed = targetPlayer.Character;

            if (targetPed is null || !DoesEntityExist(targetPedHandle = targetPed.Handle))
            {
                return;
            }

            bool lastVehicle = false;
            Ped sourcePed = source.Character;
            int sourcePedHandle = sourcePed.Handle;
            int sourcePedVehicle = GetVehiclePedIsIn(sourcePedHandle, lastVehicle);

            if (sourcePedVehicle == 0)
            {
                // Nothing to do when they are already standing right next to the summoner.
                if (sourcePed.Position.DistanceToSquared(targetPed.Position) <= 5f * 5f)
                {
                    source.TriggerEvent("vMenu:Notify", $"<C>{targetPlayer.Name}</C>~s~ is already standing right next to you.");
                    return;
                }

                targetPed.Position = sourcePed.Position;
                return;
            }

            if (GetVehiclePedIsIn(targetPedHandle, false) == sourcePedVehicle)
            {
                source.TriggerEvent("vMenu:Notify", $"<C>{targetPlayer.Name}</C>~s~ is already in your vehicle.");
                return;
            }

            bool seatFound = false;

            // Clamp the client-supplied seat count before using it as a loop bound.
            numberOfSeats = MathUtil.Clamp(numberOfSeats, 0, 16);

            // Seat indices start at `-1`
            numberOfSeats -= 1;

            for (int i = -1; i < numberOfSeats; i++)
            {
                bool seatFree = GetPedInVehicleSeat(sourcePedVehicle, i) == 0;

                if (!seatFree)
                {
                    continue;
                }

                Vector3 checkPosition;
                long timeout = GetGameTimer() + 1_500;
                Vector3 priorPosition = targetPed.Position;
                Vector3 newPosition = sourcePed.Position + new Vector3(0f, 0f, 5f);

                seatFound = true;
                targetPed.Position = newPosition;

                while (timeout > GetGameTimer() && priorPosition.DistanceToSquared(checkPosition = targetPed.Position) < newPosition.DistanceToSquared(checkPosition))
                {
                    await Delay(100);
                }

                if (timeout < GetGameTimer())
                {
                    source.TriggerEvent("vMenu:Notify", "Failed to teleport player.");
                    break;
                }

                SetPedIntoVehicle(targetPedHandle, sourcePedVehicle, i);
                break;
            }

            if (!seatFound)
            {
                source.TriggerEvent("vMenu:Notify", "No free seats in your vehicle for summoned player.");
            }
        }

        [EventHandler("vMenu:SendMessageToPlayer")]
        internal void SendPrivateMessage([FromSource] Player source, int target, string message)
        {
            if (!PermissionsManager.IsAllowed(PermissionsManager.Permission.OPSendMessage, source) && !PermissionsManager.IsAllowed(PermissionsManager.Permission.OPAll, source))
            {
                BanManager.BanCheater(source);
                return;
            }

            if (RateLimiter.IsLimited(source, "SendMessageToPlayer", 1000))
            {
                source.TriggerEvent("vMenu:Notify", "You are sending private messages too quickly.");
                return;
            }

            if (RateLimiter.IsOnlinePlayerActionLimited(source))
            {
                return;
            }

            // Cap the relayed message length (it is forwarded to the target and to the staff log).
            if (string.IsNullOrEmpty(message) || message.Length > 512)
            {
                source.TriggerEvent("vMenu:Notify", "Your message could not be sent because it was empty or too long.");
                return;
            }

            bool sourcePmsDisabled = source.State.Get("vmenu_pms_disabled") ?? false;

            if (sourcePmsDisabled)
            {
                source.TriggerEvent("vMenu:Notify", "You can't send a private message if you have private messages disabled yourself. Enable them in the Misc Settings menu and try again.");
                return;
            }

            Player targetPlayer = GetPlayerFromServerId(target);

            if (targetPlayer is null)
            {
                source.TriggerEvent("vMenu:Notify", "Failed to send message because the target could not be found. Did they disconnect?");
                return;
            }

            bool targetPmsDisabled = targetPlayer.State.Get("vmenu_pms_disabled") ?? false;

            if (targetPmsDisabled)
            {
                source.TriggerEvent("vMenu:Notify", $"Sorry, your private message to <C>{targetPlayer.Name}</C>~s~ could not be delivered because they have private messages disabled.");
                return;
            }

            targetPlayer.TriggerEvent("vMenu:PrivateMessage", source.Handle, message);

            foreach (string playerHandle in joinedPlayers.ToList())
            {
                if (!playerNotifyFlags.TryGetValue(playerHandle, out var flags) || !flags.SeesPrivateMessages)
                {
                    continue;
                }

                Player player = GetPlayerFromServerId(playerHandle);

                player?.TriggerEvent("vMenu:Notify", $"[vMenu Staff Log] <C>{source.Name}</C>~s~ sent a PM to <C>{targetPlayer.Name}</C>~s~: {message}");
            }
        }
        #endregion

        #region logging and update checks notifications
        /// <summary>
        /// If enabled using convars, will log all kick actions to the server console as well as an external file.
        /// </summary>
        /// <param name="kickLogMesage"></param>
        private static void KickLog(string kickLogMesage)
        {
            //if (GetConvar("vMenuLogKickActions", "true") == "true")
            if (GetSettingsBool(Setting.vmenu_log_kick_actions))
            {
                var file = LoadResourceFile(GetCurrentResourceName(), "vmenu.log") ?? "";
                var date = DateTime.Now;
                var formattedDate = (date.Day < 10 ? "0" : "") + date.Day + "-" +
                    (date.Month < 10 ? "0" : "") + date.Month + "-" +
                    (date.Year < 10 ? "0" : "") + date.Year + " " +
                    (date.Hour < 10 ? "0" : "") + date.Hour + ":" +
                    (date.Minute < 10 ? "0" : "") + date.Minute + ":" +
                    (date.Second < 10 ? "0" : "") + date.Second;
                var outputFile = file + $"[\t{formattedDate}\t] [KICK ACTION] {kickLogMesage}\n";
                SaveResourceFile(GetCurrentResourceName(), "vmenu.log", outputFile, -1);
                Debug.WriteLine("^3[vMenu] [KICK]^7 " + kickLogMesage + "\n");
            }
        }

        #endregion

        #region Infinity bits
        // TODO: Replace this logic and all child logic with statebags (server set, client read)
        [EventHandler("vMenu:RequestPlayerList")]
        internal void RequestPlayerListFromPlayer([FromSource] Player player)
        {
            // Clients ask for this whenever a player list is shown, so build it at most once a second.
            if (cachedPlayerList == null || GetGameTimer() - cachedPlayerListTime > 1000)
            {
                cachedPlayerList = Players.Select(p => new
                {
                    n = p.Name,
                    s = int.Parse(p.Handle),
                }).ToList<object>();
                cachedPlayerListTime = GetGameTimer();
            }
            player.TriggerEvent("vMenu:ReceivePlayerList", cachedPlayerList);
        }

        private List<object> cachedPlayerList = null;
        private long cachedPlayerListTime = 0;

        [EventHandler("vMenu:GetPlayerCoords")]
        internal void GetPlayerCoords([FromSource] Player source, long rpcId, int playerId, NetworkCallbackDelegate callback)
        {
            var coords = Vector3.Zero;

            // A refused request still gets its reply, with zero coords, which the callers already treat as a failure.
            if ((PermissionsManager.IsAllowed(PermissionsManager.Permission.OPTeleport, source) || PermissionsManager.IsAllowed(PermissionsManager.Permission.OPAll, source))
                && !RateLimiter.IsOnlinePlayerActionLimited(source))
            {
                Player targetPlayer = GetPlayerFromServerId(playerId);

                if (targetPlayer is not null)
                {
                    Ped targetPed = targetPlayer.Character;

                    if (targetPed is not null && DoesEntityExist(targetPed.Handle))
                    {
                        coords = targetPed.Position;
                    }
                }
            }

            source.TriggerEvent("vMenu:GetPlayerCoords:reply", rpcId, coords);
        }

        [EventHandler("vMenu:GetPlayerIdentifiers")]
        internal void GetPlayerIdentifiers([FromSource] Player source, int TargetPlayer, NetworkCallbackDelegate CallbackFunction)
        {
            if (!PermissionsManager.IsAllowed(PermissionsManager.Permission.OPIdentifiers, source) && !PermissionsManager.IsAllowed(PermissionsManager.Permission.OPAll, source))
            {
                BanManager.BanCheater(source);
                return;
            }

            if (TargetPlayer <= 0 || !DoesPlayerExist(TargetPlayer.ToString()))
            {
                CallbackFunction("[]");
                return;
            }

            var data = new List<string>();
            foreach (var identifier in Players[TargetPlayer].Identifiers)
            {
                if (!identifier.Contains("ip:"))
                {
                    data.Add(identifier);
                }
            }

            CallbackFunction(JsonConvert.SerializeObject(data));
        }
        #endregion

        #region Player join/quit
        private readonly HashSet<string> joinedPlayers = new();

        private sealed class NotifyFlags
        {
            public bool SeesJoinQuit;
            public bool SeesPrivateMessages;
        }

        // Worked out once per player when they join, instead of two ACE checks per player on every join, drop or PM.
        private readonly Dictionary<string, NotifyFlags> playerNotifyFlags = new();

        private void CachePlayerNotifyFlags(Player player)
        {
            playerNotifyFlags[player.Handle] = new NotifyFlags
            {
                SeesJoinQuit = PermissionsManager.IsAllowed(PermissionsManager.Permission.MSJoinQuitNotifs, player),
                SeesPrivateMessages = PermissionsManager.IsAllowed(PermissionsManager.Permission.OPSeePrivateMessages, player),
            };
        }

        private IEnumerable<Player> GetJoinQuitNotifPlayers()
        {
            List<Player> players = [];

            // Iterate a snapshot: other handlers (playerJoining/playerDropped) mutate joinedPlayers.
            foreach (string playerHandle in joinedPlayers.ToList())
            {
                if (!playerNotifyFlags.TryGetValue(playerHandle, out var flags) || !flags.SeesJoinQuit)
                {
                    continue;
                }

                Player player = GetPlayerFromServerId(playerHandle);

                if (player is not null)
                {
                    players.Add(player);
                }
            }

            return players;
        }

        private async Task PlayersFirstTick()
        {
            Tick -= PlayersFirstTick;

            // Allow plenty of time for the connected clients to restart their client scripts
            await Delay(3_000);

            foreach (var player in Players)
            {
                joinedPlayers.Add(player.Handle);

                PermissionsManager.SetPermissionsForPlayer(player);
                CachePlayerNotifyFlags(player);
            }
        }
        
        [EventHandler("playerJoining")]
        internal void OnPlayerJoining([FromSource] Player sourcePlayer)
        {
            // Only once per player; a repeat would just be a fake join notification.
            if (sourcePlayer == null || !joinedPlayers.Add(sourcePlayer.Handle))
            {
                return;
            }

            PermissionsManager.SetPermissionsForPlayer(sourcePlayer);
            CachePlayerNotifyFlags(sourcePlayer);

            string sourcePlayerName = sourcePlayer.Name;

            foreach (Player notifPlayer in GetJoinQuitNotifPlayers())
            {
                notifPlayer.TriggerEvent("vMenu:PlayerJoinQuit", sourcePlayerName, null);
            }
        }

        [EventHandler("playerDropped")]
        internal void OnPlayerDropped([FromSource] Player sourcePlayer, string reason)
        {
            if (!joinedPlayers.Contains(sourcePlayer.Handle))
            {
                return;
            }

            joinedPlayers.Remove(sourcePlayer.Handle);
            playerNotifyFlags.Remove(sourcePlayer.Handle);
            RateLimiter.Forget(sourcePlayer);

            string sourcePlayerName = sourcePlayer.Name;
            reason = BanManager.SanitizeReason(reason, null);
            
            foreach (Player notifPlayer in GetJoinQuitNotifPlayers())
            {
                notifPlayer.TriggerEvent("vMenu:PlayerJoinQuit", sourcePlayerName, reason);
            }
        }
        #endregion

        #region Utilities
        private Player GetPlayerFromServerId(string serverId)
        {
            if (!int.TryParse(serverId, out int serverIdInt))
            {
                return null;
            }

            return GetPlayerFromServerId(serverIdInt);
        }

        /// <summary>
        /// Whether the player spawned this vehicle, or sits in it (in the driver's seat when requireDriver is set).
        /// </summary>
        private static bool IsVehicleOwnedOrDrivenBy(Player player, int vehicleHandle, bool requireDriver)
        {
            if (player == null || vehicleHandle == 0 || !DoesEntityExist(vehicleHandle) || GetEntityType(vehicleHandle) != 2)
            {
                return false;
            }

            if (int.TryParse(player.Handle, out var serverId) && NetworkGetFirstEntityOwner(vehicleHandle) == serverId)
            {
                return true;
            }

            var ped = player.Character?.Handle ?? 0;
            if (ped == 0)
            {
                return false;
            }
            return requireDriver ? GetPedInVehicleSeat(vehicleHandle, -1) == ped : GetVehiclePedIsIn(ped, false) == vehicleHandle;
        }

        /// <summary>
        /// A minimum interval per player and action, for events that fan out to every client or do real work.
        /// </summary>
        internal static class RateLimiter
        {
            private const int DefaultOnlinePlayersLimit = 8;
            private const int DefaultOnlinePlayersWindowSeconds = 10;

            private static readonly Dictionary<string, long> lastCalls = new();

            // Allowances shared by a group of actions: the times of the calls still inside the window,
            // and when the player was last logged for going over, so the console hears about it once per window.
            private static readonly Dictionary<string, List<long>> recentCalls = new();
            private static readonly Dictionary<string, long> reportedAt = new();

            public static bool IsLimited(Player player, string action, int intervalMs)
            {
                if (player == null)
                {
                    return false;
                }
                var key = player.Handle + ":" + action;
                var now = GetGameTimer();
                if (lastCalls.TryGetValue(key, out var last) && now - last < intervalMs)
                {
                    return true;
                }
                lastCalls[key] = now;
                return false;
            }

            /// <summary>
            /// The shared allowance for every Online Players action aimed at another player. Tells the player
            /// how long to wait when they are over it.
            /// </summary>
            public static bool IsOnlinePlayerActionLimited(Player player)
            {
                // A missing convar reads as -1 and gets the default; only an explicit 0 turns the limit off.
                var limit = GetSettingsInt(Setting.vmenu_online_players_action_limit);
                var windowSeconds = GetSettingsInt(Setting.vmenu_online_players_action_limit_seconds);
                limit = limit < 0 ? DefaultOnlinePlayersLimit : limit;
                windowSeconds = windowSeconds < 0 ? DefaultOnlinePlayersWindowSeconds : windowSeconds;

                if (!IsOverAllowance(player, "OnlinePlayers", limit, windowSeconds * 1000, out var retryAfterSeconds))
                {
                    return false;
                }

                player.TriggerEvent("vMenu:Notify", $"You are doing that too quickly. Try again in ~y~{retryAfterSeconds}~s~ second(s).");
                return true;
            }

            /// <summary>
            /// At most <paramref name="limit"/> calls per <paramref name="windowMs"/>, counted across every action
            /// that shares the <paramref name="group"/>. A limit or window of 0 or less means no limit.
            /// </summary>
            public static bool IsOverAllowance(Player player, string group, int limit, int windowMs, out int retryAfterSeconds)
            {
                retryAfterSeconds = 0;

                if (player == null || limit <= 0 || windowMs <= 0)
                {
                    return false;
                }

                var key = player.Handle + ":" + group;
                var now = GetGameTimer();

                if (!recentCalls.TryGetValue(key, out var stamps))
                {
                    stamps = new List<long>();
                    recentCalls[key] = stamps;
                }

                // Drop the calls that have aged out of the window.
                while (stamps.Count > 0 && now - stamps[0] >= windowMs)
                {
                    stamps.RemoveAt(0);
                }

                if (stamps.Count < limit)
                {
                    stamps.Add(now);
                    return false;
                }

                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((windowMs - (now - stamps[0])) / 1000f));

                if (!reportedAt.TryGetValue(key, out var lastReport) || now - lastReport >= windowMs)
                {
                    reportedAt[key] = now;
                    Debug.WriteLine($"^3[vMenu] [WARNING]^7 {player.Name} ({player.Handle}) is over the {group} limit of {limit} action(s) per {windowMs / 1000}s. Refusing for another {retryAfterSeconds}s.");
                }

                return true;
            }

            public static void Forget(Player player)
            {
                if (player == null)
                {
                    return;
                }
                var prefix = player.Handle + ":";
                foreach (var key in lastCalls.Keys.Where(k => k.StartsWith(prefix)).ToList())
                {
                    lastCalls.Remove(key);
                }
                foreach (var key in recentCalls.Keys.Where(k => k.StartsWith(prefix)).ToList())
                {
                    recentCalls.Remove(key);
                }
                foreach (var key in reportedAt.Keys.Where(k => k.StartsWith(prefix)).ToList())
                {
                    reportedAt.Remove(key);
                }
            }
        }

        private Player GetPlayerFromServerId(int serverId)
        {
            string serverIdString = serverId.ToString();

            if (serverId <= 0 || !DoesPlayerExist(serverIdString))
            {
                return null;
            }

            return Players[serverId];
        }
        #endregion

        #region Change Engine Sound
        [EventHandler("vMenu:changeEngineSound")]
        internal void ChangeEngineSound([FromSource] Player source, int vehicle, string engineSound)
        {
            if (!PermissionsManager.IsAllowed(PermissionsManager.Permission.VOEngineSound, source) && (!PermissionsManager.IsAllowed(PermissionsManager.Permission.VOAll, source)))  
            {
                BanManager.BanCheater(source);
                return;
            }

            // Reject empty/oversized values and anything that isn't a plain engine-sound name
            // (engine sound names are model-style identifiers: letters, digits and underscores).
            if (string.IsNullOrEmpty(engineSound) || engineSound.Length > 64 || !engineSound.All(c => char.IsLetterOrDigit(c) || c == '_'))
            {
                return;
            }

            Vehicle veh = new Vehicle(NetworkGetEntityFromNetworkId(vehicle));
            if (veh == null || !DoesEntityExist(veh.Handle))
            {
                return;
            }

            // Only a vehicle the player is in or spawned, and only a sound this player was offered.
            if (!IsVehicleOwnedOrDrivenBy(source, veh.Handle, requireDriver: false)
                || (engineSound != "resetenginesound" && !AddonsConfig.IsEngineSoundAllowed(source, engineSound)))
            {
                return;
            }

            EnsureEntityStateBag(veh.Handle);

            veh.State.Set("vMenu:engineSound", engineSound, true);
        }
        #endregion
    }
}