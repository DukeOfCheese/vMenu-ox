# vMenu-OX 

Many thanks to @Gravxd for the original repository and to-do list which will be continued in this repository as his has since been archived

--------

This is a fork of vMenu designed to integrate with [ox_lib](https://github.com/overextended/ox_lib/releases/latest) you will require it for this to work.

This fork introduces some new exports that work with ox_lib for better UI features and to make the menu more convenient/easier to use for players.

Here's an example where we can move away from 3 separate input popups for rgb number values and to ox_lib's native inputDialog where the user can easily select a custom colour and visually see what they're choosing.

![Medal_ErnwOZVBn9](https://github.com/user-attachments/assets/a83d965e-05f0-4125-9e9c-65d7f03c0fd0)

---

### Download & Install
Click [here](https://github.com/DukeOfCheese/vMenu-ox/releases/) to go to the releases page and download `vMenu.zip` - this will be your fivem resource folder.

--------

### Support/Issues with my Fork
Support for this repository going forward can be found below!
[Atlas Development](https://discord.atlasdevops.com)

# Fork Features/Changes

### Core/UI

- Input Dialog Replace
  - Replaces the base game user input and replaces with ox_lib input dialog for easier use such as copy/pasting spawncodes etc.
- User Confirmations
  - Important actions like weather/time changes have user confirmation buttons preventing unwanted mishaps with a misclick.
![vmenuoxconfirmbuttons](https://github.com/user-attachments/assets/a1b53f95-f505-4ee2-9114-7082a5865685)

## Code Share System
For code sharing to function, you will require [oxmysql](https://github.com/overextended/oxmysql/releases)<br>
If you have oxmysql, you can enable the outfit/vehicle sharing systems in the `permissions.cfg`

This is a custom sharing system I designed for vMenu to follow similar behaviour i've seen in some clothing creators on economy servers.
This allows for super easy & simple sharing MP ped configurations between players and eventually vehicles is the plan.

Players create unique codes for a saved MP Ped and can give that code out where others can then load said outfit keeping their unique characteristics like hair, tattoos etc but getting clothing & prop options allowing super easy sharing!

_Say goodbye to huge spreadsheets with different numbers and say hello to simple one code input for your roleplay servers!_

### Vehicle

- Colour Selector
  - Gives users a hex selector for custom colour setting within vehicle options (primary/secondary)
- New Permissions
  - Bulletproof Tires `vMenu.VehicleOptions.BulletproofTires` (default: denied)
- Fixed getting disarmed (weapon taken away) when locking/unlocking personal vehicles
- Implemented cooldown between usage of close all doors to patch exploit to make cars float/fly
- Configurable cooldown when spawning vehicles to prevent players from spam spawning vehicles
  - `setr vmenu_vehicle_spawner_cooldown 1000`

### Weather

- Added convar `vmenu_blackout_affect_vehicles` (default: false) so that vehicle headlights/police lightbars continue to operate during blackouts

### Misc

- Keybinds for Thermal & Night Vision Modes
  - This is locked to users that have the permissions assigned to them.
    <img src="https://github.com/user-attachments/assets/d960116b-3540-485b-ad7a-ecaa1fdd42e4" width="500"><br>
- Patched vulnerability on weather events that could easily be exploited - thanks to [this pull](https://github.com/TomGrobbe/vMenu/pull/430/) that isn't merged as of 7th Jan 25.
- Disable AI with ease, with a simple convar in your permissions.cfg - `set vmenu_disable_ai true` (false by default)

### Devtools

- Auto freeze entites created with the entity spawner menu (to avoid them falling through map automatically on contact)
- Copy Coordinates Button (vector4)
- Copy Vehicle Model Hash

# Developer Integrations

Below is information related to exposed events/functions you can use in your resources to integrate your server better with vMenu.
In the FiveM resource, head to the `client` & `server` folders and you will see files labelled `integrations`.

Here you will find any events/exports for use and you can implement your server specific needs.

**Example Client Event:**
```lua
---@class logAction
---@field action string
---@field data table
AddEventHandler("vMenu:Integrations:Action", function(action, data)
    if action == "infinitefuel" then
        ---@class data table
        ---@field enabled boolean
        lib.print.debug("Infinite Fuel: " .. tostring(data.enabled))
    elseif action == "licenseplate" then
        ---@class data table
        ---@field handle integer
        ---@field plate string
        lib.print.debug("License Plate Updated: " .. data.handle .. " - " .. data.plate)
        --[[
            Example Usage:
            if doesTextContainBlacklistedWord(plate) then
                SetVehicleNumberPlateText(handle, "PLATE")
                TriggerServerEvent("banplayer")
            end
        --]]
    elseif action == "noclip" then
        ---@class data table
        ---@field enabled boolean
        lib.print.debug("NoClip: " .. tostring(data.enabled)) 
    end
end)
```

We also built a handy export for developers to block simple actions like spawning vehicles in restricted areas (jail, whilst dead etc)

```lua
---@field type string
exports("canDoInteraction", function(action)
    if action == "spawnvehicle" then
      if exports.core.isJailed() then return false end -- would block the user from spawning vehicles in jail.
    end
    return true
end)
```

### To-Do / Suggested Ideas
- [x] Alphabetically sort weapons in categories
- [x] Implement ids into notifications to cleanup / stop duplicate spammy notifications
- [x] ~~Sync Time/Weather into GlobalStates~~ Add export for time / weather
- [x] Add new event for ban manager so that developers can easily integrate their own anticheat/banning functions for event exploiters
- [x] Ratelimit on close all/open all doors (exploit to make cars fly)
- [x] Configurable vehicle spawn cooldown
- [x] ~~Take weapon spawning functionality out of c# and add export for LUA so that devs can easily integrate ox_inventory~~
- [ ] Export to add weapons + attachments into vmenu categories without them having to rebuild [REMOVE ADDON WEAPON SUBMENU / CODE] (maybe this gets extended to peds/vehicles?)
- [x] Add a export before weapon/vehicle spawning/teleports such as isRestrained() so developers can easily block actions and add their own cuff/death scripts etc
- [x] ~~Separate branch (maybe?) for outfit/weapon/vehicle code system~~ Planned to go ahead in main fork and add dependency of oxmysql as most servers use it. Maybe ill just do a resource check so if the resource isnt installed the buttons just error and say plugin not installed or smth?
- [x] Update weapon attachment right button if it is equipped (checkmark)
- [x] Copy Coords Button (devtools)
- [x] Ability to save BP tires on vehicles? (would need to perm check on re-apply)
- [x] Add an event that is triggered when infinite fuel is enabled so developers can easily integrate with scripts other than FRFUEL
- [ ] Modify weapon stats (both on weapon spawn and dynamically)
- [ ] Ability to create weapon loadout codes
- [ ] Update Voice Chat menu to use pma-voice instead
- [ ] Searchable menus (incl. convar to enable / disable)
- [ ] Convert show player names to LUA function to allow for easier developer integration
- [ ] Fix non-custom notifications
- [ ] Add default permissions.cfg with setting additions

--------

### Below is the information for the source project, all credit to the creation goes to Vespura, thank you to him for making an easy to use open source project for everyone. If you have an issue with a feature of this work or the ox_lib/dev integrations of `THIS FORK`, please use my discord as they will not be able to provide support for you. In accordance to the license, this is released as a fork with proper credit as well as a link to the original repository.

--------

# vMenu (Original)
vMenu is a server-side menu for FiveM servers created by Vespura - find the original repository [here](https://github.com/TomGrobbe/vMenu)

--------

### Original Repository License
Tom Grobbe - https://www.vespura.com/
Copyright © 2017-2025

You can use and edit this code to your liking as long as you don't ever claim it to be your code and always provide proper credit.
You're **not** allowed to sell vMenu or any code you take from it.
If you want to release your version of vMenu, you have to link the original GitHub repo or release it via a Forked repo.
