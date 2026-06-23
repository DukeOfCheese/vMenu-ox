# Runtime Addon Cars/Weapons/EUP with ACE Permissions — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Load extra cars, weapons, and EUP into vMenu at runtime from `config_server.lua`, each gated by a FiveM ACE permission evaluated server-side — with no recompile needed to add or re-permission an item.

**Architecture:** A new server Lua script reads `Config.Addons`, evaluates `IsPlayerAceAllowed` per item, and pushes an **allowed-only** JSON payload to the client (request/response, triggered from the existing `vMenu:SetConfigOptions` handshake). A new C# runtime registry (`AddonsManager`) holds the payload; a new C# `Addons` menu renders permitted vehicles/weapons; EUP is a single group flag that caps the clothing sliders to a compiled base-count table. The compiled `Permission` enum is never touched for addons.

**Tech Stack:** C# (net462, LangVersion 12, MenuAPI.FiveM 3.2.2, Newtonsoft.Json), Lua (CitizenFX + ox_lib globals), MSBuild (VS2022).

**Design spec:** `docs/superpowers/specs/2026-06-23-runtime-addons-permissions-design.md`

## Global Constraints

- **Baseline branch:** `feat/runtime-addons-permissions` (already created off `main`; spec already committed there).
- **No unit-test framework exists** for this FiveM resource and client code depends on game natives, so the per-task "test" cycle is: **(a) Release build succeeds with 0 errors**, and **(b) the consolidated in-game smoke test in Task 10**. Each C# task's verification step is the build; Lua tasks are verified structurally + in Task 10.
- **Build command** (VS2022 MSBuild is not on PATH):
  ```
  "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" vMenu.sln /p:Configuration=Release
  ```
  First build of the session only: prefix with a restore — `"...\MSBuild.exe" vMenu.sln /t:restore`. Expected tail: `Build succeeded.` … `0 Error(s)`.
- **Post-build hygiene (run after every build, before committing):**
  ```
  git checkout -- build/vMenu/config/permissions.cfg
  ```
  and delete any stray `bash.exe.stackdump` / `vMenu/bash.exe.stackdump`. The csproj copies config on every build and reverts a few ACE lines in `permissions.cfg`; never commit that churn.
- **net462 API limits:** `Math.Min`/`Math.Max` exist; **`Math.Clamp` does NOT** (use `CitizenFX.Core.MathUtil.Clamp`), and **`float.IsFinite` does NOT** (use `!float.IsNaN(x) && !float.IsInfinity(x)`).
- **SDK-style csproj** uses default file globbing — **new `.cs` files under `vMenu/` compile automatically; no `.csproj` edits.**
- **JSON contract is lower-case** (Lua `json.encode` keys): `vehicles`, `weapons`, `eup`, `spawn`, `label`, `allowed`. C# DTO field names must match exactly.
- **Commit cadence:** one commit per task, on `feat/runtime-addons-permissions`. Trailer convention:
  ```
  Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01YEaUAeWEj7A1guWaDD484u
  ```

## File Structure

| File | New/Edit | Responsibility |
|---|---|---|
| `vMenu/data/AddonsManager.cs` | New | Runtime registry + JSON payload DTO + `Load(json)` |
| `vMenu/data/EupBaseCounts.cs` | New | Compiled base-game drawable counts; `For(model, comp)` lookup |
| `vMenu/menus/Addons.cs` | New | The Addons menu (vehicle + weapon sub-lists, spawn/give) |
| `vMenu/MainMenu.cs` | Edit | `AddonsMenu` field, `AddonsSetupComplete` gate, menu creation |
| `vMenu/EventManager.cs` | Edit | Register `vMenu:SetAddons`; fire `vMenu:RequestAddons` |
| `vMenu/menus/PlayerAppearance.cs` | Edit | EUP drawable-slider cap |
| `vMenu/menus/MpPedCustomization.cs` | Edit | EUP drawable-slider cap (MP creator) |
| `build/vMenu/config/config_server.lua` | Edit | `Config.Addons` schema + example |
| `build/vMenu/server/addons.lua` | New | ACE evaluator; sends allowed-only payload |

