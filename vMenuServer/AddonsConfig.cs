using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CitizenFX.Core;

using Newtonsoft.Json;

using static CitizenFX.Core.Native.API;
using static vMenuShared.ConfigManager;
using static vMenuShared.PermissionsManager;

namespace vMenuServer
{
    /// <summary>
    /// Owns the Lua-authored configuration (config/config_server.lua, config/locations.lua) once
    /// server/addons.lua hands it over, and turns it into a per-player payload.
    ///
    /// Addon items are ACE gated here, on the server, so a player who is not permitted never
    /// receives the item at all. Each item derives its ACE in the same namespace vMenu already uses
    /// for base-game content (vMenu.WeaponOptions.&lt;spawn&gt;, vMenu.VehicleSpawner.&lt;spawn&gt;,
    /// vMenu.PlayerAppearance.&lt;spawn&gt;, vMenu.VehicleOptions.&lt;spawn&gt;) unless the entry sets
    /// its own "permission".
    /// </summary>
    public class AddonsConfig : BaseScript
    {
        private const string LocationsKvpKey = "vmenu_saved_locations";

        #region Lua-side config shapes
        public class AddonEntry
        {
            public string spawn { get; set; }
            public string label { get; set; }
            public string permission { get; set; }
        }

        public class AddonWeaponEntry : AddonEntry
        {
            public List<AddonEntry> components { get; set; }
        }

        public class AddonPedEntry : AddonEntry
        {
            public string category { get; set; }
        }

        public class EupEntry
        {
            public bool enabled { get; set; }
            public string permission { get; set; }
        }

        private class AddonsSection
        {
            public List<AddonEntry> vehicles { get; set; }
            public List<AddonWeaponEntry> weapons { get; set; }
            public List<AddonPedEntry> peds { get; set; }
            public List<AddonEntry> engineSounds { get; set; }
            public EupEntry eup { get; set; }
        }

        /// <summary>Flat shape emitted by config/locations.lua (x/y/z rather than a nested vector).</summary>
        private class FlatLocation
        {
            public string name { get; set; }
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
            public float heading { get; set; }
            public int sprite { get; set; }
            public int color { get; set; }
        }

        private class LocationsSection
        {
            public List<FlatLocation> teleports { get; set; }
            public List<FlatLocation> blips { get; set; }
        }

        private class LuaConfig
        {
            public AddonsSection addons { get; set; }
            public Dictionary<string, Dictionary<int, string>> extras { get; set; }
            public LocationsSection locations { get; set; }
        }
        #endregion

        #region Payload sent to clients
        /// <summary>
        /// What a client is told about an item it is allowed to use. Deliberately does not carry the
        /// entry's "permission": the client has no use for the ACE name and does not need to learn it.
        /// </summary>
        private class PayloadItem
        {
            public string spawn;
            public string label;
        }

        private class PayloadWeapon
        {
            public string spawn;
            public string label;
            public List<PayloadItem> components;
        }

        private class PayloadPed
        {
            public string spawn;
            public string label;
            public string category;
        }

        private class Payload
        {
            public List<PayloadItem> vehicles = new List<PayloadItem>();
            public List<PayloadWeapon> weapons = new List<PayloadWeapon>();
            public List<PayloadPed> peds = new List<PayloadPed>();
            public List<PayloadItem> engineSounds = new List<PayloadItem>();
            public Dictionary<string, Dictionary<int, string>> extras = new Dictionary<string, Dictionary<int, string>>();
            public Locations locations = new Locations();
            public EupState eup = new EupState();
        }

        private class EupState
        {
            public bool allowed = true;
        }
        #endregion

        private static LuaConfig config = new LuaConfig();
        private static bool configLoaded = false;

        /// <summary>
        /// Players who asked for their addons before the Lua config had been handed over. The client
        /// blocks menu construction until it gets a reply, so the request is parked rather than
        /// answered with an empty payload.
        /// </summary>
        private static readonly List<Player> pendingRequests = new List<Player>();

        public AddonsConfig()
        {
            // server/addons.lua also sends this unprompted on resource start, so whichever runtime
            // comes up second still gets the config across.
            TriggerEvent("vMenu:Internal:RequestConfig");
            Tick += ConfigHandoverTimeout;
        }

        /// <summary>
        /// If the Lua side never hands the config over -- a broken server/addons.lua, say -- clients
        /// would wait on their addon payload forever and never build their menus. Give up after a
        /// grace period and let them through with no addons rather than leaving them stuck.
        /// </summary>
        private async Task ConfigHandoverTimeout()
        {
            await Delay(15000);
            Tick -= ConfigHandoverTimeout;

            if (!configLoaded)
            {
                DebugLog.Log("[Addons] No config arrived from server/addons.lua within 15s. Addons, vehicle extras and map locations will be unavailable. Check that config/config_server.lua and config/locations.lua load without errors.", DebugLog.LogLevel.error);
                configLoaded = true;
                FlushPendingRequests();
            }
        }

