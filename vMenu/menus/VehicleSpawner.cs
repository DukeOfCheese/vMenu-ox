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
        private string SearchTerm = "";
        public static List<bool> allowedCategories;

        // Tracks whether addon vehicles have already been loaded & inserted into the vehicle class lists.
        // Loading is a one-time operation (the lists are static and persist), so guard against re-reading
        // addons.json and re-inserting the same vehicles on every search refresh.
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

        private void CreateMenu()
        {
            #region initial setup.
            // Create the menu.
            menu = new Menu(Game.Player.Name, "Vehicle Spawner");

            #region vehicle classes submenus
            // Loop through all the vehicle classes.
            RefreshSpawnableVehicles(menu);
        }

        private void RefreshSpawnableVehicles(Menu menu)
        {
            menu.ClearMenuItems(true);

            // Create the buttons and checkboxes.
            var spawnByName = new MenuItem("Spawn Vehicle By Model Name", "Enter the name of a vehicle to spawn.");
            var searchButton = new MenuItem("Search for Vehicle", "This will allow you to search through the available vehicles.");
            var spawnInVeh = new MenuCheckboxItem("Spawn Inside Vehicle", "This will teleport you into the vehicle when you spawn it.", SpawnInVehicle);
            var replacePrev = new MenuCheckboxItem("Replace Previous Vehicle", "This will automatically delete your previously spawned vehicle when you spawn a new vehicle.", ReplaceVehicle);

            // Add the items to the menu.
            if (IsAllowed(Permission.VSSpawnByName))
            {
                menu.AddMenuItem(spawnByName);
            }
            menu.AddMenuItem(searchButton);
            menu.AddMenuItem(spawnInVeh);
            menu.AddMenuItem(replacePrev);
            #endregion

            // Load addon vehicles exactly once. The vehicle class lists are static and persist across
            // refreshes, so re-reading addons.json and re-inserting on every search would duplicate
            // vehicles and waste a file read + JSON parse each time.
            if (!addonsLoaded)
            {
                var jsonData = LoadResourceFile(GetCurrentResourceName(), "config/addons.json") ?? "{}";
                var addons = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonData);

                if (addons != null && addons.ContainsKey("vehicles"))
                    {
                        var vehiclesList = JArray.FromObject(addons["vehicles"])
                                .ToObject<List<string>>();

                        VehicleData.Vehicles.ProcessAddonVehicles(vehiclesList);

                        Debug.WriteLine($"[VMENU] Loaded {vehiclesList.Count} addon vehicles");
                    }
                else
                {
                    Debug.WriteLine("[VMENU] No addon vehicles in addons.json");
                }

                addonsLoaded = true;
            }

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

                if (allowedCategories[vehClass])
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
                    // Convert the model name to start with a Capital letter, converting the other characters to lowercase. 
                    var properCasedModelName = veh[0].ToString().ToUpper() + veh.ToLower().Substring(1);

                    // Get the localized vehicle name, if it's "NULL" (no label found) then use the "properCasedModelName" created above.
                    var vehName = GetVehDisplayNameFromModel(veh) != "NULL" ? GetVehDisplayNameFromModel(veh) : properCasedModelName;
                    if (string.IsNullOrWhiteSpace(SearchTerm) || vehName.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var vehModelName = veh;
                        var model = (uint)GetHashKey(vehModelName);

                        var topSpeed = Map(GetVehicleModelEstimatedMaxSpeed(model), 0f, speedValues[vehClass], 0f, 1f);
                        var acceleration = Map(GetVehicleModelAcceleration(model), 0f, accelerationValues[vehClass], 0f, 1f);
                        var maxBraking = Map(GetVehicleModelMaxBraking(model), 0f, brakingValues[vehClass], 0f, 1f);
                        var maxTraction = Map(GetVehicleModelMaxTraction(model), 0f, tractionValues[vehClass], 0f, 1f);

                        // Check whether this (un-suffixed) display name has already been added to this class menu.
                        var duplicate = false;
                        if (addedVehNames.Contains(vehName))
                            {
                                {

                                    // Check if the model was marked as duplicate before.
                                    if (duplicateVehNames.ContainsKey(vehName))
                                    {
                                        // If so, add 1 to the duplicate counter for this model name.
                                        duplicateVehNames[vehName]++;
                                    }

                                    // If this is the first duplicate, then set it to 2.
                                    else
                                    {
                                        duplicateVehNames[vehName] = 2;
                                    }

                                    // The model name is a duplicate, so get the modelname and add the duplicate amount for this model name to the end of the vehicle name.
                                    vehName += $" ({duplicateVehNames[vehName]})";

                                    // Then create and add a new button for this vehicle.

                                    if (DoesModelExist(veh))
                                    {
                                        var vehBtn = new MenuItem(vehName)
                                        {
                                            Enabled = true,
                                            Label = $"({vehModelName.ToLower()})",
                                            ItemData = new float[4] { topSpeed, acceleration, maxBraking, maxTraction }
                                        };
                                        vehicleClassMenu.AddMenuItem(vehBtn);
                                    }
                                    else
                                    {
                                        var vehBtn = new MenuItem(vehName, "This vehicle is not available because the model could not be found in your game files. If this is a DLC vehicle, make sure the server is streaming it.")
                                        {
                                            Enabled = false,
                                            Label = $"({vehModelName.ToLower()})",
                                            ItemData = new float[4] { 0f, 0f, 0f, 0f }
                                        };
                                        vehicleClassMenu.AddMenuItem(vehBtn);
                                        vehBtn.RightIcon = MenuItem.Icon.LOCK;
                                    }

                                    // Mark duplicate as true.
                                    duplicate = true;
                                }
                            }

                            // If it's not a duplicate, add the model name.
                            if (!duplicate)
                            {
                                // Remember this (un-suffixed) name so later vehicles with the same name are detected as duplicates.
                                addedVehNames.Add(vehName);

                                if (DoesModelExist(veh))
                                {
                                    var vehBtn = new MenuItem(vehName)
                                    {
                                        Enabled = true,
                                        Label = $"({vehModelName.ToLower()})",
                                        ItemData = new float[4] { topSpeed, acceleration, maxBraking, maxTraction }
                                    };
                                    vehicleClassMenu.AddMenuItem(vehBtn);
                                }
                                else
                                {
                                    var vehBtn = new MenuItem(vehName, "This vehicle is not available because the model could not be found in your game files. If this is a DLC vehicle, make sure the server is streaming it.")
                                    {
                                        Enabled = false,
                                        Label = $"({vehModelName.ToLower()})",
                                        ItemData = new float[4] { 0f, 0f, 0f, 0f }
                                    };
                                    vehicleClassMenu.AddMenuItem(vehBtn);
                                    vehBtn.RightIcon = MenuItem.Icon.LOCK;
                                }
                            }
                        }
                    }
                    #endregion

                    vehicleClassMenu.ShowVehicleStatsPanel = vehicleClassMenu.Size > 0;

                    // Handle button presses
                    vehicleClassMenu.OnItemSelect += async (sender2, item2, index2) =>
                    {
                        await SpawnVehicle(VehicleData.Vehicles.VehicleClasses[className][index2], SpawnInVehicle, ReplaceVehicle);
                    };

                    static void HandleStatsPanel(Menu openedMenu, MenuItem currentItem)
                    {
                        if (currentItem != null)
                        {
                            if (currentItem.ItemData is float[] data)
                            {
                                openedMenu.ShowVehicleStatsPanel = true;
                                openedMenu.SetVehicleStats(data[0], data[1], data[2], data[3]);
                                openedMenu.SetVehicleUpgradeStats(0f, 0f, 0f, 0f);
                            }
                            else
                            {
                                openedMenu.ShowVehicleStatsPanel = false;
                            }
                        }
                }

                vehicleClassMenu.OnMenuOpen += (m) =>
                {
                    HandleStatsPanel(m, m.GetCurrentMenuItem());
                };

                vehicleClassMenu.OnIndexChange += (m, oldItem, newItem, oldIndex, newIndex) =>
                {
                    HandleStatsPanel(m, newItem);
                };
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
                else if (item == searchButton)
                {
                    SearchTerm = await GetUserInput(windowTitle: "Enter Search Term (Leave BLANK to reset)", maxInputLength: 100);
                    RefreshSpawnableVehicles(menu);
                    SearchTerm = "";
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