---

### Task 1: AddonsManager runtime registry

**Files:**
- Create: `vMenu/data/AddonsManager.cs`

**Interfaces:**
- Produces:
  - `vMenuClient.data.AddonsManager.Vehicles` → `List<AddonVehicle>` where `AddonVehicle { string spawn; string label; }`
  - `vMenuClient.data.AddonsManager.Weapons` → `List<AddonWeapon>` where `AddonWeapon { string spawn; string label; }`
  - `vMenuClient.data.AddonsManager.Eup` → `EupState { bool allowed; }`
  - `static bool HasAny` (true if any vehicle or weapon)
  - `static void Load(string json)`

- [ ] **Step 1: Create the file**

```csharp
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
```

- [ ] **Step 2: Build (compile check)**

Run the Release build (see Global Constraints). Expected: `Build succeeded.` `0 Error(s)`.

- [ ] **Step 3: Post-build hygiene + commit**

```bash
git checkout -- build/vMenu/config/permissions.cfg
git add vMenu/data/AddonsManager.cs
git commit -m "feat(addons): runtime addon registry (AddonsManager)"
```

---

### Task 2: Addons menu

**Files:**
- Create: `vMenu/menus/Addons.cs`

**Interfaces:**
- Consumes: `AddonsManager.Vehicles`, `AddonsManager.Weapons` (Task 1); `CommonFunctions.SpawnVehicle(string, bool, bool)`.
- Produces: `vMenuClient.menus.Addons` with `public Menu GetMenu()`.

**Notes for implementer:** `CommonFunctions.SpawnVehicle(string vehicleName, bool spawnInside, bool replacePrevious)` is the addon-car spawner (returns `Task<int>`). Weapons are given with `GiveWeaponToPed(ped, hash, ammo, false, true)`. `Notify` resolves from the parent `vMenuClient` namespace. The submenu-binding trio (`MenuController.AddSubmenu` / `menu.AddMenuItem(btn)` / `MenuController.BindMenuItem`) mirrors `menus/MiscSettings.cs:113-114`.

- [ ] **Step 1: Create the file**

```csharp
using CitizenFX.Core;

using MenuAPI;

using vMenuClient.data;

using static CitizenFX.Core.Native.API;
using static vMenuClient.CommonFunctions;

namespace vMenuClient.menus
{
    public class Addons
    {
        private Menu menu;

        private void CreateMenu()
        {
            menu = new Menu(Game.Player.Name, "Addons");

            // ---- Addon Vehicles ----
            if (AddonsManager.Vehicles.Count > 0)
            {
                var vehMenu = new Menu(Game.Player.Name, "Addon Vehicles");
                var vehBtn = new MenuItem("Addon Vehicles", "Spawn an addon vehicle you have access to.") { Label = "→→→" };
                MenuController.AddSubmenu(menu, vehMenu);
                menu.AddMenuItem(vehBtn);
                MenuController.BindMenuItem(menu, vehMenu, vehBtn);

                var spawnInside = new MenuCheckboxItem("Spawn Inside", "Teleport into the vehicle after it spawns.", true);
                var replacePrev = new MenuCheckboxItem("Replace Previous", "Delete your previous vehicle when spawning a new one.", false);
                vehMenu.AddMenuItem(spawnInside);
                vehMenu.AddMenuItem(replacePrev);

                foreach (var v in AddonsManager.Vehicles)
                {
                    var label = string.IsNullOrEmpty(v.label) ? v.spawn : v.label;
                    var item = new MenuItem(label, $"Spawn {label}.") { ItemData = v.spawn };
                    vehMenu.AddMenuItem(item);
                }

                vehMenu.OnItemSelect += async (sender, item, index) =>
                {
                    if (item == spawnInside || item == replacePrev)
                    {
                        return;
                    }
                    if (item.ItemData is string spawnName)
                    {
                        await SpawnVehicle(spawnName, spawnInside.Checked, replacePrev.Checked);
                    }
                };
            }

            // ---- Addon Weapons ----
            if (AddonsManager.Weapons.Count > 0)
            {
                var wepMenu = new Menu(Game.Player.Name, "Addon Weapons");
                var wepBtn = new MenuItem("Addon Weapons", "Give yourself an addon weapon you have access to.") { Label = "→→→" };
                MenuController.AddSubmenu(menu, wepMenu);
                menu.AddMenuItem(wepBtn);
                MenuController.BindMenuItem(menu, wepMenu, wepBtn);

                foreach (var w in AddonsManager.Weapons)
                {
                    var label = string.IsNullOrEmpty(w.label) ? w.spawn : w.label;
                    var item = new MenuItem(label, $"Give {label}.") { ItemData = w.spawn };
                    wepMenu.AddMenuItem(item);
                }

                wepMenu.OnItemSelect += (sender, item, index) =>
                {
                    if (item.ItemData is string spawnName)
                    {
                        var hash = (uint)GetHashKey(spawnName);
                        if (!IsWeaponValid(hash))
                        {
                            Notify.Error("That addon weapon is not valid on this server.");
                            return;
                        }
                        var maxAmmo = 0;
                        GetMaxAmmo(Game.PlayerPed.Handle, hash, ref maxAmmo);
                        GiveWeaponToPed(Game.PlayerPed.Handle, hash, maxAmmo > 0 ? maxAmmo : 250, false, true);
                    }
                };
            }
        }

        public Menu GetMenu()
        {
            if (menu == null)
            {
                CreateMenu();
            }
            return menu;
        }
    }
}
```