        #region Config intake
        [EventHandler("vMenu:Internal:LoadConfig")]
        internal void LoadConfig(string json)
        {
            try
            {
                config = JsonConvert.DeserializeObject<LuaConfig>(json) ?? new LuaConfig();

                var section = config.addons ?? new AddonsSection();
                section.vehicles = Sanitize(section.vehicles, "vehicle");
                section.weapons = Sanitize(section.weapons, "weapon");
                section.peds = Sanitize(section.peds, "ped");
                section.engineSounds = Sanitize(section.engineSounds, "engine sound");
                config.addons = section;

                configLoaded = true;
                FlushPendingRequests();

                var addons = config.addons;
                DebugLog.Log($"[Addons] Config loaded: {Count(addons?.vehicles)} vehicles, " +
                    $"{Count(addons?.weapons)} weapons, {Count(addons?.peds)} peds, " +
                    $"{Count(addons?.engineSounds)} engine sounds, {Count(config.locations?.teleports)} teleports, " +
                    $"{Count(config.locations?.blips)} blips.");
            }
            catch (Exception ex)
            {
                DebugLog.Log($"[Addons] Could not parse the Lua config, addons will be unavailable: {ex.Message}", DebugLog.LogLevel.error);
                config = new LuaConfig();
                configLoaded = true;
                FlushPendingRequests();
            }
        }

        /// <summary>Answers anyone who asked while the config was still on its way over.</summary>
        private void FlushPendingRequests()
        {
            if (pendingRequests.Count == 0)
            {
                return;
            }

            var waiting = pendingRequests.ToList();
            pendingRequests.Clear();
            foreach (var player in waiting)
            {
                try
                {
                    SendAddons(player);
                }
                catch (Exception ex)
                {
                    // Most likely they disconnected while waiting; nothing to do but move on.
                    DebugLog.Log($"[Addons] Could not send addons to a waiting player: {ex.Message}", DebugLog.LogLevel.warning);
                }
            }
        }

        private static int Count<T>(List<T> list) => list?.Count ?? 0;
        #endregion

