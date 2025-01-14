# vMenu-OX 
This is a fork of vMenu designed to integrate with [ox_lib](https://github.com/overextended/ox_lib/releases/latest) you will require it for this to work.

This fork introduces some new exports that work with ox_lib for better UI features and to make the menu more convenient/easier to use for players.

Here's an example where we can move away from 3 separate input popups for rgb number values and to ox_lib's native inputDialog where the user can easily select a custom colour and visually see what they're choosing.

![Medal_ErnwOZVBn9](https://github.com/user-attachments/assets/a83d965e-05f0-4125-9e9c-65d7f03c0fd0)


<img src="https://github.com/user-attachments/assets/d960116b-3540-485b-ad7a-ecaa1fdd42e4" width="500"><br>

--------

### Download & Install
Click [here](https://github.com/Gravxd/vMenu-ox/releases/) to go to the releases page and download `vMenu.zip` - this will be your fivem resource folder.

--------

### Support/Issues with my Fork
If you require support or have an issue / suggestion, feel free to join my discord!
I have a direct suggestions channel so you can give ideas and request stuff to be added.
[discord.gg/chromalabs](https://discord.gg/chromalabs)

# Fork Features/Changes

### Core/UI
- Input Dialog Replace
  - Replaces the base game user input and replaces with ox_lib input dialog for easier use such as copy/pasting spawncodes etc.

### Vehicle
- Colour Selector
  - Gives users a hex selector for custom colour setting within vehicle options (primary/secondary)
- New Permissions
  - Bulletproof Tires `vMenu.VehicleOptions.BulletproofTires` (default: denied)
- Fixed getting disarmed (weapon taken away) when locking/unlocking personal vehicles

### Weather
- Added convar `vmenu_blackout_affect_vehicles` (default: false) so that vehicle headlights/police lightbars continue to operate during blackouts

### Misc
- Keybinds for Thermal & Night Vision Modes
  - This is locked to users that have the permissions assigned to them.
- Patched vulnerability on weather events that could easily be exploited - thanks to [this pull](https://github.com/TomGrobbe/vMenu/pull/430/) that isn't merged still as of 7th jan

### Devtools
- Auto freeze entites created with the entity spawner menu (to avoid them falling through map automatically on contact)
- Copy Coordinates Button (vector4)
- Copy Vehicle Model Hash

## Changelog
- 13th Jan
  - Copy Coords (DevTools)
  - Version Checking
  - Infinite Fuel Event Trigger
  - Updated Integrations File (external folder in fivem resource)
- 14th Jan
  - Copy Vehicle Hash (DevTools)

# Developer Integrations
Below is information related to exposed events/functions you can use in your resources to integrate your server better with vMenu.
In the FiveM resource, head to `addons` folder and you will see files labelled `integrations_client` & `integrations_server`. 

Here you will find any events/exports for use and you can implement your server specific needs.

Example Client Event:
```lua
---@class infiniteFuelToggled
---@field enabled boolean
AddEventHandler("vMenu:Integrations:InfiniteFuel", function(enabled)
    lib.print.debug("Infinite Fuel: " .. tostring(enabled))
end)
```

### To-Do
- [ ] Sync Time/Weather into GlobalStates
- [ ] Add new event for ban manager so that developers can easily integrate their own anticheat/banning functions for event exploiters
- [ ] Create separate branch for pre-category version of vMenu with same features/improvements
- [ ] Ratelimit on close all/open all doors (exploit to make cars fly)
- [ ] Configurable vehicle spawn cooldown
- [ ] Take weapon spawning functionality out of c# and add export for LUA so that devs can easily integrate ox_inventory
- [ ] Export to add weapons + attachments into vmenu categories without them having to rebuild [REMOVE ADDON WEAPON SUBMENU / CODE] (maybe this gets extended to peds/vehicles?)
- [ ] Add a export before weapon/vehicle spawning/teleports such as isRestrained() so developers can easily block actions and add their own cuff/death scripts etc
- [ ] ~~Separate branch (maybe?) for outfit/weapon/vehicle code system~~ Planned to go ahead in main fork and add dependency of oxmysql as most servers use it. Maybe ill just do a resource check so if the resource isnt installed the buttons just error and say plugin not installed or smth?
- [ ] Update weapon attachment right button if it is equipped (checkmark)
- [x] Copy Coords Button (devtools)
- [ ] Ability to save BP tires on vehicles? (would need to perm check on re-apply)
- [x] Add an event that is triggered when infinite fuel is enabled so developers can easily integrate with scripts other than FRFUEL

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
