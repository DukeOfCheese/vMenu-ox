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
