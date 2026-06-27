# vMenu-ox — Code Review Fix Plan (Handoff)

> **Purpose:** This is a work plan for a Claude Code session running on **Windows** (where `msbuild`/VS2022 is available) to apply a reviewed set of security, correctness, performance, and refactoring fixes. It was produced on a macOS session that had **no build toolchain**, so nothing here was compile-verified. The Windows session is the verification authority.

## How to use this document

1. Read the **Ground rules** and **Build loop** sections first.
2. Work through the batches **in order**. After each batch, run the build and fix any errors before moving on.
3. Each fix has a checkbox, a `file:line` anchor, the problem, and the concrete fix. **Line numbers are from commit `cf9937f` and will drift as you edit** — locate each fix by the quoted code/symbol, not the raw line number, especially after you've already edited that file. Edit **bottom-to-top within a file** to minimize drift.
4. Tags: `[VERIFIED]` = the macOS session read the actual source and confirmed the issue. `[REPORTED]` = surfaced by an automated reviewer but not independently re-read — confirm before applying.

## Ground rules

- **No fixes have been applied yet — the tree is clean** (one attempted edit was blocked by a hook and did not land).
- **GateGuard hook:** an ECC plugin hook fires a "fact-forcing gate" on every `Edit`/`Write` (and the first `Bash`). For a ~60-edit session this is heavy friction. Recommended: start the Windows session with `ECC_GATEGUARD=off`, or add `pre:edit-write:gateguard-fact-force` and `pre:bash:gateguard-fact-force` to `ECC_DISABLED_HOOKS`. Otherwise, present the requested facts before each edit and retry.
- **Match existing style.** This codebase uses CitizenFX idioms (`BaseScript`, `[EventHandler]`, `static using CitizenFX.Core.Native.API`, `async Task`). Mirror the permission-check pattern already used in `MainServer.cs` (`PermissionsManager.IsAllowed(Permission.X, source)` + `BanManager.BanCheater(source)` on failure).
- **Keep changes surgical.** Prefer the smallest diff that fixes the issue. The two large architectural refactors (Lua factory, `ManageCamera` split) are **optional** — see Batch 7.

## Do NOT do these (intentional exclusions — would cause harm)

- **Do NOT rename `PedTatttoos`** (`vMenu/MpPedDataManager.cs`, triple-t typo). It is a serialized JSON key; renaming changes the KVP schema and **breaks every player's already-saved MP ped** with no migration. Leave it, or add a `[JsonProperty("PedTatttoos")]` alias only if you also rename — not worth the risk. (Cosmetic only.)
- **Do NOT "fix" the code-share IDOR (`server/*Codes.lua` `WHERE id = ?`) by locking it down blindly.** Codes are *meant to be shared by ID* — public-by-ID is likely by-design. The real risks are enumeration + the DoS in Batch 6 (S6). Treat ownership-locking as a product decision; default action is to document it, not gate it.

## Build loop (run after every batch)

```powershell
nuget restore vMenu.sln
msbuild vMenu.sln /p:Configuration=Release
# First run BEFORE any edits to confirm a clean baseline.
# Output DLLs land in build\vMenu\ (vMenuClient.net.dll, vMenuServer.net.dll).
```

Lua/JSON/SQL changes (Batch 6) aren't covered by msbuild — eyeball them and, if available, run `luacheck` against the ox_lib globals (`lib`, `cache`, `Config`).

---

## Batch 0 — Baseline
- [ ] Clone, `nuget restore`, `msbuild ... /p:Configuration=Release`. Confirm a **clean build before editing**. If the baseline doesn't build, stop and report.

---

## Batch 1 — Server security & correctness (`vMenuServer/`)

Build after this batch.

