using System;
using System.Collections.Generic;
using System.Linq;

using CitizenFX.Core;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using vMenuClient.data;

using static CitizenFX.Core.Native.API;
using static vMenuClient.CommonFunctions;

namespace vMenuClient
{
    /// <summary>
    /// Applies vehicle and weapon loadout share codes for client/vehicleCodes.lua and client/loadoutCodes.lua.
    /// A code is written by whoever generated it, so nothing in it is trusted: it is rebuilt here from what
    /// this client knows (the weapon list it was given, the vehicle info format) and goes through the same
    /// permission checks as the menus. Outfit codes live in MpPedCustomization, next to the EUP caps.
    /// Every export returns an empty string on success, or the message to show the player.
    /// </summary>
    public class SharedCodes : BaseScript
    {
        private const string LoadoutPrefix = "vmenu_string_saved_weapon_loadout_";
        private const string VehiclePrefix = "veh_";
        private const int MaxLoadoutWeapons = 128;

        public SharedCodes()
        {
            Exports.Add("ImportSharedLoadout", new Func<object, object, string>((saveName, json) => ImportLoadout(saveName as string, json as string)));
            Exports.Add("EquipSharedLoadout", new Func<object, string>(json => EquipLoadout(json as string)));
            Exports.Add("ImportSharedVehicle", new Func<object, object, string>((saveName, json) => ImportVehicle(saveName as string, json as string)));
        }

        private static bool IsValidSaveName(string saveName) => !string.IsNullOrWhiteSpace(saveName) && saveName.Length <= 30;

        /// <summary>
        /// Parses a code's JSON object. Codes created before the share-code rework were re-encoded by Lua, which
        /// writes an empty dictionary as [], so empty arrays in the given dictionary fields are read as {}.
        /// </summary>
        internal static T ParseCode<T>(string json, params string[] dictionaryFields)
        {
            var token = JObject.Parse(json ?? "");
            foreach (var field in dictionaryFields)
            {
                if (token[field] is JArray array && array.Count == 0)
                {
                    token[field] = new JObject();
                }
            }
            return token.ToObject<T>();
        }

        #region Loadouts
        /// <summary>
        /// Rebuilds a shared loadout from this client's weapon list. Weapons this client doesn't know (including
        /// addon weapons the server didn't give this player) are dropped, the permission and addon flag come
        /// from the local list rather than the code, and only components the weapon really takes are kept.
        /// </summary>
        private static List<ValidWeapon> RebuildLoadout(string json, out string error)
        {
            error = null;
            List<ValidWeapon> shared;
            try
            {
                shared = JsonConvert.DeserializeObject<List<ValidWeapon>>(json ?? "");
            }
            catch
            {
                shared = null;
            }
            if (shared == null || shared.Count == 0)
            {
                error = "That loadout code doesn't contain a valid loadout.";
                return null;
            }

            var known = new Dictionary<uint, ValidWeapon>();
            foreach (var weapon in ValidWeapons.WeaponList)
            {
                known[weapon.Hash] = weapon;
            }

            var rebuilt = new List<ValidWeapon>();
            foreach (var weapon in shared.Take(MaxLoadoutWeapons))
            {
                if (!known.TryGetValue(weapon.Hash, out var local) || rebuilt.Any(w => w.Hash == local.Hash))
                {
                    continue;
                }

                var components = new Dictionary<string, uint>();
                foreach (var component in local.Components ?? new Dictionary<string, uint>())
                {
                    if (weapon.Components != null && weapon.Components.Values.Contains(component.Value))
                    {
                        components[component.Key] = component.Value;
                    }
                }

                rebuilt.Add(new ValidWeapon
                {
                    Hash = local.Hash,
                    Name = local.Name,
                    SpawnName = local.SpawnName,
                    Perm = local.Perm,
                    IsAddon = local.IsAddon,
                    Components = components,
                    CurrentAmmo = MathUtil.Clamp(weapon.CurrentAmmo, -1, 9999),
                    CurrentTint = MathUtil.Clamp(weapon.CurrentTint, 0, 32),
                });
            }

            if (rebuilt.Count == 0)
            {
                error = "None of the weapons in that loadout exist on this server.";
                return null;
            }
            return rebuilt;
        }

        private static string ImportLoadout(string saveName, string json)
        {
            if (!IsValidSaveName(saveName))
            {
                return "Invalid save name.";
            }
            var key = LoadoutPrefix + saveName;
            if (!string.IsNullOrEmpty(GetResourceKvpString(key)))
            {
                return "You have a weapon loadout with that name already!";
            }

            var loadout = RebuildLoadout(json, out var error);
            if (loadout == null)
            {
                return error;
            }

            SetResourceKvp(key, JsonConvert.SerializeObject(loadout));
            // Equipping goes through the normal loadout path: permission per weapon and the "spawnloadout" interaction check.
            var spawnTask = SpawnWeaponLoadoutAsync(key, true, false, false);
            return string.Empty;
        }

        private static string EquipLoadout(string json)
        {
            if (!CanDoInteraction("spawnloadout"))
            {
                return "You can't load a weapon loadout right now.";
            }

            var loadout = RebuildLoadout(json, out var error);
            if (loadout == null)
            {
                return error;
            }

            var giveTask = GiveWeaponLoadoutAsync(loadout, true, false, false);
            return string.Empty;
        }
        #endregion

        #region Vehicles
        private static string ImportVehicle(string saveName, string json)
        {
            if (!IsValidSaveName(saveName))
            {
                return "Invalid save name.";
            }
            var key = VehiclePrefix + saveName;
            if (!string.IsNullOrEmpty(GetResourceKvpString(key)))
            {
                return "You have a vehicle saved with that name already!";
            }

            VehicleInfo vehicle;
            try
            {
                vehicle = ParseCode<VehicleInfo>(json, "colors", "extras", "mods");
            }
            catch
            {
                return "That vehicle code doesn't contain a valid vehicle.";
            }
            if (vehicle.model == 0)
            {
                return "That vehicle code doesn't contain a valid vehicle.";
            }

            // Normalise everything the saved vehicles menu and the spawner rely on being present and sane.
            vehicle.colors ??= new Dictionary<string, int>();
            vehicle.extras ??= new Dictionary<int, bool>();
            vehicle.mods ??= new Dictionary<int, int>();
            vehicle.name = string.IsNullOrEmpty(vehicle.name) ? "" : (vehicle.name.Length > 64 ? vehicle.name.Substring(0, 64) : vehicle.name);
            vehicle.plateText = string.IsNullOrEmpty(vehicle.plateText) ? "" : (vehicle.plateText.Length > 8 ? vehicle.plateText.Substring(0, 8) : vehicle.plateText);
            if (float.IsNaN(vehicle.enveffScale) || float.IsInfinity(vehicle.enveffScale))
            {
                vehicle.enveffScale = 0f;
            }
            // The sender's categories don't exist on this client.
            vehicle.Category = "Uncategorized";

            if (!StorageManager.SaveVehicleInfo(key, vehicle, false))
            {
                return "The vehicle could not be saved.";
            }
            return string.Empty;
        }
        #endregion
    }
}
