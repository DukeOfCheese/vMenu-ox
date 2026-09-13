using System;
using System.Collections.Generic;
using System.Linq;

using CitizenFX.Core;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using MenuAPI;

using vMenuClient.data;

using static CitizenFX.Core.Native.API;
using static vMenuClient.CommonFunctions;
using static vMenuShared.PermissionsManager;

namespace vMenuClient.menus
{
    public class VehicleSpawner
    {
        // Variables
        private Menu menu;

        public bool SpawnInVehicle { get; private set; } = UserDefaults.VehicleSpawnerSpawnInside;
        public bool ReplaceVehicle { get; private set; } = UserDefaults.VehicleSpawnerReplacePrevious;
        public static List<bool> allowedCategories;

        /// <summary>
        /// Addon vehicles the server sent us, lower cased. They are gated by their own ACE
        /// (vMenu.VehicleSpawner.&lt;spawn&gt;), so they stay visible even when the vehicle class
        /// they fall into is locked -- but they do not unlock the rest of that class.
        /// </summary>
        private static readonly HashSet<string> addonVehicleNames = new();

        // Tracks whether addon vehicles have already been loaded & inserted into the vehicle class lists.
        // Loading is a one-time operation (the lists are static and persist), so guard against re-reading
        // re-inserting the same vehicles on every search refresh.
        private static bool addonsLoaded = false;

        // These are the max speed, acceleration, braking and traction values per vehicle class.
        // Hoisted to static readonly fields so they're allocated once instead of on every RefreshSpawnableVehicles call.
        private static readonly float[] speedValues = new float[23]
        {
            44.9374657f,
            50.0000038f,
            48.862133f,
            48.1321335f,
            50.7077942f,
            51.3333359f,
            52.3922348f,
            53.86687f,
            52.03867f,
            49.2241631f,
            39.6176529f,
            37.5559425f,
            42.72843f,
            21.0f,
            45.0f,
            65.1952744f,
            109.764259f,
            42.72843f,
            56.5962219f,
            57.5398865f,
            43.3140678f,
            26.66667f,
            53.0537224f
        };
        private static readonly float[] accelerationValues = new float[23]
        {
            0.34f,
            0.29f,
            0.335f,
            0.28f,
            0.395f,
            0.39f,
            0.66f,
            0.42f,
            0.425f,
            0.475f,
            0.21f,
            0.3f,
            0.32f,
            0.17f,
            18.0f,
            5.88f,
            21.0700016f,
            0.33f,
            14.0f,
            6.86f,
            0.32f,
            0.2f,
            0.76f
        };
        private static readonly float[] brakingValues = new float[23]
        {
            0.72f,
            0.95f,
            0.85f,
            0.9f,
            1.0f,
            1.0f,
            1.3f,
            1.25f,
            1.52f,
            1.1f,
            0.6f,
            0.7f,
            0.8f,
            3.0f,
            0.4f,
            3.5920403f,
            20.58f,
            0.9f,
            2.93960738f,
            3.9472363f,
            0.85f,
            5.0f,
            1.3f
        };
        private static readonly float[] tractionValues = new float[23]
        {
            2.3f,
            2.55f,
            2.3f,
            2.6f,
            2.625f,
            2.65f,
            2.8f,
            2.782f,
            2.9f,
            2.95f,
            2.0f,
            3.3f,
            2.175f,
            2.05f,
            0.0f,
            1.6f,
            2.15f,
            2.55f,
            2.57f,
            3.7f,
            2.05f,
            2.5f,
            3.2925f
        };

        /// <summary>
        /// A single spawnable vehicle, hung on its MenuItem's ItemData.
        ///
        /// Carrying the model name on the item (rather than mapping the menu index back into
        /// VehicleData.Vehicles.VehicleClasses) is what makes the class menus safe to filter and
        /// safe to build with per-vehicle permission gaps in them.
        /// </summary>
        private sealed class VehEntry
        {
            public string Model;
            public string ClassName;
            public string DisplayName;
            public bool Exists;
            public float[] Stats;
        }

        /// <summary>
        /// The 23 vehicle class submenus and the buttons that open them, for the search to filter.
        /// </summary>
        private readonly List<MenuSearch.Category> vehicleCategories = new();

        private void CreateMenu()
        {
            // Create the menu.
            menu = new Menu(Game.Player.Name, "Vehicle Spawner");

            BuildSpawnableVehicles(menu);
        }

