# Runtime Addon Cars / Weapons / EUP with ACE Permissions — Design

> **Date:** 2026-06-23 · **Baseline:** `main` · **Status:** approved direction, pending spec review

## 1. Problem

vMenu's permission system is **compiled**: the `Permission` enum (~370 entries),
its `GetAceName()` ACE-string mapping, and the per-player permission dictionary
are all baked into the DLL (`SharedClasses/PermissionsManager.cs`). The server
loops every enum value per joining player, evaluates `IsPlayerAceAllowed`, and
ships a `Dictionary<Permission,bool>` blob to the client
(`SetPermissionsForPlayer` → `vMenu:SetPermissions`).

Anything defined at **runtime** (addon cars/weapons/EUP declared in a `config.lua`)
therefore has no enum slot to bind to. Across `main`, `origin/dev`
(commit `558638d` *"(remove): Custom permissions per weapon"*), and `origin/wip`
(`ValidAddonWeapon.cs` with an unset `Perm` field), the same gap recurs: addon
**items appear**, but their **permissions never gate**. On `main` specifically,
addon weapons load at runtime from `addons.json`, the `DynamicWeaponPermissions`
dict (`ValidWeapon.cs:114`) is declared but never populated, and every addon
weapon falls back to `Permission.WPAll`.

## 2. Goal

Load extra cars, weapons, and EUP into the menu **at runtime from `config.lua`**,
each gated by a **FiveM ACE permission** ("the current format"), evaluated
server-side and honored by vMenu's existing **C#** menu — with **no recompile**
required to add or re-permission an item.

## 3. Decisions (locked)

| Decision | Choice |
|---|---|
| Permission granularity (cars/weapons) | **Per-item ACE** — `vMenu.Addons.vehicles.<spawn>` / `vMenu.Addons.weapons.<spawn>`, with an optional per-item explicit override |
| EUP | A **single group ACE** (`vMenu.Addons.eup`) that exposes the extended clothing slider ranges |
| EUP base-count source | A **compiled C# base table** of base-game variation counts per ped/component; **fail open + log** for models absent from the table |
| Menu technology | vMenu's existing **C#** menu system (LemonUI/MenuAPI) |
| Config authoring | **`config_server.lua`** (server is single source of truth) |
| Permission format | FiveM **ACE** (`add_ace`/`add_principal`), evaluated server-side |
| Architecture | **Approach A** — Lua reads config + evaluates ACE, C# consumes a runtime registry |
| Delivery | **Request/response** (client asks on init; survives resource restart) |
| Security posture | Server sends **only allowed items** (no client-side "locked" state to bypass) |

### Non-goals

