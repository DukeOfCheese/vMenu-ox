# Migrating to vMenu-ox 3.1.0

This guide takes a server running vMenu-ox 3.0.x to 3.1.0 and then checks that everything works. Allow about 20 minutes for the migration and 30–45 minutes for the checks. You need two game accounts for some of the checks (an admin and a test player).

**What doesn't need migrating:**
- Players' saved vehicles, peds, MP characters, loadouts and settings are stored on their own PCs and load as they are.
- Bans and saved teleport locations are stored by the server under the resource name `vMenu`, so they carry over as long as the folder keeps that name.

For what changed and why, see [REVIEW.md](REVIEW.md).

---

## Part 1: Migrate

### 1. Check what you need

| Requirement | Notes |
|---|---|
| [ox_lib](https://github.com/overextended/ox_lib/releases/latest) | Same as before. Must start before vMenu. |
| [oxmysql](https://github.com/overextended/oxmysql/releases) | Only if you use outfit, vehicle or loadout share codes. |
| An up-to-date FXServer artifact | The menus are now drawn with MenuAPI 4 (an NUI page). Update the server artifacts if they're old. |
| Resource folder named exactly `vMenu` | Unchanged. Any other name prints an "INSTALLATION ERROR" and vMenu won't start. |

### 2. Get the 3.1.0 files

- **Server owners:** download `vMenu.zip` for 3.1.0 from the [releases page](https://github.com/DukeOfCheese/vMenu-ox/releases/).
- **Maintainers building it yourself:** the compiled DLLs aren't in the repository, so build them on Windows with Visual Studio 2022:
  ```
  "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" vMenu.sln -t:restore
  "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" vMenu.sln -p:Configuration=Release
  ```
  The finished resource is `build/vMenu/`. It must contain `vMenuClient.net.dll`, `vMenuServer.net.dll`, `MenuAPI.dll`, `index.html` and the `menuapi/` folder.

### 3. Back up

Stop the server (or run `stop vMenu`), then copy these somewhere safe:

- The whole `resources/[...]/vMenu/` folder.
- Your `permissions.cfg`, wherever you keep it, if it isn't inside the vMenu folder.
- Your database, if you use share codes. A dump of the `vmenu_outfits`, `vmenu_vehicles` and `vmenu_loadouts` tables is enough.

### 4. Replace the folder completely

**Delete the old `vMenu` folder's contents, then extract the new files.** Do not copy the new files over the old ones.

Two old files must not survive:
- `server/oufitCodes.lua`, renamed to `server/outfitCodes.lua`. If the old one stays, both load and register the same callbacks.
- `storage.html`, removed. The resource's NUI page is now `index.html`.

The new version also adds these, which must be present:
- `index.html`
- `menuapi/`, `themes/` and `menuapi-banners/`
- `client/lib/codes.lua`
- `server/lib/codes.lua`

### 5. Re-apply your configuration

Start from the **new** files and copy your settings into them. Don't copy your old files back over the new ones, because several of them changed.

| File | What to do |
|---|---|
| `config/permissions.cfg` | Copy your `add_ace` / `add_principal` lines and changed `setr` values into the new file. **Or** keep yours and add the new lines listed in [New settings](#new-settings-to-add-to-your-permissionscfg). The sample `add_principal` lines in the default file are now commented out; they were never meant for your server. |
| `config/config_server.lua` | Copy your `Config.Addons` (vehicles, weapons, peds, engineSounds, eup) and `Config.Extras` into the new file. The new keys `ModelWhitelist`, `Addons.tattoos` and `Addons.extraBlendableFaces` are optional and empty by default. |
| `config/locations.lua` | Unchanged. You can copy your old file back. |
| `config/config_client.lua` | Copy your `Config.Notify` and command settings into the new file. The only change is a guard against empty messages. |
| `client/integrations.lua` | Copy your custom code into the new file. There is a new interaction type, `"loadoutfit"`, for wearing an outfit from a code. |
| `server/integrations.lua` | **Do not restore your old copy.** The ban relays in it called the old internal events, which are the critical security hole this version closes. If you customised the overhead name format, re-apply that change inside `buildOverheadName` in the new file. That function can now also return a `crewTag` (shown next to the name, e.g. a rank), which is `""` (no tag) by default. |

#### New settings to add to your permissions.cfg

Only needed if you keep your old `permissions.cfg`. The new file already has these.

```cfg
# Menu theme: "default" or "custom" (dark theme). Applies after a client or menu restart.
setr vmenu_theme "default"

# How far in front of the player (metres, 1-25) the vMenu:DV command looks for a vehicle to delete.
setr vmenu_delete_vehicle_distance 5.0

# Actions aimed at other players (kick, kill, summon, teleport/spectate/GPS, private messages):
# at most this many per player in this many seconds. 0 turns the limit off.
setr vmenu_online_players_action_limit 8
setr vmenu_online_players_action_limit_seconds 10

# Run the clock forward to a new time instead of jumping straight to it.
setr vmenu_smooth_time_transitions false
```

New optional permissions:

```cfg
# Skip the vehicle spawn cooldown. VehicleSpawner.All does NOT grant this.
#add_ace group.admin "vMenu.VehicleSpawner.BypassRateLimit" allow

# Access to models listed in Config.ModelWhitelist (vehicles / peds / weapons)
#add_ace group.police "vMenu.VehicleSpawner.WhitelistedModels.rhino" allow
#add_ace group.admin  "vMenu.VehicleSpawner.WhitelistedModels.All" allow
#add_ace group.admin  "vMenu.PlayerAppearance.WhitelistedModels.All" allow
#add_ace group.admin  "vMenu.WeaponOptions.WhitelistedModels.All" allow   # weapon names without "weapon_"
```

Permission behaviour that changed:
- **Permanent bans** now need `vMenu.OnlinePlayers.PermBan`; before, `TempBan` was enough. Check that the staff who should permanently ban have it.
- **Ban list IPs:** only staff with `vMenu.OnlinePlayers.Unban` see IP addresses in the banned players list.
- **Server-side checks:** the server now treats `vMenu.Everything` and a menu's `.All` permission as granting its children, as the client always did. So `vmenu_use_permissions false` and `vmenu_menu_staff_only true` no longer cause false cheater bans.

### 6. Update the database (share codes only)

Skip this if `vmenu_outfit_codes`, `vmenu_vehicle_codes` and `vmenu_loadout_codes` are all `false`.

- **New install:** run `database.sql` as before.
- **Upgrading:** run these once, as in the comment at the top of `database.sql`. An `ADD INDEX` that fails with *Duplicate key name 'idx_discord'* just means that table already has the index.

```sql
ALTER TABLE `vmenu_outfits`  MODIFY `data` mediumtext DEFAULT NULL;
ALTER TABLE `vmenu_vehicles` MODIFY `data` mediumtext DEFAULT NULL;
ALTER TABLE `vmenu_loadouts` MODIFY `data` mediumtext DEFAULT NULL;
ALTER TABLE `vmenu_outfits`  ADD INDEX `idx_discord` (`discord_id`);
ALTER TABLE `vmenu_vehicles` ADD INDEX `idx_discord` (`discord_id`);
ALTER TABLE `vmenu_loadouts` ADD INDEX `idx_discord` (`discord_id`);
```

Codes made with older versions keep working.

New share-code limits:
- **Daily cap:** each Discord account can create 25 codes per type per day.
- **Setting off means fully off:** a code type whose setting is off no longer answers at all. Before, it still answered requests.

### 7. Check server.cfg order

```cfg
ensure ox_lib
ensure oxmysql      # only if you use share codes
exec @vMenu/config/permissions.cfg   # or wherever your permissions.cfg lives
ensure vMenu
```

### 8. Check other resources that talk to vMenu

| If another resource… | Then |
|---|---|
| triggered `vMenu:Internal:TempBanPlayer`, `PermBanPlayer`, `RequestedPlayerUnban`, `RequestBanList`, `playerConnecting` or `LoadConfig` | Those events no longer exist. Use the normal client events (`vMenu:TempBanPlayer`, `vMenu:PermBanPlayer`, `vMenu:RequestPlayerUnban`, `vMenu:RequestBanList`). On the server, `exports.vMenu:TempBanPlayer(adminId, targetId, hours, reason)` and `exports.vMenu:PermBanPlayer(adminId, targetId, reason)` work; vMenu still checks the admin's ACE. |
| listens to `vMenu:SyncOverheadNames` | It now gets a second argument, `isDelta`. When it's `true`, the table holds only changed entries, and `false` as a value means that player left. Each entry may also carry a `crewTag` string next to `name` and `colour`. |
| calls `loadSharedOutfit`, `loadOutfitFromCode`, `loadSharedVehicle`, `loadSharedLoadout` or `loadLoadoutFromCode` | Same names and arguments. Each one returns `false` and does nothing if its code type's setting is off. |
| uses `canDoInteraction` in `client/integrations.lua` | Add a `"loadoutfit"` branch if you want to block wearing outfits from codes in some situations. |

### 9. Start the server

Run `ensure vMenu` (or restart the server) and go to Part 2.

---

## Part 2: Verify

Tick each check as you go. If something fails, see [If something fails](#if-something-fails).

### A. Startup console (about 1 minute)

After vMenu starts, check:

- [ ] **No red errors** from vMenu or ox_lib.
- [ ] **No addons timeout.** This line must not appear: `[Addons] No config arrived from server/addons.lua within 15s`. If it does, Lua → C# exports aren't working (the first of the three runtime checks).
- [ ] **No config hand-over warning.** This line must not appear: `Could not hand the config to the C# script yet`.
- [ ] **Share-code line per enabled type,** if share codes are on: `Share codes enabled: outfit.` / `vehicle.` / `loadout.`.
- [ ] **No "No such export" errors.**

For more detail, set `server_debug_mode 'true'` in `fxmanifest.lua` for this test session. You should then see a line like `[Addons] Config loaded: N vehicles, … teleports, … blips.` Turn it back off afterwards.

### B. The three runtime checks (most important)

These cover the three things the build and Lua checks could not prove.

**1. Lua can call vMenu's C# exports, and bans work.** As admin, with a test player online:
- [ ] Online Players → test player → **Temp Ban** for 1 hour. The test player is dropped with the ban message, and you get "Target player successfully banned."
- [ ] Banned Players menu → the ban is listed. Use **Unban**.
- [ ] Repeat with **Perm Ban**. The admin needs `vMenu.OnlinePlayers.PermBan`.

*If it fails:* nothing happens, or the server console shows `ArgumentException` or `No such export TempBanPlayer`. Roll back (see [If something fails](#if-something-fails)) and report it.

**2. Ban check when connecting.**
- [ ] While the test player is banned, they try to reconnect. They see "Checking ban status...", then the ban message, and can't join.
- [ ] After the unban, they can join normally.

**3. The old internal events are dead (the security fix).** Create a throwaway resource on your **test** server only:

`vmenu-sectest/fxmanifest.lua`
```lua
fx_version 'cerulean'
game 'gta5'
client_script 'client.lua'
```
`vmenu-sectest/client.lua`
```lua
-- Run as a player with NO vMenu staff permissions: /vmenusectest <adminServerId> <testTargetServerId>
RegisterCommand('vmenusectest', function(_, args)
    local admin, target = tonumber(args[1]), tonumber(args[2])
    TriggerServerEvent('vMenu:Internal:PermBanPlayer', admin, target, 'security test')
    TriggerServerEvent('vMenu:Internal:RequestedPlayerUnban', admin, 'not-a-real-id')
    TriggerServerEvent('vMenu:Internal:LoadConfig', '{}')
    print('sent - nothing should happen')
end)
```
- [ ] `ensure vmenu-sectest`, then as the **non-staff** test player run `/vmenusectest <adminId> <anotherPlayerId>`. **Nobody gets banned**, and vMenu logs no ban.
- [ ] Addons are still in the menus. Reconnect and check that an addon vehicle or weapon is still listed. If the config had been replaced they would be gone.
- [ ] **Delete the `vmenu-sectest` resource afterwards.**

### C. Feature checks

| # | Check | Expected |
|---|---|---|
| 4 | Open the menu (M or your key) and browse a few submenus | Menus draw normally, with the usual banner; arrows, Enter and Backspace work |
| 5 | `setr vmenu_theme "custom"`, then reconnect | Dark theme with the custom header image. Set it back if you don't want it. |
| 6 | Hotkeys: Space in a searchable list; Space in Banned Players; Delete in Saved Vehicles → unavailable vehicles; Space in Vehicle Options → Mod Menu | Filter prompt; ban filter; delete; all doors toggle |
| 7 | Any ox_lib input (e.g. spawn vehicle by name) with the menu open | The dialog appears on top and takes keyboard focus |
| 8 | Time Options: freeze time, then set a different time | Time changes **and stays frozen** (this used to unfreeze) |
| 9 | Weather Options: change weather type, blackout and dynamic weather | Each works for staff with the matching permission |
| 10 | Noclip on, Shift / Ctrl to change speed, then off | Speed label updates; Ctrl lowers speed |
| 11 | God mode (player and vehicle) on, then noclip on and off | God mode still active afterwards |
| 12 | MP Ped Customization: load an existing saved character | It looks exactly as before |
| 13 | Clothing list in MP Ped or Player Appearance: hold **Left Ctrl + Enter** | An ID prompt opens; typing an ID beyond the list (or an EUP item without permission) is refused |
| 14 | Inheritance menu | Parent #1, Parent #1 Skin, Parent #2, Parent #2 Skin; existing characters unchanged |
| 15 | Tattoos menu | Hair tattoos row; "Addon Tattoos" only if `Config.Addons.tattoos` is set |
| 16 | Vehicle Options → Colours: change Paint Finish | Finish changes, colour stays; Classic and Metallic look right |
| 17 | Mod Menu → Headlights / Tire Smoke → Custom RGB, then save the vehicle, delete it and spawn it from Saved Vehicles | Colours and paint finish come back |
| 18 | Change the wheel type, then open Mod Menu → Headlights / Tire Smoke again | Both still open |
| 19 | Vehicle Options → Default Radio: enable it, pick a station, spawn a car | Car starts on that station; disabled means the game default |
| 20 | `vMenu:DV` in F8 while driving / in front of an empty car / in front of an occupied car | Deletes yours / deletes the empty one / refuses the occupied one |
| 21 | Spawn vehicles quickly | Cooldown applies, except for players with `BypassRateLimit` |
| 22 | Add a model to `Config.ModelWhitelist` (e.g. `vehicles = { 'rhino' }`), restart vMenu | Locked for a normal player; usable with `vMenu.VehicleSpawner.WhitelistedModels.rhino` |
| 23 | Weapon Options → Spawn Weapon By Name with an addon weapon the player isn't allowed | "not available to you" |
| 24 | Weapon Loadouts: spawn a saved loadout containing a weapon the player isn't allowed | That weapon is skipped with a notice; the rest arrive with full ammo |
| 25 | Die with weapons, then respawn (with weapon restore on) | Weapons come back, refilled to full ammo |
| 26 | Online Players: GPS route / spectate / teleport to a player | Works; after more than 8 actions in 10 s you're told to try again in a few seconds |
| 27 | Personal Vehicle → Kick Passengers with someone in your car | They're removed, even if they resist exiting |
| 28 | Misc Settings → overhead names on, with a second player | Names show at the configured distance; when the player leaves their tag disappears |
| 29 | Send a private message to a player who disabled PMs | The message names **that player** |

### D. Share codes (only if enabled)

| # | Check | Expected |
|---|---|---|
| 30 | Saved Vehicles → a vehicle → Generate Code | Code shown **and copied to clipboard** |
| 31 | Saved Vehicles → load a vehicle code, with a name | Vehicle appears under Uncategorized |
| 32 | Weapon Loadouts → Generate Code, then load the code on a player **without** permission for one of its weapons | That weapon is not given or saved |
| 33 | Spawn an MP character, generate an outfit code from another saved character, then load the outfit code | New character saved under the new name, with your face and the shared clothes |
| 34 | Load an outfit code with **no** MP character spawned | "Spawn the MP character you want this outfit on first" |
| 35 | Load a code made before the upgrade | Still loads |
| 36 | Set one `vmenu_*_codes` to `false` and restart | That type's code options are hidden or report that codes are disabled; the other types still work |

### E. Performance (optional but worth it)

Run `resmon 1` in F8 before and after the upgrade, standing still, in a vehicle with vehicle god mode on, and with player god mode on. vMenu's client time should be the same or lower. The biggest gains are with vehicle and player options enabled, and on busy servers (joins, overhead names, death notices).

### F. Watch for a day

- **Server console:** look for `[vMenu] [WARNING]` lines (rate-limit notices, rejected share codes) and any `[ERROR]`.
- **`vmenu.log`**, if ban or kick logging is on: new entries are appended and older ones are still there.

---

## If something fails

**Roll back:** stop vMenu, put the backed-up folder (and `permissions.cfg`) back, and start it. You don't need to undo the database changes: older versions work with the larger column and the index.

| Symptom | Likely cause | Fix |
|---|---|---|
| `INSTALLATION ERROR … resource name` | Folder isn't named `vMenu` | Rename it |
| Menus don't appear at all | `index.html` or `menuapi/` missing, or an old `fxmanifest.lua` | Replace the folder completely (Part 1, step 4) |
| `[Addons] No config arrived … within 15s` | `config_server.lua` / `locations.lua` errors, or Lua can't reach the C# export | Fix the Lua error shown just before it; if there is none, report it (runtime check 1) |
| Bans do nothing / `ArgumentException` / `No such export` | Runtime check 1 or 2 failed | Roll back and report |
| Banned players can still join | Runtime check 3 failed, or another resource also handles `playerConnecting` | Check for errors after "Checking ban status..."; report |
| Share-code options missing or "disabled" | The `vmenu_*_codes` setting is `false` | Set it to `true`, restart |
| Share codes: "The server could not … right now" | oxmysql not started, or tables missing | `ensure oxmysql` before vMenu; run `database.sql` |
| Staff get auto-banned for normal actions | Missing permission | Check the ACE for that action. Permanent bans now need `vMenu.OnlinePlayers.PermBan`. If it persists, set `vmenu_auto_ban_cheaters false` and report it. |
| Both an old and a new code file in `server/` | Files copied over the old folder | Delete `server/oufitCodes.lua` |
