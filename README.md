# vMenu: ox_lib Support
This fork is designed to integrate with [ox_lib](https://github.com/overextended/ox_lib/releases/latest) you will require it for this to work.

This fork introduces some new exports that work with ox_lib for better UI features and to make the menu more convenient/easier to use for players.

Here's an example where we can move away from 3 separate input popups for rgb number values and to ox_lib's native inputDialog where the user can easily select a custom colour and visually see what they're choosing.
![Medal_ErnwOZVBn9](https://github.com/user-attachments/assets/a83d965e-05f0-4125-9e9c-65d7f03c0fd0)


<img src="https://github.com/user-attachments/assets/d960116b-3540-485b-ad7a-ecaa1fdd42e4" width="500"><br>

--------

### List of Changes that this fork does:
- Colour Selector
  - Gives users a hex selector for custom colour setting within vehicle options (primary/secondary)
- Input Dialog Replace
  - Replaces the base game user input and replaces with ox_lib input dialog for easier use such as copy/pasting spawncodes etc.
- Keybinds for Thermal & Night Vision Modes
  - This is locked to users that have the permissions assigned to them.

If more are added, they will be listed here.

### Support/Issues with my Fork
If you require support or have an issue / suggestion, feel free to join my discord!
[discord.gg/chromalabs](https://discord.gg/chromalabs)

--------

Below is the information for the source project, all credit to the creation goes to Vespura, thank you to him for making an easy to use open source project for everyone.

--------

# vMenu
vMenu is a server-side menu for FiveM servers, including full\* permission support.


\*(Some features do not have permissions support as they are either harmless or it'd just be silly to deny them. However, they will be disabled if you deny access to the submenu that they are a part of (eg: unlimited stamina in Player Options will be disabled if you deny `vMenu.PlayerOptions.Menu`.))

--------

# Download & Installation & Permissions

## Download

Click [here](https://github.com/Gravxd/vMenu/releases/) to go to the releases page and download it.

--------

## Installation
Please follow the instructions over at the [vMenu docs](https://docs.vespura.com/vmenu/installation)

## ZAP-Hosting
You can use ZAP-Hosting's one-click installer for vMenu. Get your own FiveM server at ZAP-Hosting with a 20% discount [HERE](https://zap-hosting.com/vespura) and make sure to use `Vespura` at checkout.

--------

## Support
If you like my work, please consider supporting me on [**Patreon**](https://www.patreon.com/vespura). I've put a _lot_ of my time and hard work into these and other projects.

--------

## Trouble shooting & support
Take a look at the docs first of all. I will ignore you if your question is answered on the docs or the forum topic.

- [Docs](https://docs.vespura.com/vmenu/)
- [Forum topic](https://vespura.com/vmenu)
- [Discord](https://vespura.com/discord)


--------

## Permissions 
Click [here](https://docs.vespura.com/vmenu/permissions-ref) for permission information.

## Configuration
Click [here](https://docs.vespura.com/vmenu/configuration) for configuration options information.


--------


## MenuAPI
Starting from vMenu v2.1.0, vMenu will be using [MenuAPI (MAPI)](https://github.com/TomGrobbe/MenuAPI), a custom menu API designed specifically for vMenu by me.

vMenu v2.0.0 and earlier was [using a modified version of NativeUI](https://github.com/TomGrobbe/NativeUI), originally by [Guad](https://github.com/Guad/NativeUI), but converted to FiveM by the CitizenFX Collectives and myself (updated/refactored).


--------

## License
**For an updated license, check the license.md file. That file will always overrule anything mentioned in the readme.md**

Tom Grobbe - https://www.vespura.com/

Copyright © 2017-2023

You can use and edit this code to your liking as long as you don't ever claim it to be your code and always provide proper credit. 
You're **not** allowed to sell vMenu or any code you take from it.
If you want to release your version of vMenu, you have to link the original GitHub repo or release it via a Forked repo.