- [ ] **Step 2: Build (compile check)** — Release build, expect `0 Error(s)`. If `Notify` fails to resolve, qualify as `vMenuClient.Notify.Error(...)`.

- [ ] **Step 3: Post-build hygiene + commit**

```bash
git checkout -- build/vMenu/config/permissions.cfg
git add vMenu/menus/Addons.cs
git commit -m "feat(addons): Addons menu for permitted addon vehicles/weapons"
```

---

### Task 3: Wire the Addons menu into MainMenu + gate on addon data

**Files:**
- Modify: `vMenu/MainMenu.cs` (field near `:37`, flag near `:27`, gate in `SetPermissions` `:427`, creation in `CreateSubmenus` after `:682`)

**Interfaces:**
- Consumes: `AddonsManager.HasAny` (Task 1), `Addons` (Task 2).
- Produces: `MainMenu.AddonsMenu` (`Addons`), `MainMenu.AddonsSetupComplete` (`bool`).

- [ ] **Step 1: Add the menu field** (next to the other `...Menu { get; private set; }` fields, e.g. after the `OnlinePlayersMenu` declaration around `:37`)

```csharp
public static Addons AddonsMenu { get; private set; }
```

- [ ] **Step 2: Add the readiness flag** (next to `public static bool ConfigOptionsSetupComplete = false;` around `:27`)

```csharp
public static bool AddonsSetupComplete = false;
```

- [ ] **Step 3: Gate menu construction on addon data** — in `SetPermissions`, replace the existing config wait:

Find:
```csharp
                ArePermissionsSetup = true;
                while (!ConfigOptionsSetupComplete)
                {
                    await Delay(100);
                }
                PostPermissionsSetup();
```
Replace with:
```csharp
                ArePermissionsSetup = true;
                while (!ConfigOptionsSetupComplete)
                {
                    await Delay(100);
                }
                // Wait for the per-player addon payload, but never hang the menu: if the
                // server's addon response is dropped, build the menu without addons.
                var addonWaitStart = GetGameTimer();
                while (!AddonsSetupComplete)
                {
                    if (GetGameTimer() - addonWaitStart > 5000)
                    {
                        Debug.WriteLine("[vMenu] [Addons] Timed out waiting for addon data; building menu without addons.");
                        break;
                    }
                    await Delay(100);
                }
                PostPermissionsSetup();
```

