using System;

using CitizenFX.Core;

using static vMenuShared.PermissionsManager;

namespace vMenuClient
{
    /// <summary>
    /// Vehicle exports for other resources (e.g. a radial menu). They go through the same permissions as the
    /// matching vMenu menus and pass the call on to them. The menus only exist once permissions are set up and
    /// only for players allowed to use them, so these live on their own always-loaded BaseScript.
    /// The query exports never notify; the action exports tell the player why nothing happened and return
    /// what they did (nil in Lua when nothing happened) so the caller can confirm it.
    /// </summary>
    public class VehicleExports : BaseScript
    {
        public VehicleExports()
        {
            // Personal vehicle
            Exports.Add("SetCurrentPersonalVehicle", new Func<string>(() => GetPersonalVehicleMenu("use personal vehicle options")?.SetCurrentVehicle()));
            Exports.Add("LockPersonal", new Func<string>(() =>
            {
                var locked = GetPersonalVehicleMenu("lock or unlock your personal vehicle", Permission.PVLockDoors)?.ToggleDoorLock();
                return locked.HasValue ? (locked.Value ? "locked" : "unlocked") : null;
            }));
            Exports.Add("TogglePersonalEngine", new Func<string>(() =>
            {
                var engineOn = GetPersonalVehicleMenu("toggle your personal vehicle's engine", Permission.PVToggleEngine)?.ToggleEngine();
                return engineOn.HasValue ? (engineOn.Value ? "on" : "off") : null;
            }));
            Exports.Add("HasPersonalVehicle", new Func<bool>(() => MainMenu.PermissionsSetupComplete && MainMenu.PersonalVehicleMenu != null && MainMenu.PersonalVehicleMenu.HasVehicle));
            Exports.Add("GetPersonalVehicle", new Func<int>(() =>
                MainMenu.PermissionsSetupComplete && MainMenu.PersonalVehicleMenu != null && MainMenu.PersonalVehicleMenu.HasVehicle
                    ? MainMenu.PersonalVehicleMenu.CurrentPersonalVehicle.NetworkId
                    : 0));

            // Current vehicle. `which` is "front" or "rear"; `up` rolls the windows up, otherwise down.
            Exports.Add("RollVehicleWindows", new Func<object, object, bool>((which, up) => RollWindows(which as string, up is bool b && b)));
        }

        private static bool RollWindows(string which, bool up)
        {
            if (which != "front" && which != "rear")
            {
                Notify.Error(CommonErrors.InvalidInput, placeholderValue: which ?? "nil");
                return false;
            }

            if (!IsLoaded())
            {
                return false;
            }

            if (MainMenu.VehicleOptionsMenu == null || !IsAllowed(Permission.VOWindows))
            {
                Notify.Error(CommonErrors.NotAllowed, placeholderValue: "roll your vehicle windows");
                return false;
            }

            return MainMenu.VehicleOptionsMenu.RollWindows(which == "front", up);
        }

        /// <summary>
        /// Returns the Personal Vehicle menu if the player may use it (and the given permission, if any),
        /// otherwise notifies the player and returns null.
        /// </summary>
        private static menus.PersonalVehicle GetPersonalVehicleMenu(string action, Permission? permission = null)
        {
            if (!IsLoaded())
            {
                return null;
            }

            if (MainMenu.PersonalVehicleMenu == null || (permission.HasValue && !IsAllowed(permission.Value)))
            {
                Notify.Error(CommonErrors.NotAllowed, placeholderValue: action);
                return null;
            }

            return MainMenu.PersonalVehicleMenu;
        }

        private static bool IsLoaded()
        {
            if (!MainMenu.PermissionsSetupComplete)
            {
                Notify.Error("vMenu is still loading, try again in a moment.");
                return false;
            }
            return true;
        }
    }
}
