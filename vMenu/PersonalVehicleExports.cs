using System;

using CitizenFX.Core;

using static vMenuShared.PermissionsManager;

namespace vMenuClient
{
    /// <summary>
    /// Personal vehicle exports for other resources (e.g. a radial menu). They go through the same permissions
    /// as the Personal Vehicle menu, and pass the call on to it. The menu only exists once permissions are set
    /// up and only for players allowed to use it, so these live on their own always-loaded BaseScript.
    /// The query exports never notify; the action exports tell the player why nothing happened.
    /// </summary>
    public class PersonalVehicleExports : BaseScript
    {
        public PersonalVehicleExports()
        {
            Exports.Add("SetCurrentPersonalVehicle", new Action(() => GetMenu("use personal vehicle options")?.SetCurrentVehicle()));
            Exports.Add("LockPersonal", new Action(() => GetMenu("lock or unlock your personal vehicle", Permission.PVLockDoors)?.ToggleDoorLock()));
            Exports.Add("TogglePersonalEngine", new Action(() => GetMenu("toggle your personal vehicle's engine", Permission.PVToggleEngine)?.ToggleEngine()));
            Exports.Add("HasPersonalVehicle", new Func<bool>(() => MainMenu.PermissionsSetupComplete && MainMenu.PersonalVehicleMenu != null && MainMenu.PersonalVehicleMenu.HasVehicle));
            Exports.Add("GetPersonalVehicle", new Func<int>(() =>
                MainMenu.PermissionsSetupComplete && MainMenu.PersonalVehicleMenu != null && MainMenu.PersonalVehicleMenu.HasVehicle
                    ? MainMenu.PersonalVehicleMenu.CurrentPersonalVehicle.NetworkId
                    : 0));
        }

        /// <summary>
        /// Returns the Personal Vehicle menu if the player may use it (and the given permission, if any),
        /// otherwise notifies the player and returns null.
        /// </summary>
        private static menus.PersonalVehicle GetMenu(string action, Permission? permission = null)
        {
            if (!MainMenu.PermissionsSetupComplete)
            {
                Notify.Error("vMenu is still loading, try again in a moment.");
                return null;
            }

            if (MainMenu.PersonalVehicleMenu == null || (permission.HasValue && !IsAllowed(permission.Value)))
            {
                Notify.Error(CommonErrors.NotAllowed, placeholderValue: action);
                return null;
            }

            return MainMenu.PersonalVehicleMenu;
        }
    }
}
