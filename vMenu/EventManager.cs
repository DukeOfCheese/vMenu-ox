using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CitizenFX.Core;

using Newtonsoft.Json;

using vMenuClient.data;
using vMenuClient.menus;

using static CitizenFX.Core.Native.API;
using static vMenuClient.CommonFunctions;
using static vMenuShared.ConfigManager;
using static vMenuShared.PermissionsManager;

namespace vMenuClient
{
    public class EventManager : BaseScript
    {
        public static WeatherOptions WeatherOptionsMenu { get; private set; }
        /// <summary>
        /// Whether the server enabled snow. Read every frame by the snowball pickup check, so the
        /// convar is only read again once a second.
        /// </summary>
        public static bool IsSnowEnabled
        {
            get
            {
                if (!snowEnabledRead || GetGameTimer() - snowEnabledReadTime >= 1000)
                {
                    snowEnabled = GetSettingsBool(Setting.vmenu_enable_snow);
                    snowEnabledReadTime = GetGameTimer();
                    snowEnabledRead = true;
                }
                return snowEnabled;
            }
        }
        private static bool snowEnabled = false;
        private static bool snowEnabledRead = false;
        private static int snowEnabledReadTime = 0;
        public static int GetServerMinutes => MathUtil.Clamp(GetSettingsInt(Setting.vmenu_current_minute), 0, 59);
        public static int GetServerHours => MathUtil.Clamp(GetSettingsInt(Setting.vmenu_current_hour), 0, 23);
        public static int GetServerMinuteDuration => GetSettingsInt(Setting.vmenu_ingame_minute_duration);
        public static bool IsServerTimeFrozen => GetSettingsBool(Setting.vmenu_freeze_time);
        public static bool IsServerTimeSyncedWithMachineTime => GetSettingsBool(Setting.vmenu_sync_to_machine_time);
        public static string GetServerWeather => GetSettingsString(Setting.vmenu_current_weather, "CLEAR");
        public static bool DynamicWeatherEnabled => GetSettingsBool(Setting.vmenu_enable_dynamic_weather);
        public static bool IsBlackoutEnabled => GetSettingsBool(Setting.vmenu_blackout_enabled);
        public static bool IsVehicleLightsEnabled => GetSettingsBool(Setting.vmenu_vehicle_blackout_enabled);
        public static int WeatherChangeTime => MathUtil.Clamp(GetSettingsInt(Setting.vmenu_weather_change_duration), 0, 45);

        /// <summary>
        /// Constructor.
        /// </summary>
        public EventManager()
        {
            EventHandlers.Add("vMenu:SetConfigOptions", new Action(SetConfigOptions));
            EventHandlers.Add("vMenu:SetAddons", new Action<string>(SetAddons));
            EventHandlers.Add("vMenu:SetPermissions", new Action<string>(MainMenu.SetPermissions));
            EventHandlers.Add("vMenu:KillMe", new Action<string>(KillMe));
            EventHandlers.Add("vMenu:ForceOutOfVehicle", new Action<int>(ForceOutOfVehicle));
            EventHandlers.Add("vMenu:Notify", new Action<string>(NotifyPlayer));
            EventHandlers.Add("vMenu:SetClouds", new Action<float, string>(SetClouds));
            EventHandlers.Add("vMenu:GoodBye", new Action(GoodBye));
            EventHandlers.Add("vMenu:SetBanList", new Action<string>(UpdateBanList));
            EventHandlers.Add("vMenu:ClearArea", new Action<Vector3>(ClearAreaNearPos));
            EventHandlers.Add("vMenu:updatePedDecors", new Action(UpdatePedDecors));
            EventHandlers.Add("playerSpawned", new Action(SetAppearanceOnFirstSpawn));
            EventHandlers.Add("vMenu:PrivateMessage", new Action<string, string>(PrivateMessage));
            EventHandlers.Add("vMenu:UpdateTeleportLocations", new Action<string>(UpdateTeleportLocations));

            if (GetSettingsBool(Setting.vmenu_enable_weather_sync))
            {
                Tick += WeatherSync;
            }

            if (GetSettingsBool(Setting.vmenu_enable_time_sync))
            {
                Tick += TimeSync;
            }
        }

