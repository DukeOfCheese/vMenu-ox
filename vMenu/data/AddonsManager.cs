using System.Collections.Generic;

using CitizenFX.Core;

using Newtonsoft.Json;

namespace vMenuClient.data
{
    /// <summary>
    /// Runtime registry of addon vehicles/weapons/EUP the local player is permitted to use.
    /// Populated from the server's "vMenu:SetAddons" payload (see build/vMenu/server/addons.lua).
    /// Field names are lower-case to match the Lua json.encode keys.
    /// </summary>
    public static class AddonsManager
    {
        public class AddonVehicle
        {
            public string spawn;
            public string label;
        }

        public class AddonWeapon
        {
            public string spawn;
            public string label;
        }

        public class EupState
        {
            // Default true => "no restriction" (also used when EUP gating is disabled server-side).
            public bool allowed = true;
        }

        private class Payload
        {
            public List<AddonVehicle> vehicles;
            public List<AddonWeapon> weapons;
            public EupState eup;
        }

        public static List<AddonVehicle> Vehicles { get; private set; } = new List<AddonVehicle>();
        public static List<AddonWeapon> Weapons { get; private set; } = new List<AddonWeapon>();
        public static EupState Eup { get; private set; } = new EupState();

        public static bool HasAny => Vehicles.Count > 0 || Weapons.Count > 0;

        public static void Load(string json)
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<Payload>(json) ?? new Payload();
                Vehicles = payload.vehicles ?? new List<AddonVehicle>();
                Weapons = payload.weapons ?? new List<AddonWeapon>();
                Eup = payload.eup ?? new EupState();
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[vMenu] [Addons] Failed to parse addon payload: {ex.Message}");
                Vehicles = new List<AddonVehicle>();
                Weapons = new List<AddonWeapon>();
                Eup = new EupState();
            }
        }
    }
}