- [ ] **Step 4: Create the Addons menu** — in `CreateSubmenus`, immediately after the Banned Players block closes (the `}` at `:682`, right before `var playerSubmenuBtn = ...`), insert:

```csharp
            // Add the Addons menu (runtime addon vehicles/weapons this player is permitted to use).
            if (data.AddonsManager.HasAny)
            {
                AddonsMenu = new Addons();
                var menu = AddonsMenu.GetMenu();
                var button = new MenuItem("Addons", "Spawn addon vehicles and weapons you have access to.")
                {
                    Label = "→→→"
                };
                AddMenu(Menu, menu, button);
            }
```

- [ ] **Step 5: Build (compile check)** — Release build, expect `0 Error(s)`.

- [ ] **Step 6: Post-build hygiene + commit**

```bash
git checkout -- build/vMenu/config/permissions.cfg
git add vMenu/MainMenu.cs
git commit -m "feat(addons): create Addons menu, gate build on addon payload with timeout"
```

---

### Task 4: Client event wiring (receive payload, request it)

**Files:**
- Modify: `vMenu/EventManager.cs` (constructor `:39`, new `SetAddons` method, `SetConfigOptions` `:127`)

**Interfaces:**
- Consumes: `AddonsManager.Load(string)` (Task 1), `MainMenu.AddonsSetupComplete` (Task 3).
- Produces: handler for server event `vMenu:SetAddons`; client fires `vMenu:RequestAddons`.

- [ ] **Step 1: Register the receive handler** — in the `EventManager()` constructor, after the `vMenu:SetConfigOptions` line (`:39`), add:

```csharp
            EventHandlers.Add("vMenu:SetAddons", new Action<string>(SetAddons));
```

- [ ] **Step 2: Add the handler method** — place next to `SetConfigOptions`/`SetExtras` (after `SetConfigOptions`, around `:132`):

```csharp
        /// <summary>
        /// Receives the per-player addon payload (allowed vehicles/weapons + EUP flag) from the server.
        /// </summary>
        private void SetAddons(string json)
        {
            data.AddonsManager.Load(json);
            MainMenu.AddonsSetupComplete = true;
        }
```

- [ ] **Step 3: Request the payload from the existing config-ready signal** — in `SetConfigOptions` (`:127`), add the request after `SetExtras();`:

Find:
```csharp
        private void SetConfigOptions()
        {
            SetExtras();

            MainMenu.ConfigOptionsSetupComplete = true;
        }
```
Replace with:
```csharp
        private void SetConfigOptions()
        {
            SetExtras();

            // Ask the server for this player's permitted addons. Tied to the config-ready
            // signal so it also re-fires when vMenu restarts with players connected.
            TriggerServerEvent("vMenu:RequestAddons");

            MainMenu.ConfigOptionsSetupComplete = true;
        }
```

- [ ] **Step 4: Build (compile check)** — Release build, expect `0 Error(s)`.

- [ ] **Step 5: Post-build hygiene + commit**

```bash
git checkout -- build/vMenu/config/permissions.cfg
git add vMenu/EventManager.cs
git commit -m "feat(addons): receive vMenu:SetAddons, request addons on config-ready"
```

---

### Task 5: Config schema (`config_server.lua`)

**Files:**
- Modify: `build/vMenu/config/config_server.lua`

- [ ] **Step 1: Add the `Config.Addons` block**