        /// <summary>
        /// Builds the vehicle spawner and its 23 class submenus. Called exactly once, from
        /// <see cref="CreateMenu"/> -- searching no longer tears the menu down and rebuilds it.
        /// </summary>
        private void BuildSpawnableVehicles(Menu menu)
        {
            #region initial setup.
            // Create the buttons and checkboxes.
            var spawnByName = new MenuItem("Spawn Vehicle By Model Name", "Enter the name of a vehicle to spawn.");
            var spawnInVeh = new MenuCheckboxItem("Spawn Inside Vehicle", "This will teleport you into the vehicle when you spawn it.", SpawnInVehicle);
            var replacePrev = new MenuCheckboxItem("Replace Previous Vehicle", "This will automatically delete your previously spawned vehicle when you spawn a new vehicle.", ReplaceVehicle);

            // Add the items to the menu.
            if (IsAllowed(Permission.VSSpawnByName))
            {
                menu.AddMenuItem(spawnByName);
            }

            // Sits directly under "Spawn Vehicle By Model Name", above the class buttons.
            MenuSearch.AddCategorySearch(menu, "Vehicles", () => vehicleCategories);

            menu.AddMenuItem(spawnInVeh);
            menu.AddMenuItem(replacePrev);
            #endregion

            // Load addon vehicles exactly once. The vehicle class lists are static and persist across
            // menu instances, so re-inserting would duplicate vehicles.
            // The server has already applied this player's ACE permissions to the list.
            if (!addonsLoaded)
            {
                foreach (var vehicle in data.AddonsManager.Vehicles)
                {
                    var spawnName = vehicle.spawn?.Trim();
                    if (!string.IsNullOrWhiteSpace(spawnName))
                    {
                        addonVehicleNames.Add(spawnName.ToLowerInvariant());
                    }
                }

                VehicleData.Vehicles.ProcessAddonVehicles(addonVehicleNames.ToList());
                Debug.WriteLine($"[VMENU] Loaded {addonVehicleNames.Count} addon vehicles.");

                addonsLoaded = true;
            }

            // Drives the vehicle stats panel from the highlighted item's payload. Shared by the
            // class submenus and by the search results menu.
            static void HandleStatsPanel(Menu openedMenu, MenuItem currentItem)
            {
                if (currentItem == null)
                {
                    return;
                }

                if (currentItem.ItemData is VehEntry entry)
                {
                    openedMenu.ShowVehicleStatsPanel = true;
                    openedMenu.SetVehicleStats(entry.Stats[0], entry.Stats[1], entry.Stats[2], entry.Stats[3]);
                    openedMenu.SetVehicleUpgradeStats(0f, 0f, 0f, 0f);
                }
                else
                {
                    openedMenu.ShowVehicleStatsPanel = false;
                }
            }

            #region vehicle classes submenus
            // Loop through all the vehicle classes.
            for (var vehClass = 0; vehClass < 23; vehClass++)
            {
                // Get the class name.
                var className = GetLabelText($"VEH_CLASS_{vehClass}");

                // Create a button & a menu for it, add the menu to the menu pool and add & bind the button to the menu.
                var btn = new MenuItem(className, $"Spawn a vehicle from the ~o~{className} ~s~class.")
                {
                    Label = "→→→"
                };

                var vehicleClassMenu = new Menu("Vehicle Spawner", className);

                MenuController.AddSubmenu(menu, vehicleClassMenu);
                menu.AddMenuItem(btn);

                // A locked class still opens if this player has an addon vehicle that lands in it;
                // the per-vehicle filter below keeps the rest of the class hidden.
                var classHasAllowedAddon = VehicleData.Vehicles.VehicleClasses[className]
                    .Any(v => addonVehicleNames.Contains(v.ToLowerInvariant()));

                if (allowedCategories[vehClass] || classHasAllowedAddon)
                {
                    MenuController.BindMenuItem(menu, vehicleClassMenu, btn);
                }
                else
                {
                    btn.LeftIcon = MenuItem.Icon.LOCK;
                    btn.Description = "This category has been disabled by the server owner.";
                    btn.Enabled = false;
                }

                // Create a dictionary for the duplicate vehicle names (in this vehicle class).
                var duplicateVehNames = new Dictionary<string, int>();

                // Track the (un-suffixed) display names already added to this class menu so duplicate
                // detection is an O(1) set lookup instead of an O(n) scan over every existing menu item.
                var addedVehNames = new HashSet<string>();

                #region Add vehicles per class
                // Loop through all the vehicles in the vehicle class.
                foreach (var veh in VehicleData.Vehicles.VehicleClasses[className])
                {
                    // When the class itself is not permitted, only the addon vehicles this player has
                    // been granted individually are shown.
                    if (!allowedCategories[vehClass] && !addonVehicleNames.Contains(veh.ToLowerInvariant()))
                    {
                        continue;
                    }

                    // Convert the model name to start with a Capital letter, converting the other characters to lowercase.
                    var properCasedModelName = veh[0].ToString().ToUpper() + veh.ToLower().Substring(1);

                    // Get the localized vehicle name, if it is "NULL" (no label found) then use the "properCasedModelName" created above.
                    var vehName = GetVehDisplayNameFromModel(veh) != "NULL" ? GetVehDisplayNameFromModel(veh) : properCasedModelName;

                    var model = (uint)GetHashKey(veh);
                    var exists = DoesModelExist(veh);

                    // Duplicate display names get a " (2)", " (3)", ... suffix.
                    if (addedVehNames.Contains(vehName))
                    {
                        duplicateVehNames[vehName] = duplicateVehNames.ContainsKey(vehName) ? duplicateVehNames[vehName] + 1 : 2;
                        vehName += $" ({duplicateVehNames[vehName]})";
                    }
                    else
                    {
                        // Remember this (un-suffixed) name so later vehicles with the same name are detected as duplicates.
                        addedVehNames.Add(vehName);
                    }

                    var entry = new VehEntry
                    {
                        Model = veh,
                        ClassName = className,
                        DisplayName = vehName,
                        Exists = exists,
                        Stats = exists
                            ? new float[4]
                            {
                                Map(GetVehicleModelEstimatedMaxSpeed(model), 0f, speedValues[vehClass], 0f, 1f),
                                Map(GetVehicleModelAcceleration(model), 0f, accelerationValues[vehClass], 0f, 1f),
                                Map(GetVehicleModelMaxBraking(model), 0f, brakingValues[vehClass], 0f, 1f),
                                Map(GetVehicleModelMaxTraction(model), 0f, tractionValues[vehClass], 0f, 1f)
                            }
                            : new float[4] { 0f, 0f, 0f, 0f }
                    };

                    var vehBtn = exists
                        ? new MenuItem(vehName)
                        {
                            Enabled = true,
                            Label = $"({veh.ToLower()})",
                            ItemData = entry
                        }
                        : new MenuItem(vehName, "This vehicle is not available because the model could not be found in your game files. If this is a DLC vehicle, make sure the server is streaming it.")
                        {
                            Enabled = false,
                            Label = $"({veh.ToLower()})",
                            ItemData = entry,
                            RightIcon = MenuItem.Icon.LOCK
                        };

                    vehicleClassMenu.AddMenuItem(vehBtn);
                }
                #endregion

                vehicleClassMenu.ShowVehicleStatsPanel = vehicleClassMenu.Size > 0;

                // Handle button presses. Resolved from the payload on the item, never from the index:
                // the index does not line up with the source list once permission gaps or an active
                // filter are in play.
                vehicleClassMenu.OnItemSelect += async (_, item2, __) =>
                {
                    if (item2.ItemData is VehEntry entry)
                    {
                        await SpawnVehicle(entry.Model, SpawnInVehicle, ReplaceVehicle);
                    }
                };

                vehicleClassMenu.OnMenuOpen += (m) =>
                {
                    HandleStatsPanel(m, m.GetCurrentMenuItem());
                };

                vehicleClassMenu.OnIndexChange += (m, oldItem, newItem, oldIndex, newIndex) =>
                {
                    HandleStatsPanel(m, newItem);
                };

                vehicleCategories.Add(new MenuSearch.Category
                {
                    Button = btn,
                    Menu = vehicleClassMenu,
                    Name = className
                });
            }
            #endregion

            #region handle events
            // Handle button presses.
            menu.OnItemSelect += async (sender, item, index) =>
            {
                if (item == spawnByName)
                {
                    // Passing "custom" as the vehicle name, will ask the user for input.
                    await SpawnVehicle("custom", SpawnInVehicle, ReplaceVehicle);
                }
            };

            // Handle checkbox changes.
            menu.OnCheckboxChange += (sender, item, index, _checked) =>
            {
                if (item == spawnInVeh)
                {
                    SpawnInVehicle = _checked;
                }
                else if (item == replacePrev)
                {
                    ReplaceVehicle = _checked;
                }
            };
            #endregion
        }

        /// <summary>
        /// Create the menu if it doesn't exist, and then returns it.
        /// </summary>
        /// <returns>The Menu</returns>
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