        #region ACE evaluation
        /// <summary>
        /// The ACE an entry is gated by: its own "permission" if set, otherwise
        /// "&lt;namespace&gt;.&lt;spawn&gt;" with the spawn name lower cased, matching the way
        /// permissions.cfg spells base-game entries.
        /// </summary>
        private static string AceFor(string aceNamespace, AddonEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.permission))
            {
                return entry.permission.Trim();
            }
            return $"{aceNamespace}.{entry.spawn.Trim().ToLowerInvariant()}";
        }

        /// <summary>
        /// Drops entries with no spawn name or an over-long one, and weapons that do not look like a
        /// weapon. Runs once when the config is loaded, so a bad entry is reported to the owner once
        /// rather than on every player join.
        /// </summary>
        private static List<T> Sanitize<T>(List<T> entries, string kind) where T : AddonEntry
        {
            var kept = new List<T>();
            if (entries == null)
            {
                return kept;
            }

            var isWeapon = kind == "weapon";
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.spawn) || entry.spawn.Trim().Length > 64)
                {
                    DebugLog.Log($"[Addons] Skipping malformed {kind} entry #{i + 1} (missing or over-long spawn name).", DebugLog.LogLevel.warning);
                    continue;
                }
                if (isWeapon && !entry.spawn.Trim().StartsWith("weapon_", StringComparison.OrdinalIgnoreCase))
                {
                    DebugLog.Log($"[Addons] Skipping weapon '{entry.spawn}': weapon spawn names must start with 'weapon_'.", DebugLog.LogLevel.warning);
                    continue;
                }
                kept.Add(entry);
            }
            return kept;
        }

        private static List<T> Allowed<T>(Player player, string aceNamespace, List<T> entries) where T : AddonEntry
        {
            var allowed = new List<T>();
            if (entries == null)
            {
                return allowed;
            }

            foreach (var entry in entries)
            {
                if (IsPlayerAceAllowed(player.Handle, AceFor(aceNamespace, entry)))
                {
                    allowed.Add(entry);
                }
            }
            return allowed;
        }
        #endregion

        #region Per-player payload
        [EventHandler("vMenu:RequestAddons")]
        internal void RequestAddons([FromSource] Player player)
        {
            if (!configLoaded)
            {
                // The client blocks menu construction on this reply, so park the request and answer
                // it the moment the Lua side hands the config over rather than sending an empty one.
                if (!pendingRequests.Contains(player))
                {
                    pendingRequests.Add(player);
                }
                TriggerEvent("vMenu:Internal:RequestConfig");
                return;
            }

            SendAddons(player);
        }

        private static void SendAddons(Player player)
        {
            var addons = config.addons ?? new AddonsSection();
            var payload = new Payload
            {
                vehicles = Allowed(player, "vMenu.VehicleSpawner", addons.vehicles).Select(Project).ToList(),
                engineSounds = Allowed(player, "vMenu.VehicleOptions", addons.engineSounds).Select(Project).ToList(),
                extras = config.extras ?? new Dictionary<string, Dictionary<int, string>>(),
                locations = GetMergedLocations(),
            };

            payload.weapons = Allowed(player, "vMenu.WeaponOptions", addons.weapons)
                .Select(w => new PayloadWeapon
                {
                    spawn = w.spawn.Trim().ToLowerInvariant(),
                    label = Label(w),
                    // Components inherit the weapon's permission: if you may spawn it, you may fit
                    // its attachments. The client still asks the game whether the weapon takes each.
                    components = w.components?.Where(c => c != null && !string.IsNullOrWhiteSpace(c.spawn))
                                              .Select(Project).ToList()
                                 ?? new List<PayloadItem>(),
                })
                .ToList();

            payload.peds = Allowed(player, "vMenu.PlayerAppearance", addons.peds)
                .Select(p => new PayloadPed
                {
                    spawn = p.spawn.Trim(),
                    label = Label(p),
                    category = string.IsNullOrWhiteSpace(p.category) ? "other" : p.category.Trim().ToLowerInvariant(),
                })
                .ToList();

            var eup = addons.eup;
            payload.eup.allowed = eup == null || !eup.enabled
                || IsPlayerAceAllowed(player.Handle, string.IsNullOrWhiteSpace(eup.permission) ? "vMenu.PlayerAppearance.EUP" : eup.permission);

            player.TriggerEvent("vMenu:SetAddons", JsonConvert.SerializeObject(payload));
        }

        private static PayloadItem Project(AddonEntry entry) => new PayloadItem { spawn = entry.spawn.Trim(), label = Label(entry) };

        /// <summary>Falls back to the spawn name so an entry without a label is still usable.</summary>
        private static string Label(AddonEntry entry)
        {
            var label = string.IsNullOrWhiteSpace(entry.label) ? entry.spawn : entry.label;
            return label.Length > 64 ? label.Substring(0, 64) : label;
        }
        #endregion

        #region Teleport locations (authored in Lua, runtime additions in KVP)
        private static List<TeleportLocation> GetSavedLocations()
        {
            var stored = GetResourceKvpString(LocationsKvpKey);
            if (string.IsNullOrEmpty(stored))
            {
                return new List<TeleportLocation>();
            }
            try
            {
                return JsonConvert.DeserializeObject<List<TeleportLocation>>(stored) ?? new List<TeleportLocation>();
            }
            catch (Exception ex)
            {
                DebugLog.Log($"[Addons] Could not read saved teleport locations from KVP: {ex.Message}", DebugLog.LogLevel.error);
                return new List<TeleportLocation>();
            }
        }

        /// <summary>
        /// The authored locations from config/locations.lua plus anything players have saved in-game.
        /// Authored entries win on a name clash so a saved location can never shadow the config.
        /// </summary>
        private static Locations GetMergedLocations()
        {
            var locations = new Locations
            {
                teleports = new List<TeleportLocation>(),
                blips = new List<LocationBlip>(),
            };

            var authored = config.locations;
            if (authored?.teleports != null)
            {
                foreach (var t in authored.teleports)
                {
                    locations.teleports.Add(new TeleportLocation(t.name, new Vector3(t.x, t.y, t.z), t.heading));
                }
            }
            if (authored?.blips != null)
            {
                foreach (var b in authored.blips)
                {
                    locations.blips.Add(new LocationBlip(b.name, new Vector3(b.x, b.y, b.z), b.sprite, b.color));
                }
            }

            foreach (var saved in GetSavedLocations())
            {
                if (!locations.teleports.Any(t => t.name == saved.name))
                {
                    locations.teleports.Add(saved);
                }
            }

            return locations;
        }

        [EventHandler("vMenu:SaveTeleportLocation")]
        internal void AddTeleportLocation([FromSource] Player source, string locationJson)
        {
            if (!IsAllowed(Permission.MSTeleportSaveLocation, source) && !IsAllowed(Permission.MSAll, source))
            {
                BanManager.BanCheater(source);
                return;
            }

            TeleportLocation location;
            try
            {
                location = JsonConvert.DeserializeObject<TeleportLocation>(locationJson);
            }
            catch
            {
                DebugLog.Log("Teleport location could not be deserialized, location was not saved.", DebugLog.LogLevel.error);
                return;
            }

            // float.IsFinite is unavailable on .NET Framework, so check NaN/Infinity explicitly.
            bool NotFinite(float f) => float.IsNaN(f) || float.IsInfinity(f);
            if (string.IsNullOrEmpty(location.name) || location.name.Length > 64
                || NotFinite(location.coordinates.X) || NotFinite(location.coordinates.Y)
                || NotFinite(location.coordinates.Z) || NotFinite(location.heading))
            {
                DebugLog.Log("Teleport location failed validation (name length or non-finite coordinates), location was not saved.", DebugLog.LogLevel.error);
                return;
            }

            var merged = GetMergedLocations();
            if (merged.teleports.Any(loc => loc.name == location.name))
            {
                DebugLog.Log("A teleport location with this name already exists, location was not saved.", DebugLog.LogLevel.error);
                return;
            }

            var saved = GetSavedLocations();
            saved.Add(location);
            SetResourceKvp(LocationsKvpKey, JsonConvert.SerializeObject(saved));

            merged.teleports.Add(location);
            TriggerClientEvent("vMenu:UpdateTeleportLocations", JsonConvert.SerializeObject(merged.teleports));
        }
        #endregion
    }
}