- Not touching the compiled `Permission` enum for addons.
- Not replacing the existing `addons.json` path (it stays, orthogonal — see §10).
- Not adding server-authoritative *spawn* enforcement beyond payload filtering
  (vMenu spawns vehicles client-side; that's a separate, larger change).

## 4. Architecture (Approach A)

The menu is C# (client), the config is Lua, and ACE checks must run server-side.
C# cannot read Lua tables, so **Lua owns config-reading + ACE evaluation** and
pushes a result the C# client consumes.

```
Client C#  EventManager.SetConfigOptions handler  (fires on vMenu:SetConfigOptions)
   └─ TriggerServerEvent("vMenu:RequestAddons")

Server Lua  build/vMenu/server/addons.lua   (NEW)
   ├─ read Config.Addons  (from config_server.lua)
   ├─ for each vehicle/weapon: ace = explicit `permission` OR derived default
   │        allowed = IsPlayerAceAllowed(src, ace)
   ├─ build payload containing ONLY allowed vehicles/weapons
   │        + eup = { allowed = (not Config.Addons.eup.enabled)
   │                              or IsPlayerAceAllowed(src, eup.permission) }
   └─ TriggerClientEvent("vMenu:SetAddons", src, json.encode(payload))

Client C#  EventManager handler "vMenu:SetAddons"
   ├─ AddonsManager.Load(json)         → runtime registry populated
   └─ AddonsSetupComplete = true

MainMenu.SetPermissions()               (MainMenu.cs:427)
   └─ await ConfigOptionsSetupComplete AND AddonsSetupComplete
      → PostPermissionsSetup()          (MainMenu.cs:445)
         → CreateSubmenus()             (MainMenu.cs:642)
            └─ if AddonsManager.HasAny → create + populate Addons menu
```

**Why request/response, triggered from `vMenu:SetConfigOptions`:** the server
already fires `vMenu:SetConfigOptions` right after the permission blob, both on
join and on the resource-restart first-tick loop (`SetPermissionsForPlayer` →
`MainServer.cs:1207`). Triggering `vMenu:RequestAddons` from that handler ties
addon delivery to the existing config-ready cadence — it fires on join *and*
re-fires on restart, with no separate init timing to coordinate. A Lua
`playerJoining` hook alone would miss the restart case. Ordering with the
enum-permission blob is irrelevant: the menu-build gate waits on **both**
`ConfigOptionsSetupComplete` and `AddonsSetupComplete`, so whichever event lands
first, construction only proceeds once both are satisfied.

## 5. Config schema (`build/vMenu/config/config_server.lua`)

```lua
Config.Addons = {
    -- ACE auto-derived as vMenu.Addons.vehicles.<spawn> unless `permission` is set.
    vehicles = {
        { spawn = 'police3',   label = 'LSPD Cruiser' },
        { spawn = 'ambulance', label = 'Ambulance' },
        { spawn = 'adder',     label = 'VIP Adder', permission = 'vMenu.Addons.vip' },
    },
    weapons = {
        { spawn = 'weapon_raypistol',     label = 'Up-n-Atomizer' },
        { spawn = 'weapon_militaryrifle', label = 'Military Rifle' },
    },
    eup = {
        enabled    = true,
        permission = 'vMenu.Addons.eup',
    },
}
```

Validation (server Lua, mirrors `ValidWeapon.cs:132` hardening):
- `spawn` must be a non-empty string ≤ 64 chars; weapons must start with `weapon_`.
- `label` optional (default to `spawn`); truncate to ≤ 64.
- Bad entries are **skipped and logged once**, never abort the whole list.

## 6. ACE naming

| Item | Default ACE | Override |
|---|---|---|
| Vehicle | `vMenu.Addons.vehicles.<spawn>` (lower-cased) | per-item `permission` |
| Weapon | `vMenu.Addons.weapons.<spawn>` (lower-cased) | per-item `permission` |
| EUP | from `eup.permission` (default `vMenu.Addons.eup`) | — |

These strings are **dynamic** — not in the compiled enum. `permissions.cfg` may
list `add_ace`/`add_principal` defaults for documentation. No `GetAceName` change.

## 7. C# components

### 7.1 `vMenu/data/AddonsManager.cs` (new)
Static runtime registry, populated from the `vMenu:SetAddons` payload. Holds
`List<AddonVehicle>`, `List<AddonWeapon>`, and `EupState Eup`. Exposes
`Load(string json)`, `bool HasAny`, and `bool Eup.Allowed`. Follows the pattern of
`ValidWeapon`/`VehicleData` runtime stores. Deserialize wrapped in try/catch →
empty registry on failure.

- `AddonVehicle { string Spawn; string Label; }`
- `AddonWeapon { string Spawn; string Label; uint Hash; Dictionary<string,uint> Components; }`
  (resolve `Hash`/`Components` client-side after load, reusing the component-hash
  logic in `ValidWeapon.cs:181`)

### 7.2 `vMenu/menus/Addons.cs` (new)
Builds a `Menu` with **"Addon Vehicles"** and **"Addon Weapons"** sub-lists from
the registry.
- Vehicle select → `CommonFunctions.SpawnVehicle(spawn, SpawnInside, ReplacePrevious)`
  (`CommonFunctions.cs:1311`).
- Weapon select → give-weapon path mirroring `WeaponOptions.cs:712`
  (`GiveWeaponToPed(Game.PlayerPed.Handle, hash, maxAmmo, false, true)` + components).
- Existing `CanDoInteraction` / spawn cooldown still apply via `SpawnVehicle`.

### 7.3 `vMenu/MainMenu.cs` (edit)
- Add `public static Addons AddonsMenu { get; private set; }`.
- Add `public static bool AddonsSetupComplete = false;`.
- In `SetPermissions` (`:427`), extend the wait: `while (!ConfigOptionsSetupComplete || !AddonsSetupComplete) await Delay(100);`.
- In `CreateSubmenus` (`:642`), after the existing menus: `if (AddonsManager.HasAny) { AddonsMenu = new Addons(); AddMenu(...); }`.

### 7.4 `vMenu/EventManager.cs` (edit)
- Register `EventHandlers.Add("vMenu:SetAddons", new Action<string>(SetAddons));`.
- `SetAddons(json)` → `AddonsManager.Load(json); MainMenu.AddonsSetupComplete = true;`.
- In the existing `SetConfigOptions` handler (`:127`), after `SetExtras()`, add
  `TriggerServerEvent("vMenu:RequestAddons");`. This ties the request to the
  config-ready signal and re-fires on resource restart (see §4). The
  `vMenu:SetAddons` handler is registered in the constructor, so it is ready
  before any response arrives.

### 7.5 `build/vMenu/server/addons.lua` (new)
Handles `vMenu:RequestAddons` from `[FromSource]`, reads `Config.Addons`,
evaluates `IsPlayerAceAllowed(src, ace)` per item, builds the **allowed-only**
payload, and `TriggerClientEvent("vMenu:SetAddons", src, json.encode(payload))`.
Loaded automatically by the `server/*.lua` glob in `fxmanifest.lua`.
Mirrors the security note in `server/integrations.lua:8` (always ACE-check the
source).

## 8. EUP clothing-slider gating

GTA's `GetNumberOfPedDrawableVariations` / `GetNumberOfPedTextureVariations`
return the **total** (base + streamed EUP); there is no runtime base/EUP boundary.

- **`vMenu/data/EupBaseCounts.cs` (new, compiled):**
  `static Dictionary<uint, Dictionary<int,int>> Base` — base-game drawable counts
  per `(ped model hash, component index)` for the standard freemode peds
  (`mp_m_freemode_01`, `mp_f_freemode_01`) and common service peds. Stable across
  vMenu builds (base-game counts don't change).
- **Gate points** (cap `maxVariations` before building the slider list item):
  - `PlayerAppearance.cs:756` (`GetNumberOfPedDrawableVariations`) / `:757` (textures), loop `:764`.
  - `MpPedCustomization.cs:572` / `:580`, loop `:575`; plus the texture update at `:1381`.
- **Rule:** if `!AddonsManager.Eup.Allowed`, set
  `max = min(max, EupBaseCounts.For(model, component))`. If the model/component is
  **absent** from the table → **no cap + log once** (fail open). When EUP is
  allowed (or `eup.enabled = false`), behavior is unchanged.

## 9. Error handling

- **Lua:** per-entry validation + skip/log; never abort the list.
- **Client deserialize:** try/catch → empty registry → Addons menu simply absent.
- **Spawn:** `SpawnVehicle` validates `IsModelAVehicle`; weapons validate
  `IsWeaponValid` (reuse `ValidWeapon` logic). Invalid items are filtered at load.
- **Build:** target net462/net452 — use `CitizenFX.Core.MathUtil.Clamp` (not
  `Math.Clamp`) and `!float.IsNaN/IsInfinity` (not `float.IsFinite`). After build,
  `git checkout -- build/vMenu/config/permissions.cfg` (known artifact churn) and
  delete any stray `bash.exe.stackdump`.

## 10. Backward compatibility

The existing `addons.json` path is **left untouched**: addon cars still sort into
the normal vehicle-class lists, addon weapons still appear in Weapon Options under
category permissions. The new config.lua-driven **Addons menu** is an independent,
per-item-permissioned home. **Docs note:** don't list the same item in both
mechanisms (it would appear twice). Owners migrate at their own pace.

## 11. Testing

1. **Build:** `msbuild vMenu.sln /p:Configuration=Release` clean; revert
   `permissions.cfg` churn; remove stackdumps.
2. **In-game:** declare 2 cars + 2 weapons + `eup` in `config_server.lua`;
   `add_ace` one principal and deliberately not another.
   - Allowed player: sees + spawns the allowed items; EUP slider shows full range.
   - Denied player: Addons menu shows none of the denied items (and is absent if
     all are denied); EUP slider capped to base for table-known peds.
   - Per-item override (`permission = 'vMenu.Addons.vip'`) gates independently.
3. **Resource restart** with players connected: client re-requests → menu repopulates.
4. **Malformed config:** bad entry skipped + logged; rest of the list still loads.
5. **Lua:** eyeball / luacheck against ox globals (`lib`, `cache`, `Config`).

## 12. Risks / notes

- **EUP table coverage:** only table-known peds get capped; others fail open by
  design (logged). Acceptable per the locked decision.
- **Client-spawn trust:** payload filtering is the protection; a determined modder
  could still spawn a model manually — true of all vMenu, out of scope here.
- **Timing:** the menu-build gate must wait on `AddonsSetupComplete`; if the
  request/response is ever dropped, the Addons menu won't appear (fails safe) —
  consider a timeout fallback that proceeds without addons rather than hanging the
  gate (mirror the 5 s RPC timeout pattern at `MainMenu.cs:370`).