Find:
```lua
Config = {
    EnableServerList = GetConvarBool("vmenu_disable_server_info_convars", false) == false, -- If you want to show the framework version in your server list info convars.
    DisableAI = GetConvarBool("vmenu_disable_ai", false), -- This will disable NPC spawning in bucket 0 (if you don't touch buckets, you don't need to worry about this). This is useful for people who want a quick and easy way to disable AI.
}
```
Replace with:
```lua
Config = {
    EnableServerList = GetConvarBool("vmenu_disable_server_info_convars", false) == false, -- If you want to show the framework version in your server list info convars.
    DisableAI = GetConvarBool("vmenu_disable_ai", false), -- This will disable NPC spawning in bucket 0 (if you don't touch buckets, you don't need to worry about this). This is useful for people who want a quick and easy way to disable AI.

    -- Runtime addon vehicles/weapons/EUP. Each item is gated by a FiveM ACE permission,
    -- evaluated server-side (see server/addons.lua). Add access in your server.cfg, e.g.:
    --   add_ace group.cops vMenu.Addons.vehicles.police3 allow
    --   add_ace group.cops vMenu.Addons.weapons.weapon_raypistol allow
    --   add_ace group.cops vMenu.Addons.eup allow
    Addons = {
        -- ACE auto-derived as vMenu.Addons.vehicles.<spawn> unless `permission` is set.
        vehicles = {
            { spawn = 'police3',   label = 'LSPD Cruiser' },
            { spawn = 'ambulance', label = 'Ambulance' },
            { spawn = 'adder',     label = 'VIP Adder', permission = 'vMenu.Addons.vip' },
        },
        -- ACE auto-derived as vMenu.Addons.weapons.<spawn> unless `permission` is set.
        weapons = {
            { spawn = 'weapon_raypistol',     label = 'Up-n-Atomizer' },
            { spawn = 'weapon_militaryrifle', label = 'Military Rifle' },
        },
        eup = {
            enabled    = true,                 -- false => no EUP slider gating at all
            permission = 'vMenu.Addons.eup',   -- single group ACE for the extended clothing ranges
        },
    },
}
```

- [ ] **Step 2: Sanity-check the Lua parses** (no build; eyeball balanced braces/commas). Verify against ox globals only if `luacheck` is available.

- [ ] **Step 3: Commit**

```bash
git add build/vMenu/config/config_server.lua
git commit -m "feat(addons): config_server.lua Config.Addons schema + examples"
```

---

### Task 6: Server ACE evaluator (`server/addons.lua`)

**Files:**
- Create: `build/vMenu/server/addons.lua` (auto-loaded by the `server/*.lua` glob in `fxmanifest.lua`)

**Interfaces:**
- Consumes: `Config.Addons` (Task 5); fires client event `vMenu:SetAddons` consumed by Task 4.
- Produces: handler for `vMenu:RequestAddons`.

**Notes:** `json` and `IsPlayerAceAllowed` are CitizenFX globals; `source` inside the event handler is the requesting player. The ACE check on the source mirrors the security note in `server/integrations.lua:8`.

- [ ] **Step 1: Create the file**

```lua
-- Sends each player the addon vehicles/weapons they are permitted to use, evaluated against
-- FiveM ACE permissions. Definitions live in config_server.lua (Config.Addons).
-- See docs/superpowers/specs/2026-06-23-runtime-addons-permissions-design.md

local function deriveAce(kind, entry)
    if type(entry.permission) == 'string' and entry.permission ~= '' then
        return entry.permission
    end
    return ('vMenu.Addons.%s.%s'):format(kind, tostring(entry.spawn):lower())
end

local function isValid(entry, isWeapon)
    if type(entry) ~= 'table' then return false end
    if type(entry.spawn) ~= 'string' or #entry.spawn == 0 or #entry.spawn > 64 then return false end
    if isWeapon and entry.spawn:sub(1, 7) ~= 'weapon_' then return false end
    return true
end

local function collectAllowed(src, kind, list)
    local out = {}
    if type(list) ~= 'table' then return out end
    local isWeapon = (kind == 'weapons')
    for _, entry in ipairs(list) do
        if isValid(entry, isWeapon) then
            if IsPlayerAceAllowed(src, deriveAce(kind, entry)) then
                out[#out + 1] = { spawn = entry.spawn, label = entry.label or entry.spawn }
            end
        else
            print(('^3[vMenu] [Addons] Skipping malformed %s entry in Config.Addons^7'):format(kind))
        end
    end
    return out
end

RegisterNetEvent('vMenu:RequestAddons', function()
    local src = source
    local cfg = (Config and Config.Addons) or {}

    local payload = {
        vehicles = collectAllowed(src, 'vehicles', cfg.vehicles),
        weapons  = collectAllowed(src, 'weapons',  cfg.weapons),
        eup      = { allowed = true },
    }

    local eup = cfg.eup
    if type(eup) == 'table' and eup.enabled then
        payload.eup.allowed = IsPlayerAceAllowed(src, eup.permission or 'vMenu.Addons.eup')
    end

    TriggerClientEvent('vMenu:SetAddons', src, json.encode(payload))
end)
```