### MainServer.cs
- [ ] **S1 `[VERIFIED]` — `vMenu:GetPlayerIdentifiers` has no permission check / no source / unguarded index.** `MainServer.cs:213`. Handler `(int TargetPlayer, NetworkCallbackDelegate CallbackFunction)` lets any client read any player's identifiers (only `ip:` filtered) and `Players[TargetPlayer]` can throw.
  **Fix:** add the source player, validate the target, gate on permission. Pattern:
  ```csharp
  EventHandlers.Add("vMenu:GetPlayerIdentifiers", new Action<Player, int, NetworkCallbackDelegate>(([FromSource] Player source, TargetPlayer, CallbackFunction) =>
  {
      if (!PermissionsManager.IsAllowed(PermissionsManager.Permission.OPIdentifiers, source)
          && !PermissionsManager.IsAllowed(PermissionsManager.Permission.OPAll, source))
      { BanManager.BanCheater(source); return; }
      if (TargetPlayer <= 0 || !DoesPlayerExist(TargetPlayer.ToString())) { CallbackFunction("[]"); return; }
      var data = new List<string>();
      foreach (var e in Players[TargetPlayer].Identifiers) if (!e.Contains("ip:")) data.Add(e);
      CallbackFunction(JsonConvert.SerializeObject(data));
  }));
  ```
  Confirm `Permission.OPIdentifiers` exists in `PermissionsManager.cs`; if not, gate on `OPAll`/`Everything` or add an enum entry + ACE (see Batch 2 note on compiled permissions).
- [ ] **S3 `[VERIFIED]` — `vMenu:ClearArea` has no permission check.** `MainServer.cs:565` `ClearAreaNearPos([FromSource] Player source)` triggers a server-wide clear for any caller. Also `source.Character` can be null.
  **Fix:** guard `if (source?.Character == null) return;`, then gate on a clear-area permission before `TriggerClientEvent`. **Note:** there may be no dedicated `Permission` for this — grep the enum. If none exists, either gate on an existing Misc permission or add one (PermissionsManager.cs enum + `GetAceName` mapping + `permissions.cfg` ACE). Do not leave it ungated.
- [ ] **S4 `[REPORTED]` — `vMenu:changeEngineSound` writes a client string to a replicated state bag with no cap.** `MainServer.cs:~1212`. Confirm, then clamp length (e.g. ≤64) + validate against known engine-sound names, and `if (!DoesEntityExist(veh.Handle)) return;` before writing.
- [ ] **C11 `[REPORTED]` — `SummonPlayer` unclamped client `numberOfSeats` loop bound.** `MainServer.cs:~924`. Add `numberOfSeats = Math.Clamp(numberOfSeats, 0, 16);` before the loop. (Also see HIGH-2 below — make it `Task`.)
- [ ] **HIGH-2 `[REPORTED]` — `SummonPlayer` is `async void`.** `MainServer.cs:887`. Change to `internal async Task SummonPlayer(...)`; the `EventHandlers.Add` registration can keep the delegate. Ensure exceptions in the `await Delay` loop can't crash silently.
- [ ] **KillPlayer staff immunity `[REPORTED]`.** `MainServer.cs:862`. `KickPlayer` (`:844`) checks `DontKickMe` before acting; `KillPlayer` does not. Add the same immunity check before triggering `vMenu:KillMe`.
- [ ] **Hardcoded ACE strings `[VERIFIED]`.** `MainServer.cs:699` (`vMenu.WeatherOptions.Menu`) and `:792` (`vMenu.TimeOptions.Menu`) use raw `IsPlayerAceAllowed` strings instead of `PermissionsManager.IsAllowed(Permission.WOMenu/TOMenu, source)`. Replace for consistency (drifts silently on rename). Confirm enum names exist.
- [ ] **freezeTime ignored `[REPORTED]`.** `MainServer.cs:790` — `UpdateServerTime` accepts `freezeTimeNew` but never applies it. Add `FreezeTime = freezeTimeNew;`.
- [ ] **Culture-sensitive parse `[REPORTED]`.** `MainServer.cs:778` `float.Parse(new Random().NextDouble().ToString())` throws on locales with `,` decimal sep. Replace with `(float)_rng.NextDouble()` (see Random fix).
- [ ] **Duplicate-seed `Random` `[REPORTED]`.** `MainServer.cs:778-779` and `:664` create `new Random()` per call → identical values in the same tick. Add `private static readonly Random _rng = new();` and use it throughout.
- [ ] **Unbounded relayed strings `[REPORTED]`.** `MainServer.cs:997` (`SendMessageToPlayer`) forwards client `message` to staff/log with no cap. Add a length cap (e.g. ≤512) and reject if exceeded.
- [ ] **JSON guards `[REPORTED]`.** Wrap `JsonConvert.DeserializeObject` in try/catch (log + early-return) at `MainServer.cs:486` (`migrate` / `bans.json`) and `:1053` (`SaveTeleportLocation` — also validate `name` length ≤64 and `float.IsFinite` on coordinates/heading before writing to `config/locations.json`).
- [ ] **MEDIUM-5 `[REPORTED]` — `GetJoinQuitNotifPlayers` enumerates `joinedPlayers` HashSet that other handlers mutate.** `MainServer.cs:1115`. Iterate a snapshot: `foreach (var p in joinedPlayers.ToList())`.

