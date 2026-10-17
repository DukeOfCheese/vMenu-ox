# vMenu-ox review, October 2026

A full review of the fork on branch `chore/upstream-legacy-sync`, with the fixes applied and the fork brought up to date with TomGrobbe/vMenu **legacy** (`upstream/legacy`, v3.9.2). Fixes from vMenu **Enhanced** (`upstream/develop`) were also ported where the code they fix exists in the legacy codebase.

Everything below is verified by an MSBuild Release build after every batch and, for the Lua, a Lua 5.4 compile check. The share-code server logic was also run under Lua 5.4 with stubs. **Nothing has been run on a FiveM server yet.** See [In-game checklist](#in-game-checklist).

## Upstream sync

| Source | State before | Done |
|---|---|---|
| upstream legacy | Last hand-sync was upstream `1a43b363` (2025-11-09). 118 later commits were missing, and the fork's history is unrelated to upstream, so nothing can be merged. | Every applicable commit was ported by hand: DLC data (mp2025_02, mp2026_01), MenuAPI 4.0.1 NUI menus and themes, noclip, player list from the server, ItemData pattern matching, Ctrl+Enter ID entry, collections, radio override, door/paint/mod-name fixes, loadout permissions, smooth time, server parent permissions, spawn bypass, `vMenu:DV`, the model whitelist (as a Lua `Config` table), paint/RGB extras, tattoos and blendable faces (as Lua `Config`). |
| Enhanced (`upstream/develop`) | A rewrite (mono v2, new layout). Its own `src/Legacy` build targets the same runtime, but the code is different. | 10 fixes re-implemented by hand: online-player action rate limit, kicking passengers out, MK2/full-ammo loadouts, network-owner scan, double repair, blip rotation, visor and headlight button, no pointing while armed, filtered-menu scroll position, summon feedback. |
| Not ported | — | Upstream's JSON template generator, docs/CI-only commits, and Enhanced-only features (state-bag world sync, webhooks, plugin API, new menus). |

## Security issues

Origin: **Fork** = introduced by the fork; **Inh** = inherited from upstream.

| # | Sev | Where | Issue | Fix | Origin |
|---|---|---|---|---|---|
| S1 | **CRIT** | `BanManager.cs`, `server/integrations.lua` | Any client could call the `vMenu:Internal:*` ban, unban and ban-list events with an admin's id. On mono v1, C# event handlers also accept events sent by clients. | The Lua relays now call C# **exports**, which clients cannot reach, passing the real network `source`. | Fork |
| S2 | **CRIT** | `AddonsConfig.cs` | Any client could send `vMenu:Internal:LoadConfig` to replace the addon config, including the ACE each addon needs. | The config comes in through an export, with a 4 MB cap. | Fork |
| S3 | HIGH | `WeaponLoadouts.cs`, `CommonFunctions.cs` | Saved loadouts skipped weapon permissions and trusted the saved permission number. | Each weapon's permission is looked up by hash in the current weapon list, so it also works for addon weapons (upstream `9e65f039`/`a6349406`). | Inh |
| S4 | HIGH | `client/loadoutCodes.lua` | Shared loadout codes gave weapons straight from Lua with no permission checks and stored the attacker's permission field. | The C# side rebuilds the loadout from the player's own weapon list and spawns it through the normal checks. | Fork |
| S5 | HIGH | `BanManager.cs` | Permanent bans needed only the TempBan permission. A duration of 0 or less, or NaN, made a temp ban permanent. Reasons had no length cap. | Each path checks its own ACE. Bad durations are rejected. Reasons are capped at 256 characters with newlines stripped. | Inh |
| S6 | HIGH | `client/vehicleCodes.lua`, `StorageManager` | One crafted vehicle code broke the Saved Vehicles menu permanently. | C# normalises the code before saving it, and each saved vehicle is read inside its own try/catch. | Fork |
| S7 | MED | `server/*Codes.lua` | Share-code callbacks stayed on with the setting off. The cooldown was claimed after the work and reset on reconnect. No quota, no shape check. | One shared pipeline (`server/lib/codes.lua`): setting gate, cooldown claimed first and tied to the Discord id, 25 codes per day, shape checks. | Fork |
| S8 | MED | `client/outfitCodes.lua` | Shared outfits skipped the EUP clothing cap. Props weren't checked. | Outfits are checked against the current ped and the EUP cap in C#. | Fork |
| S9 | MED | `PermissionsManager.cs` | Server permission checks ignored parent permissions, `vmenu_use_permissions=false` and `vmenu_menu_staff_only`, so players got false cheater bans. | The server resolves permissions the same way the client does. | Inh |
| S10 | MED | `MainServer.cs` | Weather and time changes needed only the menu permission. | A permission is checked for each field actually changed. | Inh |
| S11 | MED | `MainServer.cs` | The clouds check banned moderators who had `WeatherOptions.All`. | Removed the raw ACE check. | Inh |
| S12 | MED | `MainServer.cs` | Kicking passengers out and changing the engine sound worked on any vehicle id. Engine sounds weren't checked against the allowed list. | The caller must have spawned the vehicle or be in it, and the sound must be one this player is allowed to use. | Inh/Fork |
| S13 | MED | `BanManager.cs` | The ban list sent IP addresses to moderators. | `ip:` entries are stripped unless the player has the Unban permission. | Inh |
| S14 | MED | `MainServer.cs` | Any weather string was replicated to every client. | Only known weather types are accepted. | Inh |
| S15 | LOW | `MainServer.cs` | No rate limits. Kick and drop reasons could forge log lines. Teleport saves had no cap. | Rate limits added. Reasons are sanitised. Teleport saves capped at 200. | Inh |
| S16 | LOW | `server/integrations.lua` | `playerConnecting` was registered as a net event, and deferrals were misused. | Changed to `AddEventHandler`, with defer, then the C# ban check, then done. | Fork |
| S17 | LOW | `MainServer.cs` | Console bans all shared `Guid.Empty`, so only the first was ever stored. | Each ban gets `Guid.NewGuid()`. | Inh |
| S18 | LOW | `client/*Codes.lua` | The code exports skipped interaction hooks and cooldowns. | They go through the same guarded path. | Fork |
| S19 | LOW | `MainServer.cs` | Clients could fake join and drop notices. | Duplicate joins are ignored. Drop reasons are sanitised. | Inh |
| S20 | LOW | share codes | Code ids are sequential, so codes can be enumerated. | Documented. Public by id is by design. | Fork |
| S21 | LOW | `PermissionsManager.cs` | A hard-coded developer identifier could be given `Everything`. | Removed. | Inh |
| S22 | MED | `CommonFunctions.SpawnCustomWeapon` | Spawning a weapon by name accepted any name with the basic Spawn permission, which bypassed addon-weapon ACEs. | Only weapons in the player's own weapon list are accepted. | Inh |
| — | — | Outfit codes | `MpPedCustomization` existed twice (once from the runtime, once from MainMenu), so its exports read a character that was never loaded. | The exports forward to MainMenu's instance. | Fork |

Checked and found clean: SQL (all queries parameterised), `load`/`loadstring` (not used), Newtonsoft `TypeNameHandling` (not used), `versionCheck.lua` (a plain GitHub GET).

## Performance improvements

| # | Where | Cost before | Change |
|---|---|---|---|
| P1 | `PermissionsManager.IsAllowedClient` | A convar read on every call, about 25–30 calls a frame | Cached when permissions arrive; stale results cleared |
| P2 | VehicleOptions tick | About 15 property writes and an allocation every frame | Applied on change |
| P3 | PlayerOptions tick | Invincible/ragdoll/convar set every frame | Applied on change |
| P4 | DeathNotifications | Every player scanned every frame, O(n²) | Throttled, dictionary + HashSet |
| P5 | WeaponOptions tick | `Weapons.Current` read 4× and the hash computed 2× per frame | Cached |
| P6 | GcTick | Forced `GC.Collect()` every 60 s (hitch) | Removed |
| P7 | TimeSync | 5 ms loop with 4 convar reads while time is frozen | `PauseClock` and a 1 s interval |
| P8 | Noclip | 7 scaleform slots rebuilt every frame | Rebuilt only on change (upstream `bc452e42`) |
| P9 | MP ped camera | `Task.ClearAll()` every frame | Only on transition |
| P10 | Snow / clothing animations | Convar every frame; every player every 50 ms | Cached / skip unchanged |
| P11 | `server/integrations.lua` overhead names | Full JSON each second, full broadcast every 10 s | Event-driven deltas |
| P12 | Join, drop and PM fan-out | 2 ACE checks per player each time | Flags cached per player at join |
| P13 | `SetPermissionsForPlayer` | About 300 ACE calls per join | Children skipped when the parent is allowed |
| P14 | Saved teleport locations | KVP re-read on every addon request | Kept in memory |
| P15 | Kick/ban log | Whole log file re-read and rewritten per action | Appended |
| P16 | Share codes | Identifier lookups repeated; JSON round-tripped | Single lookup; raw KVP string sent |
| P17 | `database.sql` | Unused index, no migration | Quota query uses the index; migration documented |
| P18 | Client hashing | `(uint)GetHashKey` native call per hash | Managed `Game.GenerateHashASCII` |
| P19 | Engine-always-on loop | 100 ms stall | Restructured |
| P20 | `MenuSearch.Enabled` | Convar read on every access | Cached |
| — | Player list | Rebuilt on every request | Server caches the reply for 1 s |

## New settings and permissions

| Setting / ACE | Default | What it does |
|---|---|---|
| `vmenu_smooth_time_transitions` | `false` | Runs the clock forward to a new time instead of jumping. |
| `vmenu_theme` | `"default"` | `"custom"` loads the dark theme in `themes/custom.css` with the header from `menuapi-banners/custom.png`. |
| `vmenu_delete_vehicle_distance` | `5.0` | Range of the `vMenu:DV` command. |
| `vmenu_online_players_action_limit` / `_seconds` | `8` / `10` | Actions on other players allowed per window. `0` turns the limit off. |
| `vMenu.VehicleSpawner.BypassRateLimit` | — | Skips the spawn cooldown. `VehicleSpawner.All` does not grant it. |
| `vMenu.<Menu>.WhitelistedModels.<model>` / `.All` | — | Access to models listed in `Config.ModelWhitelist`. |
| `Config.ModelWhitelist` (config_server.lua) | empty | Base-game vehicles, peds and weapons that need their own ACE. |
| `Config.Addons.tattoos` | empty | Server-defined tattoos, e.g. `{ collection = 'mytattoos_overlays', name = 'MY_Tattoo_000_M', gender = 'male' }`. Shown under Addon Tattoos. Not ACE gated. |
| `Config.Addons.extraBlendableFaces` | empty | Labels for addon heads after the 46 base-game faces. Order matters, so never remove an entry from the middle. Not ACE gated. |
| `vMenu:RequestOverheadNames` (client → server event) | — | A client asks once for the overhead-name table, then gets deltas. Limited to once per 5 s, and it returns only public names. |

## Behaviour changes to know about

- **Ban check at connect fails open.** If the C# ban check errors, the player is let in and the error is logged.
- **Loading an outfit code** needs the target MP character to be spawned. The outfit is saved as a copy of that character.
- **Saved loadouts:**
  - Loadouts load with full ammo.
  - Restoring weapons after death keeps the saved count.
  - Weapons the player isn't allowed are skipped, with a notice.
- **Spawn-by-name for weapons** only accepts weapons in the player's own weapon list.
- **`clearlayouts`** now asks for confirmation. `clearloadouts` was added as the correctly spelled name.
- **The data export/import feature** (experimental and never finished) was removed with `storage.html`.
- **`Exports["vMenu"]` is left hard-coded in C#.** The server already refuses to run unless the resource folder is named `vMenu`.
- **MP ped inheritance:** Father/Mother are now Parent #1/#2 with separate skin pickers. Each face label sits at its real head-blend index, so labels upstream had in the wrong place now show correctly, and existing saves look the same.
- **Paint finish:**
  - The list is Classic, Metallic, Pearlescent, Matte, Metals, Chrome, then Chameleon (shown only when chameleon colours are on), matching the game's paint types. Upstream has Classic and Metallic swapped.
  - Changing the finish keeps the current colour.
- **Death notices** can arrive up to 250 ms later than before. Outlines and noclip can take up to 100 ms or 50 ms to start.
- **The `vMenu:SyncOverheadNames` client event** takes an optional second argument, `isDelta`. Called with one argument it still replaces the whole table.

## In-game checklist

Run these on a test server before release. Items 1 to 3 check assumptions that the build and the Lua checks cannot cover.

1. **Lua can call this resource's own C# exports.** Ban, temp-ban, unban and the ban list should work from the menu, and addons should load. If not, the console shows `No such export … in resource vMenu`, or addons are unavailable after 15 s.
2. **Untyped arguments reach the C# exports.** Bans should go through. If not, the console shows an `ArgumentException` on every ban.
3. **Connect-time ban check.** A banned player should be refused at connect with the ban message.
4. **Old internal events are dead.** Triggering `vMenu:Internal:PermBanPlayer` or `vMenu:Internal:LoadConfig` from a client should do nothing.
5. **Time.** Setting the time while it is frozen should keep it frozen.
6. **Share codes:**
   - With the code settings off, there are no callbacks.
   - With them on, codes generate and load.
   - A loadout code with a restricted weapon gives nothing to a player without that permission.
   - A malformed vehicle code is rejected.
7. **MenuAPI 4.** Check that:
   - Every menu opens.
   - The commonmenu banner shows.
   - `vmenu_theme custom` applies.
   - These hotkeys work: search filter (Space), ban-list filter, unavailable-vehicle delete, door toggle, MP ped Ctrl+Enter.
   - ox_lib dialogs take focus over the menu.
8. **Whitelist.** A listed model is locked without the ACE and usable with it.
9. **Tattoos and blendable faces.** Custom tattoos, hair tattoos and extra faces load, and existing saved characters look unchanged.
10. **Paint.** Check that:
    - Headlight and tyre smoke colours and the paint finish persist on saved vehicles.
    - Classic vs Metallic shows correctly.
    - Mod Menu → Headlights / Tire Smoke still open after changing the wheel type, which rebuilds the mod menu.
11. **Performance.** `resmon` client ms for vMenu should be lower than before with god mode and vehicle options on.
12. **God mode survives noclip.** Player and vehicle god mode should still hold after toggling noclip; those flags are now only re-applied on change and on short timers.
13. **Frozen time.** Frozen time should stay put. The clock is now paused and re-applied once a second instead of every 5 ms.
14. **Ctrl+Enter** opens the ID prompt in the MP ped and Player Appearance clothing lists.