        private bool firstSpawn = true;
        /// <summary>
        /// Sets the saved character whenever the player first spawns.
        /// </summary>
        private async void SetAppearanceOnFirstSpawn()
        {
            try
            {
                if (firstSpawn)
                {
                    firstSpawn = false;
                    if (MainMenu.MiscSettingsMenu != null && MainMenu.MpPedCustomizationMenu != null && MainMenu.MiscSettingsMenu.MiscRespawnDefaultCharacter && !string.IsNullOrEmpty(GetResourceKvpString("vmenu_default_character")) && !GetSettingsBool(Setting.vmenu_disable_spawning_as_default_character))
                    {
                        await MainMenu.MpPedCustomizationMenu.SpawnThisCharacter(GetResourceKvpString("vmenu_default_character"), false);
                    }
                    var fadeWaitStart = GetGameTimer();
                    while (!IsScreenFadedIn() || IsPlayerSwitchInProgress() || IsPauseMenuActive() || GetIsLoadingScreenActive())
                    {
                        if (GetGameTimer() - fadeWaitStart > 30000)
                        {
                            Debug.WriteLine("[vMenu] [Warning] SetAppearanceOnFirstSpawn timed out waiting for the screen to fade in.");
                            break;
                        }
                        await Delay(0);
                    }
                    if (MainMenu.WeaponLoadoutsMenu != null && MainMenu.WeaponLoadoutsMenu.WeaponLoadoutsSetLoadoutOnRespawn && IsAllowed(Permission.WLEquipOnRespawn))
                    {
                        var saveName = GetResourceKvpString("vmenu_string_default_loadout");
                        if (!string.IsNullOrEmpty(saveName))
                        {
                            await SpawnWeaponLoadoutAsync(saveName, true, false, true);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[vMenu] Exception in SetAppearanceOnFirstSpawn: {ex}");
            }
        }

        /// <summary>
        /// Sets the extras file.
        /// </summary>
        private void SetConfigOptions()
        {
            // Ask the server for this player's permitted addons. Tied to the config-ready
            // signal so it also re-fires when vMenu restarts with players connected.
            // The reply carries the vehicle extras and map locations too.
            TriggerServerEvent("vMenu:RequestAddons");

            MainMenu.ConfigOptionsSetupComplete = true;
        }

        /// <summary>
        /// Receives the per-player addon payload (allowed vehicles/weapons + EUP flag) from the server.
        /// </summary>
        private void SetAddons(string json)
        {
            AddonsManager.Load(json);
            SetExtras();
            MiscSettings.TpLocations = AddonsManager.Teleports;
            MainMenu.AddonsSetupComplete = true;
        }


        /// <summary>
        /// Sets the named vehicle extras from Config.Extras in the server's Lua config.
        /// Two models that hash to the same key would be a config mistake, so the first wins and
        /// the clash is reported rather than silently merged.
        /// </summary>
        private void SetExtras()
        {
            VehicleOptions.VehicleExtras = new Dictionary<uint, Dictionary<int, string>>();

            foreach (var model in AddonsManager.Extras)
            {
                if (model.Value == null || model.Value.Count == 0)
                {
                    continue;
                }

                var modelHash = Game.GenerateHashASCII(model.Key);
                if (VehicleOptions.VehicleExtras.ContainsKey(modelHash))
                {
                    Debug.WriteLine($"[vMenu] [Warning] Config.Extras has more than one entry for model '{model.Key}'; only the first is used.");
                    continue;
                }

                VehicleOptions.VehicleExtras.Add(modelHash, model.Value);
            }
        }

        /// <summary>
        /// Update ban list.
        /// </summary>
        /// <param name="list"></param>
        private void UpdateBanList(string list)
        {
            MainMenu.BannedPlayersMenu?.UpdateBanList(list);
        }

        /// <summary>
        /// Used for cheaters.
        /// </summary>
        private void GoodBye()
        {
            ForceSocialClubUpdate();
        }

        /// <summary>
        /// Loads/unloads the snow fx particles if needed.
        /// </summary>
        private async Task UpdateWeatherParticles()
        {
            ForceSnowPass(IsSnowEnabled);
            SetForceVehicleTrails(IsSnowEnabled);
            SetForcePedFootstepsTracks(IsSnowEnabled);
            if (IsSnowEnabled)
            {
                if (!HasNamedPtfxAssetLoaded("core_snow"))
                {
                    RequestNamedPtfxAsset("core_snow");
                    while (!HasNamedPtfxAssetLoaded("core_snow"))
                    {
                        await Delay(0);
                    }
                }
                UseParticleFxAssetNextCall("core_snow");
            }
            else
            {
                RemoveNamedPtfxAsset("core_snow");
            }
        }

        /// <summary>
        /// OnTick loop to keep the weather synced.
        /// </summary>
        /// <returns></returns>
        // Tracks the game timer (ms) at which the weather change is expected to complete.
        // 0 means no weather change is currently in progress.
        private int weatherChangeCompleteTimer = 0;
        private async Task WeatherSync()
        {
            await UpdateWeatherParticles();
            SetArtificialLightsState(IsBlackoutEnabled);
            SetArtificialLightsStateAffectsVehicles(!IsVehicleLightsEnabled);

            if (weatherChangeCompleteTimer == 0)
            {
                // No change in progress: start one if the weather no longer matches the server's.
                if (GetNextWeatherType() != GetHashKey(GetServerWeather))
                {
                    SetWeatherTypeOvertimePersist(GetServerWeather, (float)WeatherChangeTime);
                    weatherChangeCompleteTimer = GetGameTimer() + (WeatherChangeTime * 1000) + 2000;
                }
            }
            else if (GetGameTimer() >= weatherChangeCompleteTimer)
            {
                // Enough time has elapsed for the in-progress change to finish.
                weatherChangeCompleteTimer = 0;
                TriggerEvent("vMenu:WeatherChangeComplete", GetServerWeather);
            }

            await Delay(1000);
        }


        /// <summary>
        /// This function will take care of time sync. It'll be called once, and never stop.
        /// </summary>
        /// <returns></returns>
        // Whether TimeSync currently has the game clock paused (null until the first run, so the
        // current state is always written once, e.g. after a restart that left the clock paused).
        private bool? clockPausedByTimeSync = null;

        /// <summary>
        /// Lets the clock run again if vMenu stops while it had it paused.
        /// </summary>
        [EventHandler("onClientResourceStop")]
        private void OnClientResourceStop(string resourceName)
        {
            if (resourceName == GetCurrentResourceName() && clockPausedByTimeSync == true)
            {
                PauseClock(false);
            }
        }

        private async Task TimeSync()
        {
            NetworkOverrideClockTime(GetServerHours, GetServerMinutes, 0);

            // Frozen or machine-synced time only changes when the server says so, so the clock is paused
            // instead of being pinned back every few milliseconds; once a second is enough to pick up a change.
            var serverOwnsMinute = IsServerTimeFrozen || IsServerTimeSyncedWithMachineTime;
            if (serverOwnsMinute != clockPausedByTimeSync)
            {
                PauseClock(serverOwnsMinute);
                clockPausedByTimeSync = serverOwnsMinute;
            }

            if (serverOwnsMinute)
            {
                await Delay(1000);
            }
            else
            {
                await Delay(MathUtil.Clamp(GetServerMinuteDuration, 100, 2000));
            }
        }

        /// <summary>
        /// Set the cloud hat type.
        /// </summary>
        /// <param name="opacity"></param>
        /// <param name="cloudsType"></param>
        private void SetClouds(float opacity, string cloudsType)
        {
            if (opacity == 0f && cloudsType == "removed")
            {
                ClearCloudHat();
            }
            else
            {
                SetCloudHatOpacity(opacity);
                SetCloudHatTransition(cloudsType, 4f);
            }
        }

        /// <summary>
        /// Used by events triggered from the server to notify a user.
        /// </summary>
        /// <param name="message"></param>
        bool usingCustomNotifications = GetSettingsBool(Setting.vmenu_using_custom_notify);
        private void NotifyPlayer(string message)
        {
            if (usingCustomNotifications)
            {
                TriggerEvent("vMenu:CustomNotify", message, "inform");
            }
            else
            {
                Notify.Custom(message, true, true);
            }
        }

        /// <summary>
        /// Kill this player, poor thing, someone wants you dead... R.I.P.
        /// </summary>
        private void KillMe(string sourceName)
        {
            if (!string.IsNullOrEmpty(sourceName) && sourceName.Length > 32)
            {
                sourceName = sourceName.Substring(0, 32);
            }
            Notify.Alert($"You have been killed by <C>{GetSafePlayerName(sourceName)}</C>~s~ using the ~r~Kill Player~s~ option in vMenu.");
            SetEntityHealth(Game.PlayerPed.Handle, 0);
        }

        // TaskLeaveVehicle flags: resume if interrupted (1), don't wait for the vehicle to stop (64),
        // don't close the door (256) and jump out (4096). Flag 16 alone (warp out) was often ignored.
        private const int ForceOutOfVehicleFlags = 1 | 64 | 256 | 4096;

        /// <summary>
        /// The owner of the personal vehicle this player is sitting in has kicked everyone out of it.
        /// </summary>
        private async void ForceOutOfVehicle(int vehicleNetId)
        {
            try
            {
                var vehicle = NetworkGetEntityFromNetworkId(vehicleNetId);
                var ped = Game.PlayerPed.Handle;

                if (vehicle == 0 || !DoesEntityExist(vehicle) || GetVehiclePedIsIn(ped, false) != vehicle)
                {
                    return;
                }

                TaskLeaveVehicle(ped, vehicle, ForceOutOfVehicleFlags);
                NotifyPlayer("The owner of the vehicle threw you out of it.");

                // Give the leave task a second, then pull the ped out and stand them beside the vehicle.
                for (var check = 0; check < 10; check++)
                {
                    await Delay(100);

                    if (!DoesEntityExist(vehicle) || GetVehiclePedIsIn(ped, false) != vehicle)
                    {
                        return;
                    }
                }

                var beside = GetOffsetFromEntityInWorldCoords(vehicle, 2.5f, 0f, 0f);
                ClearPedTasksImmediately(ped);
                SetEntityCoords(ped, beside.X, beside.Y, beside.Z, false, false, false, true);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[vMenu] Failed to leave the personal vehicle: {e.Message}");
            }
        }

        /// <summary>
        /// Clear the area around the provided x, y, z coordinates. Clears everything like (destroyed) objects, peds, (ai) vehicles, etc.
        /// Also restores broken streetlights, etc.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        private void ClearAreaNearPos(Vector3 position) => ClearAreaOfEverything(position.X, position.Y, position.Z, 100f, false, false, false, false);

        /// <summary>
        /// Updates ped decorators for the clothing animation when players have joined.
        /// </summary>
        private async void UpdatePedDecors()
        {
            try
            {
                await Delay(1000);
                var backup = PlayerAppearance.ClothingAnimationType;
                PlayerAppearance.ClothingAnimationType = -1;
                await Delay(100);
                PlayerAppearance.ClothingAnimationType = backup;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[vMenu] Exception in UpdatePedDecors: {ex}");
            }
        }

        /// <summary>
        /// Updates the teleport locations after someone saves a new one, so every client stays in sync.
        /// </summary>
        /// <param name="jsonData"></param>
        private void UpdateTeleportLocations(string jsonData)
        {
            try
            {
                MiscSettings.TpLocations = JsonConvert.DeserializeObject<List<vMenuShared.ConfigManager.TeleportLocation>>(jsonData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[vMenu] Exception in UpdateTeleportLocations: {ex}");
                MiscSettings.TpLocations = new List<vMenuShared.ConfigManager.TeleportLocation>();
            }
        }
    }
}