- [ ] **Step 2: Eyeball** — confirm the file is picked up by `server_scripts { ... 'server/*.lua' }` in `build/vMenu/fxmanifest.lua` (no manifest edit needed; the glob already covers it).

- [ ] **Step 3: Commit**

```bash
git add build/vMenu/server/addons.lua
git commit -m "feat(addons): server ACE evaluator sends allowed-only addon payload"
```

---

### Task 7: EUP base-count table

**Files:**
- Create: `vMenu/data/EupBaseCounts.cs`

**Interfaces:**
- Produces: `vMenuClient.data.EupBaseCounts.For(uint model, int component)` → base drawable count, or `-1` if unknown (logged once per model).

**About the data:** GTA's `GetNumberOfPedDrawableVariations` returns base **+ streamed EUP** with no runtime split, so we cap to a base-count table. These counts are **measured from a server with the EUP resource disabled** (Step 2) — they are environment data, not guesses; an unknown model fails open (no cap). Component indices are 0–11 (3=Upper, 4=Lower, 6=Shoes, 8=Undershirt, 11=Tops are the usual EUP slots).

- [ ] **Step 1: Create the file with the table skeleton + lookup**

```csharp
using System.Collections.Generic;

using CitizenFX.Core;

using static CitizenFX.Core.Native.API;

namespace vMenuClient.data
{
    /// <summary>
    /// Base-game ped drawable variation counts per (model hash, component index), used to cap
    /// clothing sliders for players without the EUP permission. Values are captured from a server
    /// with EUP disabled (see plan Task 7). Unknown models return -1 (caller leaves sliders uncapped).
    /// </summary>
    public static class EupBaseCounts
    {
        private static readonly Dictionary<uint, Dictionary<int, int>> _base = new Dictionary<uint, Dictionary<int, int>>
        {
            {
                (uint)GetHashKey("mp_m_freemode_01"), new Dictionary<int, int>
                {
                    // component index -> base drawable count (CAPTURE in Step 2):
                    // { 1, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 }, { 6, 0 },
                    // { 7, 0 }, { 8, 0 }, { 9, 0 }, { 10, 0 }, { 11, 0 },
                }
            },
            {
                (uint)GetHashKey("mp_f_freemode_01"), new Dictionary<int, int>
                {
                    // { 1, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 }, { 6, 0 },
                    // { 7, 0 }, { 8, 0 }, { 9, 0 }, { 10, 0 }, { 11, 0 },
                }
            },
        };

        private static readonly HashSet<uint> _warned = new HashSet<uint>();

        public static int For(uint model, int component)
        {
            if (_base.TryGetValue(model, out var comps) && comps.TryGetValue(component, out var count))
            {
                return count;
            }
            if (_warned.Add(model))
            {
                Debug.WriteLine($"[vMenu] [Addons] No EUP base-count entry for ped model {model} / component {component}; leaving its sliders uncapped (fail-open).");
            }
            return -1;
        }
    }
}
```

- [ ] **Step 2: Capture the base counts** — on a dev server **without** your EUP resource started, as a freemode ped, temporarily add this client command (e.g. paste into `client/exports.lua`), run `/eupdump` in F8/chat, copy the printed lines into the dictionaries above, then remove the command:

```lua
RegisterCommand('eupdump', function()
    local ped = PlayerPedId()
    local model = GetEntityModel(ped)
    local out = ('EUP base counts for model %s:'):format(model)
    for comp = 0, 11 do
        out = out .. ('\n  { %d, %d },'):format(comp, GetNumberOfPedDrawableVariations(ped, comp))
    end
    print(out)
end, false)
```

