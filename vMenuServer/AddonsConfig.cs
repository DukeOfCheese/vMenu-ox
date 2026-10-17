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

        /// <summary>A custom tattoo from a streamed overlay pack. Not ACE gated.</summary>
        public class TattooEntry
        {
            public string collection { get; set; }
            public string name { get; set; }
            /// <summary>male, female or both (the default).</summary>
            public string gender { get; set; }
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
            public List<TattooEntry> tattoos { get; set; }
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

        /// <summary>Base-game models that need their own ACE (Config.ModelWhitelist in config_server.lua).</summary>
        private class ModelWhitelistSection
        {
            public List<string> vehicles { get; set; }
            public List<string> peds { get; set; }
            public List<string> weapons { get; set; }
        }

        private class LuaConfig
        {
            public ModelWhitelistSection modelWhitelist { get; set; }
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

        /// <summary>Matches the client's Tattoo struct: gender is 0 male, 1 female, 2 both.</summary>
        private class PayloadTattoo
        {
            public string collectionName;
            public string name;
            public int gender;
        }

        private class Payload
        {
            public List<PayloadItem> vehicles = new List<PayloadItem>();
            public List<PayloadWeapon> weapons = new List<PayloadWeapon>();
            public List<PayloadPed> peds = new List<PayloadPed>();
            public List<PayloadItem> engineSounds = new List<PayloadItem>();
            public List<PayloadTattoo> tattoos = new List<PayloadTattoo>();
            public Dictionary<string, Dictionary<int, string>> extras = new Dictionary<string, Dictionary<int, string>>();
            public Locations locations = new Locations();
            public EupState eup = new EupState();
            public RestrictedModels restricted = new RestrictedModels();
        }

        private class EupState
        {
            public bool allowed = true;
        }

        /// <summary>Whitelisted models this player may NOT use. Only names, never the ACEs.</summary>
        private class RestrictedModels
        {
            public List<string> vehicles = new List<string>();
            public List<string> peds = new List<string>();
            public List<string> weapons = new List<string>();
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
            // The Lua side hands the config over through this export. It must not be an event handler:
            // on this runtime C# event handlers also accept events sent by clients, which would let any
            // player replace the addon config (and the ACE each addon is gated behind).
            Exports.Add("LoadConfig", new Action<object>(json => LoadConfig(json as string)));

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
        private const int MaxConfigLength = 4 * 1024 * 1024;

        internal void LoadConfig(string json)
        {
            if (json == null || json.Length > MaxConfigLength)
            {
                DebugLog.Log($"[Addons] Ignoring a Lua config hand-over that was empty or larger than {MaxConfigLength} characters.", DebugLog.LogLevel.error);
                return;
            }

            try
            {
                config = JsonConvert.DeserializeObject<LuaConfig>(json) ?? new LuaConfig();

                var section = config.addons ?? new AddonsSection();
                section.vehicles = Sanitize(section.vehicles, "vehicle");
                section.weapons = Sanitize(section.weapons, "weapon");
                section.peds = Sanitize(section.peds, "ped");
                section.engineSounds = Sanitize(section.engineSounds, "engine sound");
                section.tattoos = SanitizeTattoos(section.tattoos);
                config.addons = section;

                var whitelist = config.modelWhitelist ?? new ModelWhitelistSection();
                whitelist.vehicles = SanitizeModelNames(whitelist.vehicles);
                whitelist.peds = SanitizeModelNames(whitelist.peds);
                whitelist.weapons = SanitizeModelNames(whitelist.weapons);
                config.modelWhitelist = whitelist;

                configLoaded = true;
                FlushPendingRequests();

                var addons = config.addons;
                DebugLog.Log($"[Addons] Config loaded: {Count(addons?.vehicles)} vehicles, " +
                    $"{Count(addons?.weapons)} weapons, {Count(addons?.peds)} peds, " +
                    $"{Count(addons?.engineSounds)} engine sounds, {Count(addons?.tattoos)} tattoos, " +
                    $"{Count(config.locations?.teleports)} teleports, " +
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

        /// <summary>
        /// Lower-cased, trimmed, de-duplicated model names; anything empty or over 64 characters is dropped.
        /// </summary>
        private static List<string> SanitizeModelNames(List<string> names)
        {
            return (names ?? new List<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length <= 64)
                .Select(n => n.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// The whitelisted models this player may not use. Allowed by "&lt;namespace&gt;.&lt;model&gt;" (weapons without
        /// their weapon_ prefix, as upstream named them), "&lt;namespace&gt;.All" or vMenu.Everything.
        /// </summary>
        private static List<string> Restricted(Player player, string aceNamespace, List<string> models, string stripPrefix)
        {
            if (models == null || models.Count == 0
                || IsPlayerAceAllowed(player.Handle, aceNamespace + ".All")
                || IsPlayerAceAllowed(player.Handle, "vMenu.Everything"))
            {
                return new List<string>();
            }
            return models.Where(model =>
            {
                var name = stripPrefix != null && model.StartsWith(stripPrefix) ? model.Substring(stripPrefix.Length) : model;
                return !IsPlayerAceAllowed(player.Handle, $"{aceNamespace}.{name}");
            }).ToList();
        }

        /// <summary>
        /// Drops tattoos with a missing or over-long collection/name and duplicate entries, and
        /// normalises gender to male/female/both. Runs once at config load, like Sanitize.
        /// </summary>
        private static List<TattooEntry> SanitizeTattoos(List<TattooEntry> entries)
        {
            var kept = new List<TattooEntry>();
            if (entries == null)
            {
                return kept;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.collection) || string.IsNullOrWhiteSpace(entry.name)
                    || entry.collection.Trim().Length > 64 || entry.name.Trim().Length > 64)
                {
                    DebugLog.Log($"[Addons] Skipping malformed tattoo entry #{i + 1} (missing or over-long collection or name).", DebugLog.LogLevel.warning);
                    continue;
                }

                entry.collection = entry.collection.Trim();
                entry.name = entry.name.Trim();
                if (!seen.Add(entry.collection + "/" + entry.name))
                {
                    DebugLog.Log($"[Addons] Skipping duplicate tattoo entry #{i + 1} ({entry.collection} / {entry.name}). Please remove duplicate lines!", DebugLog.LogLevel.warning);
                    continue;
                }

                var gender = string.IsNullOrWhiteSpace(entry.gender) ? "both" : entry.gender.Trim().ToLowerInvariant();
                if (gender != "male" && gender != "female" && gender != "both")
                {
                    DebugLog.Log($"[Addons] Tattoo entry #{i + 1} ({entry.name}) has unknown gender '{entry.gender}', treating it as both.", DebugLog.LogLevel.warning);
                    gender = "both";
                }
                entry.gender = gender;
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
                // Not ACE gated, matching upstream vMenu: tattoos are cosmetic, and anyone who can
                // open MP Ped Customization can already wear every base-game one.
                tattoos = (addons.tattoos ?? new List<TattooEntry>())
                    .Select(t => new PayloadTattoo
                    {
                        collectionName = t.collection,
                        name = t.name,
                        gender = t.gender == "male" ? 0 : t.gender == "female" ? 1 : 2,
                    })
                    .ToList(),
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

            var whitelist = config.modelWhitelist ?? new ModelWhitelistSection();
            payload.restricted = new RestrictedModels
            {
                vehicles = Restricted(player, "vMenu.VehicleSpawner.WhitelistedModels", whitelist.vehicles, null),
                peds = Restricted(player, "vMenu.PlayerAppearance.WhitelistedModels", whitelist.peds, null),
                weapons = Restricted(player, "vMenu.WeaponOptions.WhitelistedModels", whitelist.weapons, "weapon_"),
            };

            player.TriggerEvent("vMenu:SetAddons", JsonConvert.SerializeObject(payload));
        }

        /// <summary>
        /// Whether this player may put the given engine sound on a vehicle (the same ACE check as the menu payload).
        /// </summary>
        internal static bool IsEngineSoundAllowed(Player player, string engineSound)
        {
            return Allowed(player, "vMenu.VehicleOptions", config.addons?.engineSounds)
                .Any(e => string.Equals(e.spawn.Trim(), engineSound, StringComparison.OrdinalIgnoreCase));
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
        private const int MaxSavedLocations = 200;

        // Read from KVP once and kept in memory; every addon request merges these in.
        private static List<TeleportLocation> savedLocationsCache = null;

        private static List<TeleportLocation> GetSavedLocations()
        {
            if (savedLocationsCache != null)
            {
                return new List<TeleportLocation>(savedLocationsCache);
            }
            var stored = GetResourceKvpString(LocationsKvpKey);
            if (string.IsNullOrEmpty(stored))
            {
                savedLocationsCache = new List<TeleportLocation>();
                return new List<TeleportLocation>();
            }
            try
            {
                savedLocationsCache = JsonConvert.DeserializeObject<List<TeleportLocation>>(stored) ?? new List<TeleportLocation>();
                return new List<TeleportLocation>(savedLocationsCache);
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

            var names = new HashSet<string>(locations.teleports.Select(t => t.name));
            foreach (var saved in GetSavedLocations())
            {
                if (names.Add(saved.name))
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
            if (saved.Count >= MaxSavedLocations)
            {
                source.TriggerEvent("vMenu:Notify", $"~r~This server already has {MaxSavedLocations} saved teleport locations, remove some before adding more.");
                return;
            }
            saved.Add(location);
            SetResourceKvp(LocationsKvpKey, JsonConvert.SerializeObject(saved));
            savedLocationsCache = saved;

            merged.teleports.Add(location);
            TriggerClientEvent("vMenu:UpdateTeleportLocations", JsonConvert.SerializeObject(merged.teleports));
        }
        #endregion
    }
}