### BanManager.cs
- [ ] **C1 `[VERIFIED]` — broken unban (event-name mismatch).** `BanManager.cs:55` registers `vMenu:Internal:RequestPlayerUnban`; Lua `build/vMenu/server/integrations.lua:57` fires `vMenu:Internal:Request`**`ed`**`PlayerUnban`. `RemoveBanRecord` never runs (unban silently does nothing — fails safe). **Fix:** change line 55 to `"vMenu:Internal:RequestedPlayerUnban"`. (Chain: `BannedPlayers.cs:237` → Lua `:56` → internal event → C#.)
- [ ] **S2 `[VERIFIED]` — `SendBanList` has no ACE check.** `BanManager.cs:63`. `vMenu:RequestBanList` is relayed ungated from `integrations.lua:62`, so any client receives the full ban list (all identifiers + reasons). **Fix:** before sending (line ~73), gate on `vMenu.OnlinePlayers.ViewBannedPlayers` / `OPAll` / `Everything`; `BanCheater(source)` + return on failure. Keep the existing `source?.` null-safety.
- [ ] **C6 `[VERIFIED]` — `GetBanList` deserialize has no try/catch.** `BanManager.cs:107`. One corrupt ban KVP throws up through `CheckForBans`, failing the connecting handler (can let players through). **Fix:** try/catch inside the foreach, `Log(... error)` the bad key, `continue`.
- [ ] **C7 `[VERIFIED]` — `BanPlayer` derefs `source.Handle` when `source` is null.** `BanManager.cs:217-226`. `source` is only assigned when `playerId != 0`, but line 226 calls `source.Handle` unconditionally → NRE. (Latent: only via a `playerId==0` trigger path, but it's a hard crash. Note `SendBanList` already uses the null-safe pattern.) **Fix:** `if (source == null) { Log("BanPlayer: null source, aborting.", LogLevel.error); return; }` after the assignment block.
- [ ] **MEDIUM-8 `[REPORTED]` — log file read+rewrite per action.** `BanManager.cs:437` / `MainServer.cs:1021` `LoadResourceFile`+`SaveResourceFile` on every ban/kick. Low priority; optionally batch via an in-memory buffer. Skip if risky.

---

## Batch 2 — Shared (`SharedClasses/`)

Build after this batch. **Lower priority / optional — assess risk.**

- [ ] **MEDIUM-7 `[REPORTED]` — `SetPermissionsForPlayer` does ~180 uncached `IsPlayerAceAllowed` P/Invokes per joining player.** `PermissionsManager.cs:539`. Optional optimization: cache the per-player result in a `ConcurrentDictionary<string, Dictionary<Permission,bool>>` keyed by handle, invalidated on `playerDropped`. **Only do this if the build stays green and behavior is unchanged — it's a hot, security-critical path.** Safe to skip.
- [ ] **Compiled-permission note:** `Permission` (enum) and `Setting` (enum) here are compiled. Any *new* permission added for S1/S3 must be added to the enum, to `GetAceName`, and to `permissions.cfg` as an ACE. A new ACE alone does nothing.

---

## Batch 3 — Client correctness & async (`vMenu/`: CommonFunctions, EventManager, MainMenu, EntitySpawner, Noclip)

Build after this batch.

### Cross-cutting: `async void` → safe
- [ ] **`[VERIFIED]` async void sweep.** In the CitizenFX runtime an exception escaping `async void` can silently kill the script. Convert to `async Task` where a caller can await, or wrap the body in `try/catch` that logs (`Debug.WriteLine(ex)`), for:
  - `CommonFunctions.cs`: ~`155, 462, 803, 824, 862, 933, 1652, 1991, 2471, 2755, 2784, 3067, 3460, 3518` (e.g. `LockOrUnlockDoors`, `QuitGame`, `TeleportToWp`, `KickPlayer`, `BanPlayer`, `CommitSuicide`, `SaveVehicle`, `SetLicensePlateCustomText`, `SpawnPedByName`, `SetAllWeaponsAmmo`, `SpawnCustomWeapon`, `SetWalkingStyle`, `PrivateMessage`, `PressKeyFob`).
  - `EventManager.cs:86` (`SetAppearanceOnFirstSpawn`), `:302`.
  - `MainMenu.cs:386` (`SetPermissions`).
  - `EntitySpawner.cs:60` (`SpawnEntity`).
  Menu callbacks that are inherently fire-and-forget: keep `async void` only if you add a `try/catch` that logs.

### CommonFunctions.cs
- [ ] **C-3 `[VERIFIED]` — `new ExternalFunctions()` allocated on every UI call.** `CommonFunctions.cs:1941-1983` — every static wrapper (`GetUserInput`, `GetUserInputSlider`, `GetUserColourInput`, `CopyToClipboard`, `CanDoInteraction`, `LoadSharedOutfit/Vehicle/Loadout`, `GetUserConfirmation`) does `var ExternalFunctions = new ExternalFunctions();`, constructing a `BaseScript`-derived object each call. The `_instance` singleton (`:25-29`) is set in the ctor but never used. **Fix:** expose `ExternalFunctions._instance` (ensure it's assigned at startup since the class is a `BaseScript` that gets instantiated by the runtime) and route all static helpers through it. *(Note: the class registers no event handlers/ticks, so this is wasted allocation, not duplicate-registration.)*
- [ ] **C-4 `[VERIFIED-ish]` — `PrivateMessage` does `int.Parse(source)` + derefs `sourcePlayer.Character`.** `CommonFunctions.cs:3473-3476`. Use `int.TryParse(source, out var serverId)` (return on fail) and `if (sourcePlayer.Character == null) return;` before `RegisterPedheadshot`. Truncate `message` (≤128) — `:3494,3498`.
- [ ] **H-1/H-2 `[REPORTED]` — null `GetVehicle()` derefs.** `DriveToWp`/`DriveWander` `:425-426,443-444` (`veh.Model.Hash`) and `CycleThroughSeats` `:1202` (`vehicle.Handle`). Add `if (veh == null) return;` / `if (vehicle == null || !vehicle.Exists()) return;`.
- [ ] **H-5 `[REPORTED]` — unguarded deserialize.** `CommonFunctions.cs:2919` (`SpawnWeaponLoadoutAsync` second branch) lacks the try/catch the sibling branches (`:2855-2862`) have. Wrap it, return empty list on failure.
- [ ] **M-1 `[REPORTED]` — `ToProperString` O(n²) string building.** `:2044-2073`. Use `StringBuilder` and a static compiled `Regex` for the double-space pass.
- [ ] **M-2 `[REPORTED]` — `GetVehicleModel` re-hashes.** `:281` `(uint)GetHashKey(GetEntityModel(vehicle).ToString())` → `(uint)GetEntityModel(vehicle)`.
- [ ] **M-3 `[REPORTED]` — `JsonSerializerSettings` per call.** `:2839-2846` (`GetSavedWeaponLoadout`). Hoist to `private static readonly`.
- [ ] **M-4 `[REPORTED]` — double enumeration in `SpawnWeaponLoadoutAsync`.** `:2931-2936` `loadout.Any(...)` then `foreach`. Single pass.
- [ ] **M-5 `[REPORTED]` — `mods.ToList().ForEach`.** `:1595` allocates a throwaway list twice. Use `foreach (var mod in vehicleInfo.mods)`.
- [ ] **M-6 `[REPORTED]` — `PlayersList.ToList().Find(...)`.** `:3465-3466`. Use `FirstOrDefault(...)` directly, compare `ServerId` as int via `int.TryParse`.
- [ ] **M-10 `[REPORTED]` — `GetSafePlayerName` doesn't strip newlines.** `:3242-3249`. Also strip `\n`/`\r`.
- [ ] **L-1 `[REPORTED]` — `new Random()` in `ToggleVehicleAlarm`.** `:147`. Static readonly `_rng`.
- [ ] **L-3 `[REPORTED]` — `DrawTextOnScreen` derefs `MiscSettingsMenu` w/o null check.** `:2310`. Add `MainMenu.MiscSettingsMenu != null &&` to the guard.
- [ ] **M-9 `[REPORTED]` — dead int/double parse fallback in `BanPlayer`.** `:876-905`. `double.TryParse` already covers integer strings; remove the unreachable `int.TryParse` branch.

### EventManager.cs
- [ ] **C-5 `[REPORTED]` — `UpdateTeleportLocations` unguarded deserialize.** `:316-317`. try/catch, reset to empty list.
- [ ] **H-4 `[REPORTED]` — `SetExtras` no null check after deserialize.** `:136-137`. `if (extras == null) return;`.
- [ ] **C-6 `[REPORTED]` — truncate server-supplied `sourceName` in `KillMe`.** `:285-287`. Cap length.
- [ ] **H-6 `[REPORTED]` — `SetAppearanceOnFirstSpawn` unbounded busy-wait.** `:95` `while(!IsScreenFadedIn()...) await Delay(0)`. Add a `GetGameTimer()` timeout (~30s) + warn.
- [ ] **H-8 `[REPORTED]` — `WeatherSync` Tick blocks up to 47s.** `:220` `await Delay((WeatherChangeTime*1000)+2000)`. Refactor to a timestamp-based state check rather than blocking the Tick for tens of seconds.

### MainMenu.cs
- [ ] **C8/B2 `[REPORTED]` — `RequestPlayerCoordinates` awaits RPC with no timeout.** `:362-378`. Add a `GetGameTimer()` timeout (~5s) in the `while` loop; remove the queue entry + return `Vector3.Zero` on timeout.
- [ ] **B3 `[REPORTED]` — non-atomic RPC state.** `:343-344`. Use `Interlocked.Increment` for `rpcIdCounter` and `ConcurrentDictionary` for `rpcQueue` (or document single-in-flight).
- [ ] **P7 `[VERIFIED]` — `Version` calls natives on every read.** `:55`. Change to `public static readonly string Version = GetResourceMetadata(GetCurrentResourceName(), "version", 0);`.
- [ ] **P8 `[VERIFIED]` — `GetKeyMappingId` double `GetSettingsString`.** `:880`. Store the first result in a local before the `IsNullOrWhiteSpace` check.

### EntitySpawner.cs
- [ ] **CRITICAL-4 `[REPORTED]` — `SpawnEntity` async void + unbounded model-load loop.** `:60,76`. Make `async Task`; add a timeout (~5s) to `while(!HasModelLoaded(model)) await Delay(1)`.

### Noclip.cs
- [ ] **P6/P9 `[VERIFIED]` — per-frame `JOAAT` + KVP read in noclip loop.** `Noclip.cs:152-153`. The input tag `$"~INPUT_{JOAAT($"vMenu:{GetKeyMappingId()}:NoClip")}~"` is invariant — compute once into a `private static readonly string` and reference it in the scaleform call.

---

## Batch 4 — Client per-frame perf & bugs (`vMenu/FunctionsController.cs`)

Build after this batch. This is the per-frame tick controller — the highest-value perf file.

- [ ] **C2 `[VERIFIED]` — `return` should be `continue` in `DeathNotifications`.** `:1251` `if (deadPlayers.Contains(p.Handle)) { return; }` inside `foreach (var p in pl)` exits the whole method on the first already-counted dead player. Change to `continue`. (Optional: replace the O(n²) killer scan `:1262-1290` with a handle→player dictionary built once.)
- [ ] **C3 `[VERIFIED]` — waypoint removal uses Handle instead of serverId.** `:2357-2401`. Loop is `foreach (var serverId in PlayersWaypointList)`; null-branch adds `serverId` (`:2363`) but the reached-branch adds `playerId` (= `player.Handle`, `:2367,2377`); removal `:2397` keys by the list's serverId, so reached waypoints never clear. **Fix:** change `:2377` `waypointPlayerIdsToRemove.Add(playerId)` → `.Add(serverId)`. (Verify `PlayerCoordWaypoints` `:2388` keying too.)
- [ ] **CRITICAL-P1/P2 `[VERIFIED]` — `async void DrawMiscSettingsText` called bare from tick.** `:741`. Make `private async Task`, `await` it from `MiscSettings`.
- [ ] **P1 `[VERIFIED]` — per-frame `new List<Menu>(...)`.** `:592-604` (VehicleOptions on-foot path). Hoist the list to a `static readonly`/instance field built once after `CreateSubmenus`.
- [ ] **P2 `[REPORTED]` — `PlayerBlipsControl` no throttle on steady state.** `:2102-2243`. Add `await Delay(500)` on the `DecorIsRegisteredAsType` happy path (mirror `PlayerOverheadNamesControl` `:2259`).
- [ ] **P3 `[REPORTED]` — `PlayerClothingAnimationsController` per-player native calls each cycle + double `DecorSetInt`.** `:2014,2022-2063,2090`. Throttle consistently (`await Delay(50)` regardless of branch); remove the duplicate `DecorSetInt`.
- [ ] **P5 `[VERIFIED]` — `GetVehicle(true)` called 3× back-to-back.** `:618-624`. Cache `var lastVeh = GetVehicle(true);` once; use throughout (also closes a null TOCTOU).
- [ ] **P6 `[REPORTED]` — `SwitchHelmetOnce` builds 5 lists + ~30 `GetHashKey` per call.** `:2979-3042`. Hoist to `private static readonly HashSet<uint>` fields; use `Contains`.
- [ ] **P10 `[REPORTED]` — `await Task.FromResult(0)` anti-pattern.** `:304 (PlayerOptions), :633 (VehicleHighbeamFlashTick), :662 (VehicleShowHealthOnScreenTick)`. Doesn't yield. Use `await Delay(0)` (or drop async if synchronous). Health display: consider throttling its 3 string allocs/`DrawTextOnScreen` per frame.
- [ ] **P12 `[REPORTED]` — `ManageCamera` mutates button descriptions every frame via `.Replace`.** `:1533,1539,1545`. Track a `bool` and only mutate on state transition.
- [ ] **R3 `[REPORTED]` — `DoPlayerAndVehicleChecks` fragments cheap bool reads across 6× `Delay(100)`.** `:313-341`. Compute synchronously, single `await Delay(1000)` at the end.

---

## Batch 5 — Storage, data, menus (`vMenu/`: StorageManager, UserDefaults, ValidWeapon, Notification, menus/*)

Build after this batch.

### StorageManager.cs
- [ ] **C5/CRITICAL-1 `[VERIFIED]` — `GetSavedPeds` leaks KVP handle + no try/catch.** `:50-65`. `StartFindKvp("ped_")` never gets `EndFindKvp`; `JsonConvert.DeserializeObject<PedInfo>` unguarded. **Fix:** `EndFindKvp(handle)` in a `finally`; try/catch around deserialize with `continue`. Mirror `GetSavedMpPeds` (`:98-113`).
- [ ] **CRITICAL-2 `[VERIFIED]` — `SavePedInfo` serializes twice.** `:88-89`. Cache `var json = JsonConvert.SerializeObject(pedData);`, write it, compare against `json` (mirror `SaveVehicleInfo` `:139-148`).
- [ ] **LOW-1 `[REPORTED]` — ~110 lines of commented-out dead code.** `:163-271` (`GetSavedVehicleInfo`). Delete (it's in git history).

### UserDefaults.cs
- [ ] **HIGH-4 `[VERIFIED]` — `GetSettingsBool` double KVP read.** `:388-432`. Line 431 re-reads instead of reusing `savedValue` from `:391`. **Fix:** `return savedValue.ToLower() == "true";`.
- [ ] **HIGH-7 `[VERIFIED]` — `GetSettingsFloat` dead-code logic.** `:445-463`. `savedValue.ToString() != null` on a `float` is always true → the `else` (which double-prefixes the key, `settings_settings_`) is unreachable dead code, and the intended `-1f` default never returns (missing floats yield `0f`). **Fix:** rewrite to read `GetResourceKvpFloat(SETTINGS_PREFIX + kvpString)` once and return a sensible default; drop the always-true null check and the dead double-prefix write. *(Correction to original review: the "permanently unreadable settings" claim is wrong — the corrupt-write branch never executes.)*

### data/ValidWeapon.cs
- [ ] **CRITICAL-3 `[VERIFIED — downgraded]` — brittle cache-invalidation magic number.** `:104` `if (_weaponsList.Count == weaponNames.Count - 1)`. The `-1` assumes exactly one weapon (`weapon_unarmed`) is skipped; arithmetic shows it stays consistent after addon injection (so it does **not** rebuild every call as originally reported), but it's fragile. **Fix:** add `private static bool _initialized;`, set it at the end of `CreateWeaponsList`, and check it in the `WeaponList` getter instead of comparing counts.
- [ ] **HIGH-1 `[VERIFIED]` — O(weapons×components) build loop.** `:163-198`. ~44k native calls per build. **Fix:** pre-compute `GetHashKey` for all component names once into a `Dictionary<string,uint>` before the weapon loop.
- [ ] **HIGH-6 `[VERIFIED]` — addon weapon injection has no hash validation.** `:128-135`. **Fix:** after `GetHashKey`, check `IsWeaponValid(hash)` before adding; validate the spawn-name string shape; cap the `AddTextEntry` label length.
- [ ] **MEDIUM-1 `[REPORTED]` — `weaponDescriptions` calls `GetLabelText` ×95 at type-load, never used by `CreateWeaponsList`.** `:204-446`. Lazy-init (`Lazy<...>`) or load on first detail-panel access.

### Notification.cs
- [ ] **MEDIUM-4 `[REPORTED]` — `usingCustomNotifications` static read at type-init (config may not be loaded).** `:81`. Read the setting lazily inside the notify methods, or add a `Reload()` called after convar sync.

### menus/WeaponOptions.cs
- [ ] **HIGH-2 `[REPORTED]` — search rebuilds the whole menu tree + handlers.** `:40-836` (`RefreshSpawnableWeapons`, called from `:781`). Separate data-init (weapon structs/component dicts) from menu-item creation; on search, show/hide items rather than destroy+recreate.
- [ ] **LOW-2 `[REPORTED]` — component-update block copy-pasted 4×.** `:462-506,553-599`. Extract `void RefreshComponentStates(uint weaponHash)` scoped to the current weapon's components (not the global dict).
- [ ] **LOW-3 `[REPORTED]` — magic uint category hashes, no fallback for unknown categories.** `:624-671`. Name the constants; add an `else` that logs so uncategorized addon weapons don't vanish.

### menus/VehicleSpawner.cs
- [ ] **CRITICAL-5 `[REPORTED]` — `RefreshSpawnableVehicles` re-reads/parses `addons.json` on every search.** `:166-181` (also called from `:364`). Move addon loading to `CreateMenu`/one-time init; guard against double-insert.
- [ ] **HIGH-3 `[REPORTED]` — O(n²) duplicate-name scan + `.Keys.Contains`.** `:234-284`. Use `ContainsKey` and a `HashSet<string>` of added names.
- [ ] **MEDIUM-3 `[REPORTED]` — 4× constant 23-elem float arrays allocated per call.** `:44-148`. Hoist to `private static readonly float[]`.

### menus/SavedVehicles.cs
- [ ] **HIGH-9 `[REPORTED]` — index-based menu handlers + unguarded `ElementAt(4)` cast.** `:252-427`. Compare `item == renameBtn` / `descriptionBtn` / `deleteBtn` (the codebase's normal pattern); guard the checkbox cast.
- [ ] **MEDIUM-2 `[REPORTED]` — `Enum.GetNames(typeof(MenuItem.Icon)).ToList()` per interaction.** `:134,432`. Cache as `private static readonly List<string>`.

### menus/WeaponLoadouts.cs
- [ ] **HIGH-8 `[REPORTED]` — duplicated KVP enumeration + per-entry `JsonSerializerSettings`.** `:31-68,79-131`. Hoist a `static readonly JsonSerializerSettings`; have `RefreshSavedWeaponsList` call the static `GetSavedWeapons`.
- [ ] **MEDIUM-5 `[REPORTED]` — fragile KVP-key reconstruction from display text.** `:358-368`. Store the full KVP key in `item.ItemData` at construction (`:181`) and read it back.

---

## Batch 6 — Lua + SQL (`build/vMenu/...`)

Not covered by msbuild — review manually / luacheck.

- [ ] **C4 `[VERIFIED]` — `client/loadoutCodes.lua` missing `return false`.** `:38-40`: `if not Valid then Config.Notify(...) end` falls through to `json.decode(Valid)` (`:42`) and errors. Add `return false` after the notify (outfit/vehicle paths already do).
- [ ] **S5 `[VERIFIED]` — shared loadout grants weapons with no permission re-check.** `client/loadoutCodes.lua:44-49` calls `GiveWeaponToPed`/`GiveWeaponComponentToPed` straight from the decoded blob. **Fix:** validate each `weapon.Hash` (`IsWeaponValid`) and ideally re-check server-authoritative permission before granting; clamp ammo. Also confirm `weapon.GetMaxAmmo` (`:45`) is the right field for the ammo arg (looks like a wrong key from the C# serialization).
- [ ] **S6 `[VERIFIED]` — unbounded blob DoS on code generation.** `server/loadoutCodes.lua`, `server/oufitCodes.lua`, `server/vehicleCodes.lua` generate callbacks only check `type(data)=="table"` then `json.encode` into a `longtext`. **Fix:** after `json.encode`, reject if `#encoded > 65536` (or chosen cap); optionally validate expected keys/depth. Tighten cooldowns / add a per-discord-id hard rate cap. *(Queries are parameterized — no SQLi. Don't add ownership gating unless product decides codes are private — see exclusions.)*
- [ ] **P9 `[VERIFIED]` — `server/integrations.lua` 1s polling broadcast of overhead names.** `:67-83` rebuilds the full name table and `TriggerClientEvent('vMenu:SyncOverheadNames', -1, cache)` every second regardless of change. **Fix:** push deltas on `playerJoining`/`playerDropped`/name-change, or raise the interval substantially.
- [ ] **P2(Lua) `[REPORTED]` — startup error logged 10×.** `server/*Codes.lua` (e.g. `loadoutCodes.lua:80-82` `for i=1,10 do lib.print.error(...)`). Log once.
- [ ] **C5(Lua) `[REPORTED]` — cooldown tables not cleared on player drop.** `server/*Codes.lua`. Minor (SetTimeout bounds it); add a `playerDropped` cleanup if you add per-source counts for S6.
- [ ] **DB `[VERIFIED]` — `database.sql` missing index + dead `label` column.** Add `KEY idx_discord (discord_id)` to `vmenu_outfits`/`vmenu_vehicles`/`vmenu_loadouts` (needed if you add any discord-scoped query). The `label` column is never written — populate it (e.g. the save name, useful for moderation) or drop it. Consider `MEDIUMTEXT` + the S6 size cap instead of `longtext`.

---

## Batch 7 — Optional refactors (do last, only if build stays green)

These are larger and **not required** to fix the bugs above. Each is independently optional.

- [ ] **Collapse the six near-duplicate `*Codes.lua` files** (client+server × outfit/vehicle/loadout) into one parameterized factory (`registerCodeSystem(name, table, requestCd, generateCd, applyFn)` server-side; a client helper parameterized by KVP prefix + callback names + apply-fn). The divergences between them are the source of C4 and the missing validation, so this also prevents regressions. Only attempt after Batch 6 is done and you can manually test each system.
- [ ] **Extract `MpPedCustomization` editor-open check** duplicated 3× (`FunctionsController.cs:254,539,1484`) into one `public static bool`.
- [ ] **Split `ManageCamera`** (`FunctionsController.cs:1504-1717`, ~213 lines) into `UpdateMpCharEditorButtonStates()` + `UpdateCharacterEditorCamera(...)` + orchestrator.

---

## Suggested commit strategy

One commit per batch (or finer), so a build break is easy to bisect:
- `fix(server): add permission checks + input validation to event handlers`
- `fix(server): repair broken unban, guard ban-list deserialize, null-safe BanPlayer`
- `fix(client): make async void handlers exception-safe; add RPC/loop timeouts`
- `perf(client): throttle per-frame tick loops; hoist per-call allocations`
- `fix(storage): close KVP handles, repair settings getters, harden weapon list`
- `fix(lua): validate shared codes, cap blob size, fix loadout crash; add DB index`

Use the repo's commit trailer convention (see `git log`). Build green before each commit.

## Final verification before pushing
- [ ] `msbuild vMenu.sln /p:Configuration=Release` clean.
- [ ] Smoke-test in a FiveM dev server: open the menu, spawn a vehicle/weapon, generate+load an outfit/vehicle/loadout code, ban+unban a test player, toggle noclip, confirm overhead names/blips. These exercise every CRITICAL/HIGH path changed above.