Paste the `{ comp, count }` pairs for `mp_m_freemode_01` and `mp_f_freemode_01` into the matching dictionaries. (If you don't run EUP for one sex, leave that dictionary empty — it fails open.)

- [ ] **Step 3: Build (compile check)** — Release build, expect `0 Error(s)`.

- [ ] **Step 4: Post-build hygiene + commit**

```bash
git checkout -- build/vMenu/config/permissions.cfg
git add vMenu/data/EupBaseCounts.cs
git commit -m "feat(addons): EUP base-count table for clothing-slider gating"
```

---

### Task 8: EUP cap — Player Appearance sliders

**Files:**
- Modify: `vMenu/menus/PlayerAppearance.cs` (drawable loop, `:756-759`)

**Interfaces:**
- Consumes: `AddonsManager.Eup.allowed` (Task 1), `EupBaseCounts.For(uint,int)` (Task 7).

- [ ] **Step 1: Insert the cap before the list is built**

Find:
```csharp
                var maxTextures = GetNumberOfPedTextureVariations(Game.PlayerPed.Handle, drawable, currentDrawable);

                if (maxVariations > 0)
```
Replace with:
```csharp
                var maxTextures = GetNumberOfPedTextureVariations(Game.PlayerPed.Handle, drawable, currentDrawable);

                // EUP gate: players without the EUP permission only see base-game drawables.
                if (!data.AddonsManager.Eup.allowed)
                {
                    var eupBase = data.EupBaseCounts.For((uint)GetEntityModel(Game.PlayerPed.Handle), drawable);
                    if (eupBase >= 0 && maxVariations > eupBase)
                    {
                        maxVariations = eupBase;
                        if (currentDrawable >= maxVariations)
                        {
                            currentDrawable = 0;
                        }
                    }
                }

                if (maxVariations > 0)
```

- [ ] **Step 2: Build (compile check)** — Release build, expect `0 Error(s)`.

- [ ] **Step 3: Post-build hygiene + commit**

```bash
git checkout -- build/vMenu/config/permissions.cfg
git add vMenu/menus/PlayerAppearance.cs
git commit -m "feat(addons): cap Player Appearance clothing sliders without EUP permission"
```

---

### Task 9: EUP cap — MP Ped Customization sliders

**Files:**
- Modify: `vMenu/menus/MpPedCustomization.cs` (clothing loop, `:572-574`)

**Interfaces:**
- Consumes: `AddonsManager.Eup.allowed` (Task 1), `EupBaseCounts.For(uint,int)` (Task 7).

- [ ] **Step 1: Insert the cap right after the drawable count is read**

Find:
```csharp
                    var maxDrawables = GetNumberOfPedDrawableVariations(Game.PlayerPed.Handle, i);

                    var items = new List<string>();
```
Replace with:
```csharp
                    var maxDrawables = GetNumberOfPedDrawableVariations(Game.PlayerPed.Handle, i);

                    // EUP gate: players without the EUP permission only see base-game drawables.
                    if (!data.AddonsManager.Eup.allowed)
                    {
                        var eupBase = data.EupBaseCounts.For((uint)GetEntityModel(Game.PlayerPed.Handle), i);
                        if (eupBase >= 0 && maxDrawables > eupBase)
                        {
                            maxDrawables = eupBase;
                            if (currentVariationIndex >= maxDrawables)
                            {
                                currentVariationIndex = 0;
                            }
                        }
                    }

                    var items = new List<string>();
```

- [ ] **Step 2: Build (compile check)** — Release build, expect `0 Error(s)`.

- [ ] **Step 3: Post-build hygiene + commit**

```bash
git checkout -- build/vMenu/config/permissions.cfg
git add vMenu/menus/MpPedCustomization.cs
git commit -m "feat(addons): cap MP ped clothing sliders without EUP permission"
```

---

### Task 10: End-to-end in-game verification

**Files:** none (manual smoke test — this is the integration test for the whole feature).

**Setup:** Copy `build/vMenu/` to a dev FiveM server. In `config_server.lua` keep the example `Config.Addons`. In `server.cfg`, grant a test principal only **some** items, e.g.:
```
add_ace identifier.fivem:<yourid> vMenu.Addons.vehicles.police3 allow
add_ace identifier.fivem:<yourid> vMenu.Addons.weapons.weapon_raypistol allow
add_ace identifier.fivem:<yourid> vMenu.Addons.eup allow
# deliberately NOT granting: ambulance, weapon_militaryrifle, vMenu.Addons.vip (adder)
```

- [ ] **Step 1: Allowed-items visibility** — join, open vMenu → **Addons** menu exists; **Addon Vehicles** lists *LSPD Cruiser* only (not Ambulance/Adder); **Addon Weapons** lists *Up-n-Atomizer* only.
- [ ] **Step 2: Spawn/give works** — select LSPD Cruiser → it spawns (and you're inside, "Spawn Inside" checked). Select Up-n-Atomizer → weapon given with ammo.
- [ ] **Step 3: Denial** — revoke all three ACEs for your principal, restart the server, rejoin → the **Addons** menu is absent entirely (no allowed items).
- [ ] **Step 4: EUP cap** — with `vMenu.Addons.eup` **denied**, open Player Appearance (and MP Ped creator) → clothing drawable sliders for table-known components stop at the base count (EUP items hidden). Grant `vMenu.Addons.eup`, rejoin → full ranges return. (Peds absent from the table stay uncapped; check the server console for the one-time fail-open log line.)
- [ ] **Step 5: Resource restart** — with items granted and a player connected, `restart vMenu` (or `ensure`) → the Addons menu repopulates (client re-requests on the re-fired config signal).
- [ ] **Step 6: Malformed config** — add a bad entry (`{ spawn = '' }`) to `Config.Addons.vehicles`, restart → server console logs "Skipping malformed vehicles entry", the rest of the list still loads, no crash.
- [ ] **Step 7: Final** — confirm `git status` shows no `permissions.cfg`/stackdump churn; the feature branch holds one commit per task.

---

## Self-Review

**1. Spec coverage:**
- Per-item ACE (vehicles/weapons) → Tasks 5, 6 (`deriveAce`, auto + override). ✓
- Group-ACE EUP exposing clothing ranges → Tasks 6 (eup flag), 7–9 (cap). ✓
- Compiled base table, fail-open + log → Task 7. ✓
- C# menu → Tasks 2, 3. ✓
- Config in `config_server.lua` → Task 5. ✓
- ACE format, server-evaluated → Task 6. ✓
- Approach A request/response from `SetConfigOptions`; menu-build gate → Tasks 3, 4. ✓
- Send-only-allowed → Task 6 (`collectAllowed` filters). ✓
- Backward-compat (addons.json untouched) → no task modifies `ValidWeapon.cs`/`VehicleData.cs`/`addons.json`. ✓
- Error handling (Lua skip/log, client try/catch, gate timeout) → Tasks 1, 3, 6. ✓
- Testing → Task 10. ✓

**2. Placeholder scan:** Task 7's base-count *values* are captured via a fully-specified dump procedure (environment-specific data, defined fallback), not a hand-wave. No `TBD`/`TODO`/"handle edge cases" left.

**3. Type consistency:** `AddonsManager.{Vehicles,Weapons,Eup,HasAny,Load}`, `AddonVehicle.{spawn,label}`, `AddonWeapon.{spawn,label}`, `EupState.allowed`, `EupBaseCounts.For(uint,int)`, `MainMenu.{AddonsMenu,AddonsSetupComplete}`, events `vMenu:RequestAddons`/`vMenu:SetAddons`, and the lower-case JSON keys (`vehicles/weapons/eup/spawn/label/allowed`) are used identically across the C# DTO (Task 1), the menu (Task 2), the wiring (Tasks 3–4), the Lua payload (Task 6), and the EUP caps (Tasks 7–9). ✓
