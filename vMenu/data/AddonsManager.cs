using System.Collections.Generic;

using CitizenFX.Core;

using Newtonsoft.Json;

using static vMenuShared.ConfigManager;

namespace vMenuClient.data
{
    /// <summary>
    /// Everything this player is permitted to see that was authored in the server's Lua config:
    /// addon vehicles/weapons/peds/engine sounds, named vehicle extras, and the map locations.
    /// Populated from the server's "vMenu:SetAddons" payload (see vMenuServer/AddonsConfig.cs).
    ///
    /// The server has already applied ACE permissions before sending, so anything in here is
    /// allowed by definition -- there is no client-side filtering left to do. Menu construction
    /// waits on MainMenu.AddonsSetupComplete, so this is fully populated before any menu is built.
    ///
    /// Field names are lower-case to match the JSON keys.
    /// </summary>
    public static class AddonsManager
    {
        public class AddonItem
        {
            public string spawn;
            public string label;
        }

        public class AddonWeapon
        {
            public string spawn;
            public string label;
            public List<AddonItem> components;
        }

        public class AddonPed
        {
            public string spawn;
            public string label;
            /// <summary>main, animal, male, female or other.</summary>
            public string category;
        }

        public class EupState
        {
            // Default true => "no restriction" (also used when EUP gating is disabled server-side).
            public bool allowed = true;
        }

        private class Payload
        {
            [JsonProperty("vehicles")]     public List<AddonItem>   vehicles     { get; set; }
            [JsonProperty("weapons")]      public List<AddonWeapon> weapons      { get; set; }
            [JsonProperty("peds")]         public List<AddonPed>    peds         { get; set; }
            [JsonProperty("engineSounds")] public List<AddonItem>   engineSounds { get; set; }
            [JsonProperty("extras")]       public Dictionary<string, Dictionary<int, string>> extras { get; set; }
            [JsonProperty("locations")]    public Locations         locations    { get; set; }
            [JsonProperty("eup")]          public EupState          eup          { get; set; }
        }

        public static List<AddonItem> Vehicles { get; private set; } = new List<AddonItem>();
        public static List<AddonWeapon> Weapons { get; private set; } = new List<AddonWeapon>();
        public static List<AddonPed> Peds { get; private set; } = new List<AddonPed>();
        public static List<AddonItem> EngineSounds { get; private set; } = new List<AddonItem>();
        public static Dictionary<string, Dictionary<int, string>> Extras { get; private set; } = new Dictionary<string, Dictionary<int, string>>();
        public static List<TeleportLocation> Teleports { get; private set; } = new List<TeleportLocation>();
        public static List<LocationBlip> Blips { get; private set; } = new List<LocationBlip>();
        public static EupState Eup { get; private set; } = new EupState();

        public static void Load(string json)
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<Payload>(json) ?? new Payload();
                Vehicles = payload.vehicles ?? new List<AddonItem>();
                Weapons = payload.weapons ?? new List<AddonWeapon>();
                Peds = payload.peds ?? new List<AddonPed>();
                EngineSounds = payload.engineSounds ?? new List<AddonItem>();
                Extras = payload.extras ?? new Dictionary<string, Dictionary<int, string>>();
                Teleports = payload.locations.teleports ?? new List<TeleportLocation>();
                Blips = payload.locations.blips ?? new List<LocationBlip>();
                Eup = payload.eup ?? new EupState();
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[vMenu] [Addons] Failed to parse addon payload: {ex.Message}");
                Reset();
            }
        }

        private static void Reset()
        {
            Vehicles = new List<AddonItem>();
            Weapons = new List<AddonWeapon>();
            Peds = new List<AddonPed>();
            EngineSounds = new List<AddonItem>();
            Extras = new Dictionary<string, Dictionary<int, string>>();
            Teleports = new List<TeleportLocation>();
            Blips = new List<LocationBlip>();
            Eup = new EupState();
        }
    }
}
