using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using CitizenFX.Core;

using MenuAPI;

using vMenuClient.data;

using static CitizenFX.Core.Native.API;
using static vMenuClient.CommonFunctions;
using static vMenuShared.ConfigManager;
using static vMenuShared.PermissionsManager;

namespace vMenuClient.menus
{
    public class VehicleOptions : BaseScript
    {
        #region Variables
        // Menu variable, will be defined in CreateMenu()
        private Menu menu;
        public static Dictionary<uint, Dictionary<int, string>> VehicleExtras;

        // Submenus
        public Menu VehicleModMenu { get; private set; }
        public Menu VehicleDoorsMenu { get; private set; }
        public Menu VehicleWindowsMenu { get; private set; }
        public Menu VehicleComponentsMenu { get; private set; }
        public Menu VehicleLiveriesMenu { get; private set; }
        public Menu VehicleColorsMenu { get; private set; }
        public Menu DeleteConfirmMenu { get; private set; }
        public Menu VehicleUnderglowMenu { get; private set; }
        public Menu VehicleEngineSoundMenu { get; private set; }
        public Menu VehicleHandlingMenu { get; private set; }

        // Public variables (getters only), return the private variables.
        public bool VehicleGodMode { get; private set; } = UserDefaults.VehicleGodMode;
        public bool VehicleGodInvincible { get; private set; } = UserDefaults.VehicleGodInvincible;
        public bool VehicleGodEngine { get; private set; } = UserDefaults.VehicleGodEngine;
        public bool VehicleGodVisual { get; private set; } = UserDefaults.VehicleGodVisual;
        public bool VehicleGodStrongWheels { get; private set; } = UserDefaults.VehicleGodStrongWheels;
        public bool VehicleGodRamp { get; private set; } = UserDefaults.VehicleGodRamp;
        public bool VehicleGodAutoRepair { get; private set; } = UserDefaults.VehicleGodAutoRepair;

        public bool VehicleNeverDirty { get; private set; } = UserDefaults.VehicleNeverDirty;
        public bool VehicleEngineAlwaysOn { get; private set; } = UserDefaults.VehicleEngineAlwaysOn;
        public bool VehicleNoSiren { get; private set; } = UserDefaults.VehicleNoSiren;
        public bool VehicleNoBikeHelemet { get; private set; } = UserDefaults.VehicleNoBikeHelmet;
        public bool FlashHighbeamsOnHonk { get; private set; } = UserDefaults.VehicleHighbeamsOnHonk;
        public bool DisablePlaneTurbulence { get; private set; } = UserDefaults.VehicleDisablePlaneTurbulence;
        public bool DisableHelicopterTurbulence { get; private set; } = UserDefaults.VehicleDisableHelicopterTurbulence;
        public bool AnchorBoat { get; private set; } = UserDefaults.VehicleAnchorBoat;
        public bool VehicleBikeSeatbelt { get; private set; } = UserDefaults.VehicleBikeSeatbelt;
        public bool VehicleInfiniteFuel { get; private set; } = false;
        public bool VehicleShowHealth { get; private set; } = false;
        public bool VehicleFrozen { get; private set; } = false;
        public bool VehicleTorqueMultiplier { get; private set; } = false;
        public bool VehiclePowerMultiplier { get; private set; } = false;
        public bool VehicleBrakeMultiplier { get; private set; } = false;
        public float VehicleTorqueMultiplierAmount { get; private set; } = 2f;
        public float VehiclePowerMultiplierAmount { get; private set; } = 2f;
        public float VehicleBrakeMultiplierAmount { get; private set; } = 2f;

        public bool VehicleCloseAllDoorsCooldown { get; private set; } = false;

        private readonly Dictionary<MenuItem, int> vehicleExtras = [];

        #endregion

        #region Vehicle Sound Syncing
        [EventHandler("onClientResourceStart")]
        public void onClientResourceStart(string resName)
        {
            if (resName != GetCurrentResourceName()) return;
            AddStateBagChangeHandler("vMenu:engineSound", null,
                new Action<string, string, dynamic, int, bool>
            (HandleVehicleSoundChange));
        }

        private static void HandleVehicleSoundChange(string bagName, string key, dynamic value, int reserved, bool replicated)
        {
            var entity = GetEntityFromStateBagName(bagName);
            if (entity == 0) return;
            if (!IsEntityAVehicle(entity)) return;
            ForceUseAudioGameObject(entity, value);
        }
        #endregion

        #region Vehicle Stat Modification
        private async Task HandleVehicleStat(MenuItem item, string field, string title, string suffix = "", double min = -1f, double max = -1f)
        {
            var veh = GetVehicle();
            if (veh == null || !veh.Exists()) return;

            bool isInteger = field.StartsWith("n"); 
            float current = isInteger ? GetVehicleHandlingInt(veh.Handle, "CHandlingData", field) 
                            : GetVehicleHandlingFloat(veh.Handle, "CHandlingData", field);

            float parsedValue;

            if (min != -1f && max != -1f)
            {
                if (isInteger)
                {
                    parsedValue = (float)await GetUserInputSlider(title, (double)current, (double)min, (double)max, 1);
                }
                else
                {
                    parsedValue = (float)await GetUserInputSlider(title, current, min, max, 0.01);
                }
                
            }
            else
            {
                string input = await GetUserInput(title, current.ToString());
                if (!float.TryParse(input, out parsedValue)) 
                {
                    if (!string.IsNullOrEmpty(input)) Notify.Error("Invalid input.");
                    return;
                }
            }

            if (isInteger) SetVehicleHandlingInt(veh.Handle, "CHandlingData", field, (int)parsedValue);
            else SetVehicleHandlingFloat(veh.Handle, "CHandlingData", field, parsedValue);

            item.Label = parsedValue.ToString() + suffix;
            Notify.Success($"Set {item.Text} to {parsedValue.ToString()}{suffix}");
        }
        #endregion

        #region CreateMenu()
        /// <summary>
        /// Create menu creates the vehicle options menu.
        /// </summary>
        private void CreateMenu()
        {
            // Create the menu.
            menu = new Menu(Game.Player.Name, "Vehicle Options");

            #region menu items variables
            // vehicle god mode menu
            var vehGodMenu = new Menu("Vehicle Godmode", "Vehicle Godmode Options");
            var vehGodMenuBtn = new MenuItem("God Mode Options", "Enable or disable specific damage types.") { Label = "→→→" };
            MenuController.AddSubmenu(menu, vehGodMenu);

            // Create Checkboxes.
            var vehicleGod = new MenuCheckboxItem("Vehicle God Mode", "Makes your vehicle not take any damage. Note, you need to go into the god menu options below to select what kind of damage you want to disable.", VehicleGodMode);
            var vehicleNeverDirty = new MenuCheckboxItem("Keep Vehicle Clean", "This will constantly clean your car if the vehicle dirt level goes above 0. Note that this only cleans ~o~dust~s~ or ~o~dirt~s~. This does not clean mud, snow or other ~r~damage decals~s~. Repair your vehicle to remove them.", VehicleNeverDirty);
            var vehicleBikeSeatbelt = new MenuCheckboxItem("Bike Seatbelt", "Prevents you from being knocked off your bike, bicyle, ATV or similar.", VehicleBikeSeatbelt);
            var vehicleEngineAO = new MenuCheckboxItem("Engine Always On", "Keeps your vehicle engine on when you exit your vehicle.", VehicleEngineAlwaysOn);
            var vehicleNoTurbulence = new MenuCheckboxItem("Disable Plane Turbulence", "Disables the turbulence for all planes.", DisablePlaneTurbulence);
            var vehicleNoTurbulenceHeli = new MenuCheckboxItem("Disable Helicopter Turbulence", "Disables the turbulence for all helicopters.", DisableHelicopterTurbulence);
            var vehicleSetAnchor = new MenuCheckboxItem("Anchor Boat", "Only works if the current vehicle is a boat and its position is valid for anchoring", AnchorBoat);
            var vehicleNoSiren = new MenuCheckboxItem("Disable Siren", "Disables your vehicle's siren. Only works if your vehicle actually has a siren.", VehicleNoSiren);
            var vehicleNoBikeHelmet = new MenuCheckboxItem("No Bike Helmet", "No longer auto-equip a helmet when getting on a bike or quad.", VehicleNoBikeHelemet);
            var vehicleFreeze = new MenuCheckboxItem("Freeze Vehicle", "Freeze your vehicle's position.", VehicleFrozen);
            var torqueEnabled = new MenuCheckboxItem("Enable Torque Multiplier", "Enables the torque multiplier selected from the list below.", VehicleTorqueMultiplier);
            var powerEnabled = new MenuCheckboxItem("Enable Power Multiplier", "Enables the power multiplier selected from the list below.", VehiclePowerMultiplier);
            var brakeEnabled = new MenuCheckboxItem("Enable Brake Multiplier", "Enables the brake multiplier selected from the list below.", VehicleBrakeMultiplier);
            var highbeamsOnHonk = new MenuCheckboxItem("Flash Highbeams On Honk", "Turn on your highbeams on your vehicle when honking your horn. Does not work during the day when you have your lights turned off.", FlashHighbeamsOnHonk);
            var showHealth = new MenuCheckboxItem("Show Vehicle Health", "Shows the vehicle health on the screen.", VehicleShowHealth);
            var infiniteFuel = new MenuCheckboxItem("Infinite Fuel", "Enables or disables infinite fuel for this vehicle, only works if FRFuel is installed.", VehicleInfiniteFuel);

            // Create buttons.
            var fixVehicle = new MenuItem("Repair Vehicle", "Repair any visual and physical damage present on your vehicle.");
            var cleanVehicle = new MenuItem("Wash Vehicle", "Clean your vehicle.");
            var toggleEngine = new MenuItem("Toggle Engine On/Off", "Turn your engine on/off.");
            var setLicensePlateText = new MenuItem("Set License Plate Text", "Enter a custom license plate for your vehicle.");
            var modMenuBtn = new MenuItem("Mod Menu", "Tune and customize your vehicle here.")
            {
                Label = "→→→"
            };
            var handlingMenuBtn = new MenuItem("Handling Menu", "Edit your vehicle's handle here.")
            {
                Label = "→→→"
            };
            var doorsMenuBtn = new MenuItem("Vehicle Doors", "Open, close, remove and restore vehicle doors here.")
            {
                Label = "→→→"
            };
            var windowsMenuBtn = new MenuItem("Vehicle Windows", "Roll your windows up/down or remove/restore your vehicle windows here.")
            {
                Label = "→→→"
            };
            var componentsMenuBtn = new MenuItem("Vehicle Extras", "Add/remove vehicle components/extras.")
            {
                Label = "→→→"
            };
            var liveriesMenuBtn = new MenuItem("Vehicle Liveries", "Style your vehicle with fancy liveries!")
            {
                Label = "→→→"
            };
            var colorsMenuBtn = new MenuItem("Vehicle Colors", "Style your vehicle even further by giving it some ~g~Snailsome ~s~colors!")
            {
                Label = "→→→"
            };
            var underglowMenuBtn = new MenuItem("Vehicle Neon Kits", "Make your vehicle shine with some fancy neon underglow!")
            {
                Label = "→→→"
            };
            var engineSoundMenuBtn = new MenuItem("Vehicle Engine Sound", "Modify your vehicle's engine sound!")
            {
                Label = "→→→"
            };
            var vehicleInvisible = new MenuItem("Toggle Vehicle Visibility", "Makes your vehicle visible/invisible. ~r~Your vehicle will be made visible again as soon as you leave the vehicle. Otherwise you would not be able to get back in.");
            var flipVehicle = new MenuItem("Flip Vehicle", "Sets your current vehicle on all 4 wheels.");
            var vehicleAlarm = new MenuItem("Toggle Vehicle Alarm", "Starts/stops your vehicle's alarm.");
            var cycleSeats = new MenuItem("Cycle Through Vehicle Seats", "Cycle through the available vehicle seats.");
            var lights = new List<string>()
            {
                "Hazard Lights",
                "Left Indicator",
                "Right Indicator",
                "Interior Lights",
                //"Taxi Light", // this doesn't seem to work no matter what.
                "Helicopter Spotlight",
            };
            var vehicleLights = new MenuListItem("Vehicle Lights", lights, 0, "Turn vehicle lights on/off.");

            var stationNames = new List<string>();

            foreach (var radioStationName in Enum.GetNames(typeof(RadioStation)))
            {
                stationNames.Add(radioStationName);
            }

            var radioIndex = UserDefaults.VehicleDefaultRadio;

            if (radioIndex == (int)RadioStation.RadioOff)
            {
                var stations = (RadioStation[])Enum.GetValues(typeof(RadioStation));
                var index = Array.IndexOf(stations, RadioStation.RadioOff);
                radioIndex = index;
            }

            var radioStations = new MenuListItem("Default radio station", stationNames, radioIndex, "Select a defalut radio station to be set when spawning new car");

            var tiresList = new List<string>() { "All Tires", "Tire #1", "Tire #2", "Tire #3", "Tire #4", "Tire #5", "Tire #6", "Tire #7", "Tire #8" };
            var vehicleTiresList = new MenuListItem("Fix / Destroy Tires", tiresList, 0, "Fix or destroy a specific vehicle tire, or all of them at once. Note, not all indexes are valid for all vehicles, some might not do anything on certain vehicles.");

            var destroyEngine = new MenuItem("Destroy Engine", "Destroys your vehicle's engine.");
            var fixEngine = new MenuItem("Fix Engine", "Fixes your vehicle's engine.");

            var deleteBtn = new MenuItem("~r~Delete Vehicle", "Delete your vehicle, this ~r~can NOT be undone~s~!")
            {
                LeftIcon = MenuItem.Icon.WARNING,
                Label = "→→→"
            };
            var deleteNoBtn = new MenuItem("NO, CANCEL", "NO, do NOT delete my vehicle and go back!");
            var deleteYesBtn = new MenuItem("~r~YES, DELETE", "Yes I'm sure, delete my vehicle please, I understand that this cannot be undone.")
            {
                LeftIcon = MenuItem.Icon.WARNING
            };

            // Create lists.
            var dirtlevel = new List<string> { "No Dirt", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15" };
            var setDirtLevel = new MenuListItem("Set Dirt Level", dirtlevel, 0, "Select how much dirt should be visible on your vehicle, press ~r~enter~s~ " +
                "to apply the selected level.");
            var licensePlates = new List<string> {
                GetLabelText("CMOD_PLA_0"), // Plate Index 0 // BlueOnWhite1
                GetLabelText("CMOD_PLA_1"), // Plate Index 1 // BlueOnWhite2
                GetLabelText("CMOD_PLA_2"), // Plate Index 2 // BlueOnWhite3
                GetLabelText("CMOD_PLA_3"), // Plate Index 3 // YellowOnBlue
                GetLabelText("CMOD_PLA_4"), // Plate Index 4 // YellowOnBlack
                GetLabelText("PROL"), // Plate Index 5 // NorthYankton
                GetLabelText("CMOD_PLA_6"), // Plate Index 6 // ECola
                GetLabelText("CMOD_PLA_7"), // Plate Index 7 // LasVenturas
                GetLabelText("CMOD_PLA_8"), // Plate Index 8 // LibertyCity
                GetLabelText("CMOD_PLA_9"), // Plate Index 9 // LSCarMeet
                GetLabelText("CMOD_PLA_10"), // Plate Index 10 // LSPanic
                GetLabelText("CMOD_PLA_11"), // Plate Index 11 // LSPounders
                GetLabelText("CMOD_PLA_12"), // Plate Index 12 // Sprunk
            };
            var setLicensePlateType = new MenuListItem("License Plate Type", licensePlates, 0, "Choose a license plate type and press ~r~enter ~s~to apply " +
                "it to your vehicle.");
            var torqueMultiplierList = new List<string> { "x2", "x4", "x8", "x16", "x32", "x64", "x128", "x256", "x512", "x1024" };
            var torqueMultiplier = new MenuListItem("Set Engine Torque Multiplier", torqueMultiplierList, 0, "Set the engine torque multiplier.");
            var powerMultiplierList = new List<string> { "x2", "x4", "x8", "x16", "x32", "x64", "x128", "x256", "x512", "x1024" };
            var powerMultiplier = new MenuListItem("Set Engine Power Multiplier", powerMultiplierList, 0, "Set the engine power multiplier.");
            var speedLimiterOptions = new List<string>() { "Set", "Reset", "Custom Speed Limit" };
            var speedLimiter = new MenuListItem("Speed Limiter", speedLimiterOptions, 0, "Set your vehicles max speed to your ~y~current speed~s~. Resetting your vehicles max speed will set the max speed of your current vehicle back to default. Only your current vehicle is affected by this option.");
            #endregion

            #region Submenus
            // Submenu's
            VehicleModMenu = new Menu("Mod Menu", "Vehicle Mods");
            VehicleHandlingMenu = new Menu("Handling Menu", "Vehicle Handing");
            VehicleModMenu.InstructionalButtons.Add(Control.Jump, "Toggle Vehicle Doors");
            VehicleModMenu.ButtonPressHandlers.Add(new Menu.ButtonPressHandler(Control.Jump, Menu.ControlPressCheckType.JUST_PRESSED, new Action<Menu, Control>((m, c) =>
            {
                var veh = GetVehicle();
                if (veh != null && veh.Exists() && !veh.IsDead && veh.Driver == Game.PlayerPed)
                {
                    var open = GetVehicleDoorAngleRatio(veh.Handle, 0) < 0.1f;
                    if (open)
                    {
                        for (var i = 0; i < 8; i++)
                        {
                            SetVehicleDoorOpen(veh.Handle, i, false, false);
                        }
                    }
                    else
                    {
                        SetVehicleDoorsShut(veh.Handle, false);
                    }
                }
            }), false));
            VehicleDoorsMenu = new Menu("Vehicle Doors", "Vehicle Doors Management");
            VehicleWindowsMenu = new Menu("Vehicle Windows", "Vehicle Windows Management");
            VehicleComponentsMenu = new Menu("Vehicle Extras", "Vehicle Extras/Components");
            VehicleLiveriesMenu = new Menu("Vehicle Liveries", "Vehicle Liveries");
            VehicleColorsMenu = new Menu("Vehicle Colors", "Vehicle Colors");
            DeleteConfirmMenu = new Menu("Confirm Action", "Delete Vehicle, Are You Sure?");
            VehicleUnderglowMenu = new Menu("Vehicle Neon Kits", "Vehicle Neon Underglow Options");
            VehicleEngineSoundMenu = new Menu("Engine Sounds", "Engine Sound Management");

            MenuController.AddSubmenu(menu, VehicleModMenu);
            MenuController.AddSubmenu(menu, VehicleHandlingMenu);
            MenuController.AddSubmenu(menu, VehicleDoorsMenu);
            MenuController.AddSubmenu(menu, VehicleWindowsMenu);
            MenuController.AddSubmenu(menu, VehicleComponentsMenu);
            MenuController.AddSubmenu(menu, VehicleLiveriesMenu);
            MenuController.AddSubmenu(menu, VehicleColorsMenu);
            MenuController.AddSubmenu(menu, DeleteConfirmMenu);
            MenuController.AddSubmenu(menu, VehicleUnderglowMenu);
            MenuController.AddSubmenu(menu, VehicleEngineSoundMenu);
            #endregion

            #region Add items to the menu.
            // Add everything to the menu. (based on permissions)
            if (IsAllowed(Permission.VOGod)) // GOD MODE
            {
                menu.AddMenuItem(vehicleGod);
                menu.AddMenuItem(vehGodMenuBtn);
                MenuController.BindMenuItem(menu, vehGodMenu, vehGodMenuBtn);

                var godInvincible = new MenuCheckboxItem("Invincible", "Makes the car invincible. Includes fire damage, explosion damage, collision damage and more.", VehicleGodInvincible);
                var godEngine = new MenuCheckboxItem("Engine Damage", "Disables your engine from taking any damage.", VehicleGodEngine);
                var godVisual = new MenuCheckboxItem("Visual Damage", "This prevents scratches and other damage decals from being applied to your vehicle. It does not prevent (body) deformation damage.", VehicleGodVisual);
                var godStrongWheels = new MenuCheckboxItem("Strong Wheels", "Disables your wheels from being deformed and causing reduced handling. This does not make tires bulletproof.", VehicleGodStrongWheels);
                var godRamp = new MenuCheckboxItem("Ramp Damage", "Disables vehicles such as the Ramp Buggy from taking damage when using the ramp.", VehicleGodRamp);
                var godAutoRepair = new MenuCheckboxItem("~r~Auto Repair", "Automatically repairs your vehicle when it has ANY type of damage. It's recommended to keep this turned off to prevent glitchyness.", VehicleGodAutoRepair);

                vehGodMenu.AddMenuItem(godInvincible);
                vehGodMenu.AddMenuItem(godEngine);
                vehGodMenu.AddMenuItem(godVisual);
                vehGodMenu.AddMenuItem(godStrongWheels);
                vehGodMenu.AddMenuItem(godRamp);
                vehGodMenu.AddMenuItem(godAutoRepair);

                vehGodMenu.OnCheckboxChange += (sender, item, index, _checked) =>
                {
                    if (item == godInvincible)
                    {
                        VehicleGodInvincible = _checked;
                    }
                    else if (item == godEngine)
                    {
                        VehicleGodEngine = _checked;
                    }
                    else if (item == godVisual)
                    {
                        VehicleGodVisual = _checked;
                    }
                    else if (item == godStrongWheels)
                    {
                        VehicleGodStrongWheels = _checked;
                    }
                    else if (item == godRamp)
                    {
                        VehicleGodRamp = _checked;
                    }
                    else if (item == godAutoRepair)
                    {
                        VehicleGodAutoRepair = _checked;
                    }
                };

            }
            if (IsAllowed(Permission.VORepair)) // REPAIR VEHICLE
            {
                menu.AddMenuItem(fixVehicle);
            }
            if (IsAllowed(Permission.VOKeepClean))
            {
                menu.AddMenuItem(vehicleNeverDirty);
            }
            if (IsAllowed(Permission.VOWash))
            {
                menu.AddMenuItem(cleanVehicle); // CLEAN VEHICLE
                menu.AddMenuItem(setDirtLevel); // SET DIRT LEVEL
            }
            if (IsAllowed(Permission.VOMod)) // MOD MENU
            {
                menu.AddMenuItem(modMenuBtn);
            }
            if (IsAllowed(Permission.VOHandling))
            {
                menu.AddMenuItem(handlingMenuBtn);
            }
            if (IsAllowed(Permission.VOColors)) // COLORS MENU
            {
                menu.AddMenuItem(colorsMenuBtn);
            }
            if (IsAllowed(Permission.VOUnderglow)) // UNDERGLOW EFFECTS
            {
                menu.AddMenuItem(underglowMenuBtn);
                MenuController.BindMenuItem(menu, VehicleUnderglowMenu, underglowMenuBtn);
            }
            if (IsAllowed(Permission.VOLiveries)) // LIVERIES MENU
            {
                menu.AddMenuItem(liveriesMenuBtn);
            }
            if (IsAllowed(Permission.VOComponents)) // COMPONENTS MENU
            {
                menu.AddMenuItem(componentsMenuBtn);
            }
            if (IsAllowed(Permission.VOEngineSound))
            {
                menu.AddMenuItem(engineSoundMenuBtn);
            }
            if (IsAllowed(Permission.VOEngine)) // TOGGLE ENGINE ON/OFF
            {
                menu.AddMenuItem(toggleEngine);
            }
            if (IsAllowed(Permission.VOChangePlate))
            {
                menu.AddMenuItem(setLicensePlateText); // SET LICENSE PLATE TEXT
                menu.AddMenuItem(setLicensePlateType); // SET LICENSE PLATE TYPE
            }
            if (IsAllowed(Permission.VODoors)) // DOORS MENU
            {
                menu.AddMenuItem(doorsMenuBtn);
            }
            if (IsAllowed(Permission.VOWindows)) // WINDOWS MENU
            {
                menu.AddMenuItem(windowsMenuBtn);
            }
            if (IsAllowed(Permission.VOBikeSeatbelt))
            {
                menu.AddMenuItem(vehicleBikeSeatbelt);
            }
            if (IsAllowed(Permission.VOSpeedLimiter)) // SPEED LIMITER
            {
                menu.AddMenuItem(speedLimiter);
            }
            if (IsAllowed(Permission.VOTorqueMultiplier))
            {
                menu.AddMenuItem(torqueEnabled); // TORQUE ENABLED
                menu.AddMenuItem(torqueMultiplier); // TORQUE LIST
            }
            if (IsAllowed(Permission.VOPowerMultiplier))
            {
                menu.AddMenuItem(powerEnabled); // POWER ENABLED
                menu.AddMenuItem(powerMultiplier); // POWER LIST
            }
            if (IsAllowed(Permission.VODisableTurbulence))
            {
                menu.AddMenuItem(vehicleNoTurbulence);
                menu.AddMenuItem(vehicleNoTurbulenceHeli);
            }
            if (IsAllowed(Permission.VOAnchorBoat))
            {
                menu.AddMenuItem(vehicleSetAnchor);
            }
            if (IsAllowed(Permission.VOFlip)) // FLIP VEHICLE
            {
                menu.AddMenuItem(flipVehicle);
            }
            if (IsAllowed(Permission.VOAlarm)) // TOGGLE VEHICLE ALARM
            {
                menu.AddMenuItem(vehicleAlarm);
            }
            if (IsAllowed(Permission.VOCycleSeats)) // CYCLE THROUGH VEHICLE SEATS
            {
                menu.AddMenuItem(cycleSeats);
            }
            if (IsAllowed(Permission.VOLights)) // VEHICLE LIGHTS LIST
            {
                menu.AddMenuItem(vehicleLights);
            }
            if (IsAllowed(Permission.VOFixOrDestroyTires))
            {
                menu.AddMenuItem(vehicleTiresList);
            }
            if (IsAllowed(Permission.VODestroyEngine))
            {
                menu.AddMenuItem(destroyEngine);
            }
            if (IsAllowed(Permission.VOFixEngine))
            {
                menu.AddMenuItem(fixEngine);
            }
            if (IsAllowed(Permission.VOFreeze)) // FREEZE VEHICLE
            {
                menu.AddMenuItem(vehicleFreeze);
            }
            if (IsAllowed(Permission.VOInvisible)) // MAKE VEHICLE INVISIBLE
            {
                menu.AddMenuItem(vehicleInvisible);
            }
            if (IsAllowed(Permission.VOEngineAlwaysOn)) // LEAVE ENGINE RUNNING
            {
                menu.AddMenuItem(vehicleEngineAO);
            }
            if (IsAllowed(Permission.VOInfiniteFuel)) // INFINITE FUEL
            {
                menu.AddMenuItem(infiniteFuel);
            }
            // always allowed
            menu.AddMenuItem(showHealth); // SHOW VEHICLE HEALTH

            // I don't really see why would you want to disable this so I will not add useless permissions
            menu.AddMenuItem(radioStations);

            if (IsAllowed(Permission.VONoSiren) && !GetSettingsBool(Setting.vmenu_use_els_compatibility_mode)) // DISABLE SIREN
            {
                menu.AddMenuItem(vehicleNoSiren);
            }
            if (IsAllowed(Permission.VONoHelmet)) // DISABLE BIKE HELMET
            {
                menu.AddMenuItem(vehicleNoBikeHelmet);
            }
            if (IsAllowed(Permission.VOFlashHighbeamsOnHonk)) // FLASH HIGHBEAMS ON HONK
            {
                menu.AddMenuItem(highbeamsOnHonk);
            }

            if (IsAllowed(Permission.VODelete)) // DELETE VEHICLE
            {
                menu.AddMenuItem(deleteBtn);
            }
            #endregion

            #region delete vehicle handle stuff
            DeleteConfirmMenu.AddMenuItem(deleteNoBtn);
            DeleteConfirmMenu.AddMenuItem(deleteYesBtn);
            DeleteConfirmMenu.OnItemSelect += (sender, item, index) =>
            {
                if (item == deleteNoBtn)
                {
                    DeleteConfirmMenu.GoBack();
                }
                else
                {
                    var veh = GetVehicle();
                    if (veh != null && veh.Exists() && GetVehicle().Driver == Game.PlayerPed)
                    {
                        SetVehicleHasBeenOwnedByPlayer(veh.Handle, false);
                        SetEntityAsMissionEntity(veh.Handle, false, false);
                        veh.Delete();
                    }
                    else
                    {
                        if (!Game.PlayerPed.IsInVehicle())
                        {
                            Notify.Alert(CommonErrors.NoVehicle);
                        }
                        else
                        {
                            Notify.Alert("You need to be in the driver's seat if you want to delete a vehicle.");
                        }

                    }
                    DeleteConfirmMenu.GoBack();
                    menu.GoBack();
                }
            };
            #endregion

            #region Bind Submenus to their buttons.
            MenuController.BindMenuItem(menu, VehicleModMenu, modMenuBtn);
            MenuController.BindMenuItem(menu, VehicleHandlingMenu, handlingMenuBtn);
            MenuController.BindMenuItem(menu, VehicleDoorsMenu, doorsMenuBtn);
            MenuController.BindMenuItem(menu, VehicleWindowsMenu, windowsMenuBtn);
            MenuController.BindMenuItem(menu, VehicleComponentsMenu, componentsMenuBtn);
            MenuController.BindMenuItem(menu, VehicleEngineSoundMenu, engineSoundMenuBtn);
            MenuController.BindMenuItem(menu, VehicleLiveriesMenu, liveriesMenuBtn);
            MenuController.BindMenuItem(menu, VehicleColorsMenu, colorsMenuBtn);
            MenuController.BindMenuItem(menu, DeleteConfirmMenu, deleteBtn);
            #endregion

            #region Handle button presses
            // Manage button presses.
            menu.OnItemSelect += (sender, item, index) =>
            {
                if (item == deleteBtn) // reset the index so that "no" / "cancel" will always be selected by default.
                {
                    DeleteConfirmMenu.RefreshIndex();
                }
                // If the player is actually in a vehicle, continue.
                if (GetVehicle() != null && GetVehicle().Exists())
                {
                    // Create a vehicle object.
                    var vehicle = GetVehicle();

                    // Check if the player is the driver of the vehicle, if so, continue.
                    if (vehicle.GetPedOnSeat(VehicleSeat.Driver) == Game.PlayerPed)
                    {
                        // Repair vehicle.
                        if (item == fixVehicle)
                        {
                            vehicle.Repair();
                        }
                        // Clean vehicle.
                        else if (item == cleanVehicle)
                        {
                            vehicle.Wash();
                        }
                        // Flip vehicle.
                        else if (item == flipVehicle)
                        {
                            SetVehicleOnGroundProperly(vehicle.Handle);
                        }
                        // Toggle alarm.
                        else if (item == vehicleAlarm)
                        {
                            ToggleVehicleAlarm(vehicle);
                        }
                        // Toggle engine
                        else if (item == toggleEngine)
                        {
                            SetVehicleEngineOn(vehicle.Handle, !vehicle.IsEngineRunning, false, true);
                        }
                        // Set license plate text
                        else if (item == setLicensePlateText)
                        {
                            SetLicensePlateCustomText();
                        }
                        // Make vehicle invisible.
                        else if (item == vehicleInvisible)
                        {
                            if (vehicle.IsVisible)
                            {
                                // Check the visibility of all peds inside before setting the vehicle as invisible.
                                var visiblePeds = new Dictionary<Ped, bool>();
                                foreach (var p in vehicle.Occupants)
                                {
                                    visiblePeds.Add(p, p.IsVisible);
                                }

                                // Set the vehicle invisible or invincivble.
                                vehicle.IsVisible = !vehicle.IsVisible;

                                // Restore visibility for each ped.
                                foreach (var pe in visiblePeds)
                                {
                                    pe.Key.IsVisible = pe.Value;
                                }
                            }
                            else
                            {
                                // Set the vehicle invisible or invincivble.
                                vehicle.IsVisible = !vehicle.IsVisible;
                            }
                        }
                        // Destroy vehicle engine
                        else if (item == destroyEngine)
                        {
                            SetVehicleEngineHealth(vehicle.Handle, -4000);
                        }
                        else if (item == fixEngine)
                        {
                            SetVehicleEngineHealth(vehicle.Handle, 1000);
                        }
                    }

                    // If the player is not the driver seat and a button other than the option below (cycle seats) was pressed, notify them.
                    else if (item != cycleSeats)
                    {
                        Notify.Error("You have to be the driver of a vehicle to access this menu!", true, false);
                    }

                    // Cycle vehicle seats
                    if (item == cycleSeats)
                    {
                        CycleThroughSeats();
                    }
                }
            };
            #endregion

            #region Handle checkbox changes.
            menu.OnCheckboxChange += (sender, item, index, _checked) =>
            {
                // Create a vehicle object.
                var vehicle = GetVehicle();

                if (item == vehicleGod) // God Mode Toggled
                {
                    VehicleGodMode = _checked;
                }
                else if (item == vehicleFreeze) // Freeze Vehicle Toggled
                {
                    VehicleFrozen = _checked;
                    if (!_checked)
                    {
                        if (vehicle != null && vehicle.Exists())
                        {
                            FreezeEntityPosition(vehicle.Handle, false);
                        }
                    }
                }
                else if (item == torqueEnabled) // Enable Torque Multiplier Toggled
                {
                    VehicleTorqueMultiplier = _checked;
                }
                else if (item == powerEnabled) // Enable Power Multiplier Toggled
                {
                    VehiclePowerMultiplier = _checked;
                    if (_checked)
                    {
                        if (vehicle != null && vehicle.Exists())
                        {
                            SetVehicleEnginePowerMultiplier(vehicle.Handle, VehiclePowerMultiplierAmount);
                        }
                    }
                    else
                    {
                        if (vehicle != null && vehicle.Exists())
                        {
                            SetVehicleEnginePowerMultiplier(vehicle.Handle, 1f);
                        }
                    }
                }
                else if (item == vehicleEngineAO) // Leave Engine Running (vehicle always on) Toggled
                {
                    VehicleEngineAlwaysOn = _checked;
                }
                else if (item == showHealth) // show vehicle health on screen.
                {
                    VehicleShowHealth = _checked;
                }
                else if (item == vehicleNoSiren) // Disable Siren Toggled
                {
                    VehicleNoSiren = _checked;
                    if (vehicle != null && vehicle.Exists())
                    {
                        vehicle.IsSirenSilent = _checked;
                    }
                }
                else if (item == vehicleNoBikeHelmet) // No Helemet Toggled
                {
                    VehicleNoBikeHelemet = _checked;
                }
                else if (item == highbeamsOnHonk)
                {
                    FlashHighbeamsOnHonk = _checked;
                }
                else if (item == vehicleNoTurbulence)
                {
                    DisablePlaneTurbulence = _checked;
                    if (vehicle != null && vehicle.Exists() && vehicle.Model.IsPlane)
                    {
                        if (MainMenu.VehicleOptionsMenu.DisablePlaneTurbulence)
                        {
                            SetPlaneTurbulenceMultiplier(vehicle.Handle, 0f);
                        }
                        else
                        {
                            SetPlaneTurbulenceMultiplier(vehicle.Handle, 1.0f);
                        }
                    }
                }
                else if (item == vehicleNoTurbulenceHeli)
                {
                    DisableHelicopterTurbulence = _checked;
                    if (vehicle != null && vehicle.Exists() && vehicle.Model.IsHelicopter)
                    {
                        if (MainMenu.VehicleOptionsMenu.DisableHelicopterTurbulence)
                        {
                            SetHeliTurbulenceScalar(vehicle.Handle, 0f);
                        }
                        else
                        {
                            SetHeliTurbulenceScalar(vehicle.Handle, 1f);
                        }
                    }
                }
                else if (item == vehicleSetAnchor)
                {
                    AnchorBoat = _checked;
                    if (vehicle != null && vehicle.Exists())
                    {
                        if (vehicle.Model.IsBoat)
                        {
                            if (CanAnchorBoatHere(vehicle.Handle))
                            {
                                if (MainMenu.VehicleOptionsMenu.AnchorBoat)
                                {
                                    SetBoatAnchor(vehicle.Handle, true);
                                    SetBoatFrozenWhenAnchored(vehicle.Handle, true);
                                    SetForcedBoatLocationWhenAnchored(vehicle.Handle, true);
                                }
                                else
                                {
                                    SetBoatAnchor(vehicle.Handle, false);
                                    SetBoatFrozenWhenAnchored(vehicle.Handle, false);
                                    SetForcedBoatLocationWhenAnchored(vehicle.Handle, false);
                                }
                            }
                            else
                            {
                                vehicleSetAnchor.Checked = false;
                                Notify.Error("You cannot anchor your boat here!");
                            }
                        }
                        else
                        {
                            vehicleSetAnchor.Checked = false;
                            Notify.Error("You are not currently in a boat!");
                        }
                    }
                }
                else if (item == vehicleNeverDirty)
                {
                    VehicleNeverDirty = _checked;
                }
                else if (item == vehicleBikeSeatbelt)
                {
                    VehicleBikeSeatbelt = _checked;
                }
                else if (item == infiniteFuel)
                {
                    VehicleInfiniteFuel = _checked;
                    
                    var actionData = new Dictionary<string, object>
                    {
                        ["enabled"] = _checked
                    };
                    TriggerEvent("vMenu:Integrations:Action", "infinitefuel", actionData);
                }
            };
            #endregion

            #region Handle List Changes.
            // Handle list changes.
            menu.OnListIndexChange += (sender, item, oldIndex, newIndex, itemIndex) =>
            {
                if (GetVehicle() != null && GetVehicle().Exists())
                {
                    var veh = GetVehicle();
                    // If the torque multiplier changed. Change the torque multiplier to the new value.
                    if (item == torqueMultiplier)
                    {
                        // Get the selected value and remove the "x" in the string with nothing.
                        var value = torqueMultiplierList[newIndex].ToString().Replace("x", "");
                        // Convert the value to a float and set it as a public variable.
                        VehicleTorqueMultiplierAmount = float.Parse(value);
                    }
                    // If the power multiplier is changed. Change the power multiplier to the new value.
                    else if (item == powerMultiplier)
                    {
                        // Get the selected value. Remove the "x" from the string.
                        var value = powerMultiplierList[newIndex].ToString().Replace("x", "");
                        // Conver the string into a float and set it to be the value of the public variable.
                        VehiclePowerMultiplierAmount = float.Parse(value);
                        if (VehiclePowerMultiplier)
                        {
                            SetVehicleEnginePowerMultiplier(veh.Handle, VehiclePowerMultiplierAmount);
                        }
                    }
                    else if (item == setLicensePlateType)
                    {
                        // Set the license plate style.
                        switch (newIndex)
                        {
                            case 0:
                                veh.Mods.LicensePlateStyle = LicensePlateStyle.BlueOnWhite1;
                                break;
                            case 1:
                                veh.Mods.LicensePlateStyle = LicensePlateStyle.BlueOnWhite2;
                                break;
                            case 2:
                                veh.Mods.LicensePlateStyle = LicensePlateStyle.BlueOnWhite3;
                                break;
                            case 3:
                                veh.Mods.LicensePlateStyle = LicensePlateStyle.YellowOnBlue;
                                break;
                            case 4:
                                veh.Mods.LicensePlateStyle = LicensePlateStyle.YellowOnBlack;
                                break;
                            case 5:
                                veh.Mods.LicensePlateStyle = LicensePlateStyle.NorthYankton;
                                break;
                            case 6:
                                veh.Mods.LicensePlateStyle = LicensePlateStyle.ECola;
                                break;
                            case 7:
                                veh.Mods.LicensePlateStyle = LicensePlateStyle.LasVenturas;
                                break;
                            case 8:
                                veh.Mods.LicensePlateStyle = LicensePlateStyle.LibertyCity;
                                break;
                            case 9:
                                veh.Mods.LicensePlateStyle = LicensePlateStyle.LSCarMeet;
                                break;
                            case 10:
                                veh.Mods.LicensePlateStyle = LicensePlateStyle.LSPanic;
                                break;
                            case 11:
                                veh.Mods.LicensePlateStyle = LicensePlateStyle.LSPounders;
                                break;
                            case 12:
                                veh.Mods.LicensePlateStyle = LicensePlateStyle.Sprunk;
                                break;
                            default:
                                break;
                        }
                    }
                }
            };
            #endregion

            #region Handle List Items Selected
            menu.OnListItemSelect += async (sender, item, listIndex, itemIndex) =>
            {
                // Set dirt level
                if (item == setDirtLevel)
                {
                    if (Game.PlayerPed.IsInVehicle())
                    {
                        GetVehicle().DirtLevel = float.Parse(listIndex.ToString());
                    }
                    else
                    {
                        Notify.Error(CommonErrors.NoVehicle);
                    }
                }
                // Toggle vehicle lights
                else if (item == vehicleLights)
                {
                    if (Game.PlayerPed.IsInVehicle())
                    {
                        var veh = GetVehicle();
                        // We need to do % 4 because this seems to be some sort of flags system. For a taxi, this function returns 65, 66, etc.
                        // So % 4 takes care of that.
                        var state = GetVehicleIndicatorLights(veh.Handle) % 4; // 0 = none, 1 = left, 2 = right, 3 = both

                        if (listIndex == 0) // Hazard lights
                        {
                            if (state != 3) // either all lights are off, or one of the two (left/right) is off.
                            {
                                SetVehicleIndicatorLights(veh.Handle, 1, true); // left on
                                SetVehicleIndicatorLights(veh.Handle, 0, true); // right on
                            }
                            else // both are on.
                            {
                                SetVehicleIndicatorLights(veh.Handle, 1, false); // left off
                                SetVehicleIndicatorLights(veh.Handle, 0, false); // right off
                            }
                        }
                        else if (listIndex == 1) // left indicator
                        {
                            if (state != 1) // Left indicator is (only) off
                            {
                                SetVehicleIndicatorLights(veh.Handle, 1, true); // left on
                                SetVehicleIndicatorLights(veh.Handle, 0, false); // right off
                            }
                            else
                            {
                                SetVehicleIndicatorLights(veh.Handle, 1, false); // left off
                                SetVehicleIndicatorLights(veh.Handle, 0, false); // right off
                            }
                        }
                        else if (listIndex == 2) // right indicator
                        {
                            if (state != 2) // Right indicator (only) is off
                            {
                                SetVehicleIndicatorLights(veh.Handle, 1, false); // left off
                                SetVehicleIndicatorLights(veh.Handle, 0, true); // right on
                            }
                            else
                            {
                                SetVehicleIndicatorLights(veh.Handle, 1, false); // left off
                                SetVehicleIndicatorLights(veh.Handle, 0, false); // right off
                            }
                        }
                        else if (listIndex == 3) // Interior lights
                        {
                            SetVehicleInteriorlight(veh.Handle, !IsVehicleInteriorLightOn(veh.Handle));
                            //CommonFunctions.Log("Something cool here.");
                        }
                        //else if (listIndex == 4) // taxi light
                        //{
                        //    veh.IsTaxiLightOn = !veh.IsTaxiLightOn;
                        //    //    SetTaxiLights(veh, true);
                        //    //    SetTaxiLights(veh, false);
                        //    //    //CommonFunctions.Log(IsTaxiLightOn(veh).ToString());
                        //    //    //SetTaxiLights(veh, true);
                        //    //    //CommonFunctions.Log(IsTaxiLightOn(veh).ToString());
                        //    //    //SetTaxiLights(veh, false);
                        //    //    //SetTaxiLights(veh, !IsTaxiLightOn(veh));
                        //    //    CommonFunctions.Log
                        //}
                        else if (listIndex == 4) // helicopter spotlight
                        {
                            SetVehicleSearchlight(veh.Handle, !IsVehicleSearchlightOn(veh.Handle), true);
                        }
                    }
                    else
                    {
                        Notify.Error(CommonErrors.NoVehicle);
                    }
                }
                // Speed Limiter
                else if (item == speedLimiter)
                {
                    if (Game.PlayerPed.IsInVehicle())
                    {
                        var vehicle = GetVehicle();

                        if (vehicle != null && vehicle.Exists())
                        {
                            if (listIndex == 0) // Set
                            {
                                SetEntityMaxSpeed(vehicle.Handle, 500.01f);
                                SetEntityMaxSpeed(vehicle.Handle, vehicle.Speed);

                                if (ShouldUseMetricMeasurements()) // kph
                                {
                                    Notify.Info($"Vehicle speed is now limited to ~b~{Math.Round(vehicle.Speed * 3.6f, 1)} KPH~s~.");
                                }
                                else // mph
                                {
                                    Notify.Info($"Vehicle speed is now limited to ~b~{Math.Round(vehicle.Speed * 2.237f, 1)} MPH~s~.");
                                }

                            }
                            else if (listIndex == 1) // Reset
                            {
                                SetEntityMaxSpeed(vehicle.Handle, 500.01f); // Default max speed seemingly for all vehicles.
                                Notify.Info("Vehicle speed is now no longer limited.");
                            }
                            else if (listIndex == 2) // custom speed
                            {
                                var inputSpeed = await GetUserInput("Enter a speed (in meters/sec)", "20.0", 5);
                                if (!string.IsNullOrEmpty(inputSpeed))
                                {
                                    if (float.TryParse(inputSpeed, out var outFloat))
                                    {
                                        //vehicle.MaxSpeed = outFloat;
                                        SetEntityMaxSpeed(vehicle.Handle, 500.01f);
                                        await BaseScript.Delay(0);
                                        SetEntityMaxSpeed(vehicle.Handle, outFloat + 0.01f);
                                        if (ShouldUseMetricMeasurements()) // kph
                                        {
                                            Notify.Info($"Vehicle speed is now limited to ~b~{Math.Round(outFloat * 3.6f, 1)} KPH~s~.");
                                        }
                                        else // mph
                                        {
                                            Notify.Info($"Vehicle speed is now limited to ~b~{Math.Round(outFloat * 2.237f, 1)} MPH~s~.");
                                        }
                                    }
                                    else if (int.TryParse(inputSpeed, out var outInt))
                                    {
                                        SetEntityMaxSpeed(vehicle.Handle, 500.01f);
                                        await BaseScript.Delay(0);
                                        SetEntityMaxSpeed(vehicle.Handle, outInt + 0.01f);
                                        if (ShouldUseMetricMeasurements()) // kph
                                        {
                                            Notify.Info($"Vehicle speed is now limited to ~b~{Math.Round(outInt * 3.6f, 1)} KPH~s~.");
                                        }
                                        else // mph
                                        {
                                            Notify.Info($"Vehicle speed is now limited to ~b~{Math.Round(outInt * 2.237f, 1)} MPH~s~.");
                                        }
                                    }
                                    else
                                    {
                                        Notify.Error("This is not a valid number. Please enter a valid speed in meters per second.");
                                    }
                                }
                                else
                                {
                                    Notify.Error(CommonErrors.InvalidInput);
                                }
                            }
                        }
                    }
                }
                else if (item == vehicleTiresList)
                {
                    //bool fix = item == vehicleTiresList;

                    var veh = GetVehicle();
                    if (veh != null && veh.Exists())
                    {
                        if (Game.PlayerPed == veh.Driver)
                        {
                            if (listIndex == 0)
                            {
                                if (IsVehicleTyreBurst(veh.Handle, 0, false))
                                {
                                    for (var i = 0; i < 8; i++)
                                    {
                                        SetVehicleTyreFixed(veh.Handle, i);
                                    }
                                    Notify.Success("All vehicle tyres have been fixed.");
                                }
                                else
                                {
                                    for (var i = 0; i < 8; i++)
                                    {
                                        SetVehicleTyreBurst(veh.Handle, i, false, 1f);
                                    }
                                    Notify.Success("All vehicle tyres have been destroyed.");
                                }
                            }
                            else
                            {
                                var index = listIndex - 1;
                                if (IsVehicleTyreBurst(veh.Handle, index, false))
                                {
                                    SetVehicleTyreFixed(veh.Handle, index);
                                    Notify.Success($"Vehicle tyre #{listIndex} has been fixed.");
                                }
                                else
                                {
                                    SetVehicleTyreBurst(veh.Handle, index, false, 1f);
                                    Notify.Success($"Vehicle tyre #{listIndex} has been destroyed.");
                                }
                            }
                        }
                        else
                        {
                            Notify.Error(CommonErrors.NeedToBeTheDriver);
                        }
                    }
                    else
                    {
                        Notify.Error(CommonErrors.NoVehicle);
                    }
                }
                else if (item == radioStations)
                {
                    var newStation = (RadioStation)Enum.GetValues(typeof(RadioStation)).GetValue(listIndex);

                    var veh = GetVehicle();
                    if (veh != null && veh.Exists())
                    {
                        veh.RadioStation = newStation;
                    }

                    UserDefaults.VehicleDefaultRadio = (int)newStation;
                }
            };
            #endregion

            #region Vehicle Colors Submenu Stuff
            // color customization menu
            var customizeColorMenu = new Menu("Vehicle Colors", "Customize Colors");
            MenuController.AddSubmenu(VehicleColorsMenu, customizeColorMenu);

            var colorsCustomizationBtn = new MenuItem("Customize Colors") { Label = "→→→" };
            VehicleColorsMenu.AddMenuItem(colorsCustomizationBtn);
            MenuController.BindMenuItem(VehicleColorsMenu, customizeColorMenu, colorsCustomizationBtn);

            // primary menu
            var primaryColorsMenu = new Menu("Vehicle Colors", "Primary Colors");
            MenuController.AddSubmenu(customizeColorMenu, primaryColorsMenu);

            var primaryColorsBtn = new MenuItem("Primary Color") { Label = "→→→" };
            customizeColorMenu.AddMenuItem(primaryColorsBtn);
            MenuController.BindMenuItem(customizeColorMenu, primaryColorsMenu, primaryColorsBtn);

            // secondary menu
            var secondaryColorsMenu = new Menu("Vehicle Colors", "Secondary Colors");
            MenuController.AddSubmenu(customizeColorMenu, secondaryColorsMenu);

            var secondaryColorsBtn = new MenuItem("Secondary Color") { Label = "→→→" };
            customizeColorMenu.AddMenuItem(secondaryColorsBtn);
            MenuController.BindMenuItem(customizeColorMenu, secondaryColorsMenu, secondaryColorsBtn);

            var presetColorsBtn = new MenuListItem("Preset Colors", [], 0);
            customizeColorMenu.AddMenuItem(presetColorsBtn);

            var chrome = new MenuItem("Chrome");
            customizeColorMenu.AddMenuItem(chrome);

            // color lists
            var classic = new List<string>();
            var matte = new List<string>();
            var metals = new List<string>();
            var util = new List<string>();
            var worn = new List<string>();
            var chameleon = new List<string>();
            var wheelColors = new List<string>() { "Default Alloy" };

            // Just quick and dirty solution to put this in a new enclosed section so that we can still use 'i' as a counter in the other code parts.
            {
                var i = 0;
                foreach (var vc in VehicleData.ClassicColors)
                {
                    classic.Add($"{GetLabelText(vc.label)} ({i + 1}/{VehicleData.ClassicColors.Count})");
                    i++;
                }

                i = 0;
                foreach (var vc in VehicleData.MatteColors)
                {
                    matte.Add($"{GetLabelText(vc.label)} ({i + 1}/{VehicleData.MatteColors.Count})");
                    i++;
                }

                i = 0;
                foreach (var vc in VehicleData.MetalColors)
                {
                    metals.Add($"{GetLabelText(vc.label)} ({i + 1}/{VehicleData.MetalColors.Count})");
                    i++;
                }

                i = 0;
                foreach (var vc in VehicleData.UtilColors)
                {
                    util.Add($"{GetLabelText(vc.label)} ({i + 1}/{VehicleData.UtilColors.Count})");
                    i++;
                }

                i = 0;
                foreach (var vc in VehicleData.WornColors)
                {
                    worn.Add($"{GetLabelText(vc.label)} ({i + 1}/{VehicleData.WornColors.Count})");
                    i++;
                }

                if (GetSettingsBool(Setting.vmenu_using_chameleon_colours))
                {
                    i = 0;
                    foreach (var vc in VehicleData.ChameleonColors)
                    {
                        chameleon.Add($"{GetLabelText(vc.label)} ({i + 1}/{VehicleData.ChameleonColors.Count})");
                        i++;
                    }
                }

                wheelColors.AddRange(classic);
            }

            var wheelColorsList = new MenuListItem("Wheel Color", wheelColors, 0);
            var dashColorList = new MenuListItem("Dashboard Color", classic, 0);
            var intColorList = new MenuListItem("Interior / Trim Color", classic, 0);
            var vehicleEnveffScale = new MenuSliderItem("Vehicle Enveff Scale", "This works on certain vehicles only, like the besra for example. It 'fades' certain paint layers.", 0, 20, 10, true);

            VehicleColorsMenu.AddMenuItem(vehicleEnveffScale);

            VehicleColorsMenu.OnSliderPositionChange += (m, sliderItem, oldPosition, newPosition, itemIndex) =>
            {
                var veh = GetVehicle();
                if (veh != null && veh.Driver == Game.PlayerPed && !veh.IsDead)
                {
                    if (sliderItem == vehicleEnveffScale)
                    {
                        SetVehicleEnveffScale(veh.Handle, newPosition / 20f);
                    }
                }
                else
                {
                    Notify.Error("You need to be the driver of a driveable vehicle to change this slider.");
                }
            };

            VehicleColorsMenu.AddMenuItem(dashColorList);
            VehicleColorsMenu.AddMenuItem(intColorList);
            VehicleColorsMenu.AddMenuItem(wheelColorsList);

            VehicleColorsMenu.OnListIndexChange += HandleListIndexChanges;

            void HandleListIndexChanges(Menu sender, MenuListItem listItem, int oldIndex, int newIndex, int itemIndex)
            {
                var veh = GetVehicle();
                if (veh != null && veh.Exists() && !veh.IsDead && veh.Driver == Game.PlayerPed)
                {
                    var primaryColor = 0;
                    var secondaryColor = 0;
                    var pearlColor = 0;
                    var wheelColor = 0;
                    var dashColor = 0;
                    var intColor = 0;

                    GetVehicleColours(veh.Handle, ref primaryColor, ref secondaryColor);
                    GetVehicleExtraColours(veh.Handle, ref pearlColor, ref wheelColor);
                    GetVehicleDashboardColour(veh.Handle, ref dashColor);
                    GetVehicleInteriorColour(veh.Handle, ref intColor);

                    if (sender == primaryColorsMenu)
                    {
                        if (itemIndex == 2)
                        {
                            pearlColor = VehicleData.ClassicColors[newIndex].id;
                        }
                        else
                        {
                            pearlColor = 0;
                        }

                        switch (itemIndex)
                        {
                            case 0:
                            case 1:
                            case 2:
                                primaryColor = VehicleData.ClassicColors[newIndex].id;
                                break;
                            case 3:
                                primaryColor = VehicleData.MatteColors[newIndex].id;
                                break;
                            case 4:
                                primaryColor = VehicleData.MetalColors[newIndex].id;
                                break;
                            case 5:
                                primaryColor = VehicleData.UtilColors[newIndex].id;
                                break;
                            case 6:
                                primaryColor = VehicleData.WornColors[newIndex].id;
                                break;
                        }

                        if (GetSettingsBool(Setting.vmenu_using_chameleon_colours))
                        {
                            if (itemIndex == 7)
                            {
                                primaryColor = VehicleData.ChameleonColors[newIndex].id;
                                secondaryColor = VehicleData.ChameleonColors[newIndex].id;

                                SetVehicleModKit(veh.Handle, 0);
                            }
                        }

                        ClearVehicleCustomPrimaryColour(veh.Handle);
                        SetVehicleColours(veh.Handle, primaryColor, secondaryColor);
                    }
                    else if (sender == secondaryColorsMenu)
                    {
                        switch (itemIndex)
                        {
                            case 0:
                            case 1:
                                pearlColor = VehicleData.ClassicColors[newIndex].id;
                                break;
                            case 2:
                            case 3:
                                secondaryColor = VehicleData.ClassicColors[newIndex].id;
                                break;
                            case 4:
                                secondaryColor = VehicleData.MatteColors[newIndex].id;
                                break;
                            case 5:
                                secondaryColor = VehicleData.MetalColors[newIndex].id;
                                break;
                            case 6:
                                secondaryColor = VehicleData.UtilColors[newIndex].id;
                                break;
                            case 7:
                                secondaryColor = VehicleData.WornColors[newIndex].id;
                                break;
                        }

                        ClearVehicleCustomSecondaryColour(veh.Handle);
                        SetVehicleColours(veh.Handle, primaryColor, secondaryColor);
                    }
                    else if (sender == VehicleColorsMenu)
                    {
                        if (listItem == wheelColorsList)
                        {
                            if (newIndex == 0)
                            {
                                wheelColor = 156; // default alloy color.
                            }
                            else
                            {
                                wheelColor = VehicleData.ClassicColors[newIndex - 1].id;
                            }
                        }
                        else if (listItem == dashColorList)
                        {
                            dashColor = VehicleData.ClassicColors[newIndex].id;
                            // sadly these native names are mixed up :/ but ofc it's impossible to fix due to backwards compatibility.
                            // this should actually be called SetVehicleDashboardColour
                            SetVehicleInteriorColour(veh.Handle, dashColor);
                        }
                        else if (listItem == intColorList)
                        {
                            intColor = VehicleData.ClassicColors[newIndex].id;
                            // sadly these native names are mixed up :/ but ofc it's impossible to fix due to backwards compatibility.
                            // this should actually be called SetVehicleInteriorColour
                            SetVehicleDashboardColour(veh.Handle, intColor);
                        }
                    }

                    SetVehicleExtraColours(veh.Handle, pearlColor, wheelColor);
                }
                else
                {
                    Notify.Error("You need to be the driver of a vehicle in order to change the vehicle colors.");
                }
            }

            async void HandleItemSelect(Menu menu, MenuItem menuItem, int itemIndex)
            {
                Vehicle veh = GetVehicle();
                if (veh != null && veh.Exists() && !veh.IsDead && veh.Driver == Game.PlayerPed)
                {   

                    int vehicleType = (menu == primaryColorsMenu) ? 1 : 2;

                    var chosenColour = await GetUserColourInput(vehicleType);
                    if (string.IsNullOrEmpty(chosenColour))
                    {
                        return;
                    }

                    chosenColour = chosenColour.Replace("#", "");
                    int r = Convert.ToInt32(chosenColour.Substring(0, 2), 16);
                    int g = Convert.ToInt32(chosenColour.Substring(2, 2), 16);
                    int b = Convert.ToInt32(chosenColour.Substring(4, 2), 16);

                    if (vehicleType == 1)
                    {
                        SetVehicleCustomPrimaryColour(veh.Handle, r, g, b);
                    }
                    else
                    {
                        SetVehicleCustomSecondaryColour(veh.Handle, r, g, b);
                    }
                }
            }

            for (var i = 0; i < 2; i++)
            {
                var customColour = new MenuItem("Custom RGB") { Label = "→→→" };
                var pearlescentList = new MenuListItem("Pearlescent", classic, 0);
                var classicList = new MenuListItem("Classic", classic, 0);
                var metallicList = new MenuListItem("Metallic", classic, 0);
                var matteList = new MenuListItem("Matte", matte, 0);
                var metalList = new MenuListItem("Metals", metals, 0);
                var utilList = new MenuListItem("Util", util, 0);
                var wornList = new MenuListItem("Worn", worn, 0);

                if (i == 0)
                {
                    primaryColorsMenu.AddMenuItem(customColour);
                    primaryColorsMenu.AddMenuItem(classicList);
                    primaryColorsMenu.AddMenuItem(metallicList);
                    primaryColorsMenu.AddMenuItem(matteList);
                    primaryColorsMenu.AddMenuItem(metalList);
                    primaryColorsMenu.AddMenuItem(utilList);
                    primaryColorsMenu.AddMenuItem(wornList);

                    if (GetSettingsBool(Setting.vmenu_using_chameleon_colours))
                    {
                        var chameleonList = new MenuListItem("Chameleon", chameleon, 0);

                        primaryColorsMenu.AddMenuItem(chameleonList);
                    }

                    primaryColorsMenu.OnListIndexChange += HandleListIndexChanges;
                    primaryColorsMenu.OnItemSelect += HandleItemSelect;
                }
                else
                {
                    secondaryColorsMenu.AddMenuItem(customColour);
                    secondaryColorsMenu.AddMenuItem(pearlescentList);
                    secondaryColorsMenu.AddMenuItem(classicList);
                    secondaryColorsMenu.AddMenuItem(metallicList);
                    secondaryColorsMenu.AddMenuItem(matteList);
                    secondaryColorsMenu.AddMenuItem(metalList);
                    secondaryColorsMenu.AddMenuItem(utilList);
                    secondaryColorsMenu.AddMenuItem(wornList);

                    secondaryColorsMenu.OnListIndexChange += HandleListIndexChanges;
                    secondaryColorsMenu.OnItemSelect += HandleItemSelect;
                }
            }

            customizeColorMenu.OnMenuOpen += (_) =>
            {
                int numVehColors = GetNumberOfVehicleColours(GetVehicle().Handle);

                if (numVehColors == 0)
                {
                    presetColorsBtn.Enabled = false;
                    presetColorsBtn.ListItems = ["No Preset Colors"];
                    presetColorsBtn.ListIndex = 0;
                    return;
                }

                List<string> colorOptions = [];

                presetColorsBtn.Enabled = true;

                for (int i = 0; i < numVehColors; i++)
                {
                    colorOptions.Add($"Preset Color #{i + 1}");
                }

                int currentColor = GetVehicleColourCombination(GetVehicle().Handle);

                presetColorsBtn.ListItems = colorOptions;
                presetColorsBtn.ListIndex = currentColor < 0 ? 0 : currentColor;
            };

            customizeColorMenu.OnItemSelect += (_, item, _) =>
            {
                var veh = GetVehicle();
                if (veh != null && veh.Exists() && !veh.IsDead && veh.Driver == Game.PlayerPed)
                {
                    if (item == chrome)
                    {
                        SetVehicleColours(veh.Handle, 120, 120); // chrome is index 120
                    }
                }
                else
                {
                    Notify.Error("You need to be the driver of a driveable vehicle to change this.");
                }
            };

            customizeColorMenu.OnListItemSelect += (_, _, index, _) => ChangeVehiclePresetColor(index);

            customizeColorMenu.OnListIndexChange += (_, _, _, index, _) => ChangeVehiclePresetColor(index);

            void ChangeVehiclePresetColor(int index)
            {
                var veh = GetVehicle();
                if (veh != null && veh.Exists() && !veh.IsDead && veh.Driver == Game.PlayerPed)
                {
                    SetVehicleColourCombination(veh.Handle, index);
                }
                else
                {
                    Notify.Error("You need to be the driver of a driveable vehicle to change this.");
                }
            }
            #endregion

            #region Vehicle Doors Submenu Stuff
            var openAll = new MenuItem("Open All Doors", "Open all vehicle doors.");
            var closeAll = new MenuItem("Close All Doors", "Close all vehicle doors.");
            var LF = new MenuItem("Left Front Door", "Open/close the left front door.");
            var RF = new MenuItem("Right Front Door", "Open/close the right front door.");
            var LR = new MenuItem("Left Rear Door", "Open/close the left rear door.");
            var RR = new MenuItem("Right Rear Door", "Open/close the right rear door.");
            var HD = new MenuItem("Hood", "Open/close the hood.");
            var TR = new MenuItem("Trunk", "Open/close the trunk.");
            var E1 = new MenuItem("Extra 1", "Open/close the extra door (#1). Note this door is not present on most vehicles.");
            var E2 = new MenuItem("Extra 2", "Open/close the extra door (#2). Note this door is not present on most vehicles.");
            var BB = new MenuItem("Bomb Bay", "Open/close the bomb bay. Only available on some planes.");
            var doors = new List<string>() { "Front Left", "Front Right", "Rear Left", "Rear Right", "Hood", "Trunk", "Extra 1", "Extra 2" };
            var removeDoorList = new MenuListItem("Remove Door", doors, 0, "Remove a specific vehicle door completely.");
            var deleteDoors = new MenuCheckboxItem("Delete Removed Doors", "When enabled, doors that you remove using the list above will be deleted from the world. If disabled, then the doors will just fall on the ground.", false);

            VehicleDoorsMenu.AddMenuItem(LF);
            VehicleDoorsMenu.AddMenuItem(RF);
            VehicleDoorsMenu.AddMenuItem(LR);
            VehicleDoorsMenu.AddMenuItem(RR);
            VehicleDoorsMenu.AddMenuItem(HD);
            VehicleDoorsMenu.AddMenuItem(TR);
            VehicleDoorsMenu.AddMenuItem(E1);
            VehicleDoorsMenu.AddMenuItem(E2);
            VehicleDoorsMenu.AddMenuItem(BB);
            VehicleDoorsMenu.AddMenuItem(openAll);
            VehicleDoorsMenu.AddMenuItem(closeAll);
            VehicleDoorsMenu.AddMenuItem(removeDoorList);
            VehicleDoorsMenu.AddMenuItem(deleteDoors);

            VehicleDoorsMenu.OnListItemSelect += (sender, item, index, itemIndex) =>
            {
                var veh = GetVehicle();
                if (veh != null && veh.Exists())
                {
                    if (veh.Driver == Game.PlayerPed)
                    {
                        if (item == removeDoorList)
                        {
                            SetVehicleDoorBroken(veh.Handle, index, deleteDoors.Checked);
                        }
                    }
                    else
                    {
                        Notify.Error(CommonErrors.NeedToBeTheDriver);
                    }
                }
                else
                {
                    Notify.Error(CommonErrors.NoVehicle);
                }
            };

            // Handle button presses.
            VehicleDoorsMenu.OnItemSelect += async (sender, item, index) =>
            {
                // Get the vehicle.
                var veh = GetVehicle();
                // If the player is in a vehicle, it's not dead and the player is the driver, continue.
                if (veh != null && veh.Exists() && !veh.IsDead && veh.Driver == Game.PlayerPed)
                {
                    // If button 0-5 are pressed, then open/close that specific index/door.
                    if (index < 8)
                    {
                        // If the door is open.
                        var open = GetVehicleDoorAngleRatio(veh.Handle, index) > 0.1f;

                        if (open)
                        {
                            // Close the door.
                            SetVehicleDoorShut(veh.Handle, index, false);
                        }
                        else
                        {
                            // Open the door.
                            SetVehicleDoorOpen(veh.Handle, index, false, false);
                        }
                    }
                    // If the index >= 8, and the button is "openAll": open all doors.
                    else if (item == openAll)
                    {
                        // Loop through all doors and open them.
                        for (var door = 0; door < 8; door++)
                        {
                            SetVehicleDoorOpen(veh.Handle, door, false, false);
                        }
                        if (veh.HasBombBay)
                        {
                            veh.OpenBombBay();
                        }
                    }
                    // If the index >= 8, and the button is "closeAll": close all doors.
                    else if (item == closeAll)
                    {
                        // Close all doors.
                        if(VehicleCloseAllDoorsCooldown)
                        {
                            Notify.Error("You must wait a few seconds before closing all doors again.");
                            return;
                        }
                        SetVehicleDoorsShut(veh.Handle, false);
                        if (veh.HasBombBay)
                        {
                            veh.CloseBombBay();
                        }
                        VehicleCloseAllDoorsCooldown = true;
                        await Delay(2000);
                        VehicleCloseAllDoorsCooldown = false;

                    }
                    // If bomb bay doors button is pressed and the vehicle has bomb bay doors.
                    else if (item == BB && veh.HasBombBay)
                    {
                        var bombBayOpen = AreBombBayDoorsOpen(veh.Handle);
                        // If open, close them.
                        if (bombBayOpen)
                        {
                            veh.CloseBombBay();
                        }
                        // Otherwise, open them.
                        else
                        {
                            veh.OpenBombBay();
                        }
                    }
                }
                else
                {
                    Notify.Alert(CommonErrors.NoVehicle, placeholderValue: "to open/close a vehicle door");
                }
            };

            #endregion

            #region Vehicle Windows Submenu Stuff
            var fwu = new MenuItem("~y~↑~s~ Roll Front Windows Up", "Roll both front windows up.");
            var fwd = new MenuItem("~o~↓~s~ Roll Front Windows Down", "Roll both front windows down.");
            var rwu = new MenuItem("~y~↑~s~ Roll Rear Windows Up", "Roll both rear windows up.");
            var rwd = new MenuItem("~o~↓~s~ Roll Rear Windows Down", "Roll both rear windows down.");
            VehicleWindowsMenu.AddMenuItem(fwu);
            VehicleWindowsMenu.AddMenuItem(fwd);
            VehicleWindowsMenu.AddMenuItem(rwu);
            VehicleWindowsMenu.AddMenuItem(rwd);
            VehicleWindowsMenu.OnItemSelect += (sender, item, index) =>
            {
                var veh = GetVehicle();
                if (veh != null && veh.Exists() && !veh.IsDead)
                {
                    if (item == fwu)
                    {
                        RollUpWindow(veh.Handle, 0);
                        RollUpWindow(veh.Handle, 1);
                    }
                    else if (item == fwd)
                    {
                        RollDownWindow(veh.Handle, 0);
                        RollDownWindow(veh.Handle, 1);
                    }
                    else if (item == rwu)
                    {
                        RollUpWindow(veh.Handle, 2);
                        RollUpWindow(veh.Handle, 3);
                    }
                    else if (item == rwd)
                    {
                        RollDownWindow(veh.Handle, 2);
                        RollDownWindow(veh.Handle, 3);
                    }
                }
            };
            #endregion

            #region Vehicle Liveries Submenu Stuff
            menu.OnItemSelect += (sender, item, idex) =>
            {
                // If the liverys menu button is selected.
                if (item == liveriesMenuBtn)
                {
                    // Get the player's vehicle.
                    var veh = GetVehicle();
                    // If it exists, isn't dead and the player is in the drivers seat continue.
                    if (veh != null && veh.Exists() && !veh.IsDead)
                    {
                        if (veh.Driver == Game.PlayerPed)
                        {
                            VehicleLiveriesMenu.ClearMenuItems();
                            SetVehicleModKit(veh.Handle, 0);
                            var liveryCount = GetVehicleLiveryCount(veh.Handle);

                            if (liveryCount > 0)
                            {
                                var liveryList = new List<string>();
                                for (var i = 0; i < liveryCount; i++)
                                {
                                    var livery = GetLiveryName(veh.Handle, i);
                                    livery = GetLabelText(livery) != "NULL" ? GetLabelText(livery) : $"Livery #{i}";
                                    liveryList.Add(livery);
                                }
                                var liveryListItem = new MenuListItem("Set Livery", liveryList, GetVehicleLivery(veh.Handle), "Choose a livery for this vehicle.");
                                VehicleLiveriesMenu.AddMenuItem(liveryListItem);
                                VehicleLiveriesMenu.OnListIndexChange += (_menu, listItem, oldIndex, newIndex, itemIndex) =>
                                {
                                    if (listItem == liveryListItem)
                                    {
                                        veh = GetVehicle();
                                        SetVehicleLivery(veh.Handle, newIndex);
                                    }
                                };
                                VehicleLiveriesMenu.RefreshIndex();
                                //VehicleLiveriesMenu.UpdateScaleform();
                            }
                            else
                            {
                                Notify.Error("This vehicle does not have any liveries.");
                                VehicleLiveriesMenu.CloseMenu();
                                menu.OpenMenu();
                                var backBtn = new MenuItem("No Liveries Available :(", "Click me to go back.")
                                {
                                    Label = "Go Back"
                                };
                                VehicleLiveriesMenu.AddMenuItem(backBtn);
                                VehicleLiveriesMenu.OnItemSelect += (sender2, item2, index2) =>
                                {
                                    if (item2 == backBtn)
                                    {
                                        VehicleLiveriesMenu.GoBack();
                                    }
                                };

                                VehicleLiveriesMenu.RefreshIndex();
                                //VehicleLiveriesMenu.UpdateScaleform();
                            }
                        }
                        else
                        {
                            Notify.Error("You have to be the driver of a vehicle to access this menu.");
                        }
                    }
                    else
                    {
                        Notify.Error("You have to be the driver of a vehicle to access this menu.");
                    }
                }
            };
            #endregion

            #region Vehicle Mod Submenu Stuff
            menu.OnItemSelect += (sender, item, index) =>
            {
                // When the mod submenu is openend, reset all items in there.
                if (item == modMenuBtn)
                {
                    if (Game.PlayerPed.IsInVehicle())
                    {
                        UpdateMods();
                    }
                    else
                    {
                        VehicleModMenu.CloseMenu();
                        menu.OpenMenu();
                    }

                }
            };
            #endregion

            #region Vehicle Handling Submenu Stuff
            var massBtn = new MenuItem("Mass", "Changes the vehicle's mass.");
            var dragCoeffBtn = new MenuItem("Drag Coefficient", "Changes the vehicle's maximum speed.");
            var downforceBtn = new MenuItem("Downforce Modifier", "Changes the amount of downforce the vehicle has.");
            var submergedBtn = new MenuItem("Percent Submerged", "Changes the percentage of the vehicle height that floats.");
            var driveBiasBtn = new MenuItem("Drive Bias Front", "Determines whether a vehicle is FWD, RWD or 4WD.");
            var driveGearsBtn = new MenuItem("Drive Gears", "Changes the number of forward speeds a transmission contains.");
            var driveForceBtn = new MenuItem("Drive Force", "Change the drive force of the car at the wheels.");
            var driveInertiaBtn = new MenuItem("Drive Inertia", "Changes how fast an engine will rev.");
            var clutchRateUpBtn = new MenuItem("Clutch Change Rate Up Shift", "Changes how fast a vehicle's up shifts are.");
            var clutchRateDownBtn = new MenuItem("Clutch Change Rate Down Shift", "Changes how fast a vehicle's down shifts are.");
            var driveMaxFlatBtn = new MenuItem("Drive Max Flat Velocity", "Changes the speed at redline in top gear.");
            var brakeForceBtn = new MenuItem("Brake Force", "Changes the multiplier for the game's calculation of deceleration.");
            var brakeBiasBtn = new MenuItem("Brake Bias Front", "Changes the distribution of the braking force between front and rear.");
            var handbrakeBtn = new MenuItem("Handbrake Force", "Changes the braking power of the handbrake.");
            var steeringLockBtn = new MenuItem("Steering Lock", "Changes the maximum angle that steered wheels wil be able to turn.");
            var tractionCurveMaxBtn = new MenuItem("Traction Curve Max", "Changes maximum cornering / acceleration coefficient of grip.");
            var tractionCurveMinBtn = new MenuItem("Traction Curve Min", "Changes sliding cornering / acceleration coefficient of grip.");
            var tractionCurveLateralBtn = new MenuItem("Traction Curve Lateral", "Changes shape of lateral traction curve where lower values make grip more responsive and higher levels make grip less responsive.");
            var tractionSpringDeltaBtn = new MenuItem("Traction Spring Delta", "Changes the max distance of lateral sidewall travel in meters.");
            var lowSpeedTractionLossBtn = new MenuItem("Low Speed Traction Loss", "Changes how much traction is reduced at low speed, mostly affecting burnout.");
            var camberStiffBtn = new MenuItem("Camber Stiffness", "Changes how much vehicle is pushed towards its roll direction.");
            var tractionBiasFrontBtn = new MenuItem("Traction Bias Front", "Determines the distribution of traction from front to rear.");
            var tractionLossBtn = new MenuItem("Traction Loss", "Changes how much traction is affected by material grip differences.");
            var suspensionForceBtn = new MenuItem("Suspension Force", "Affects how strong suspension is.");
            var suspensionCompBtn = new MenuItem("Suspension Compression", "Affects the damping during strut comrpession.");
            var suspensionReboundBtn = new MenuItem("Suspension Rebound", "Affects the damping during strut rebound.");
            var suspensionUpperBtn = new MenuItem("Suspension Upper Limit", "Changes how far the wheels can move up from their original position.");
            var suspensionLowerBtn = new MenuItem("Suspension Lower Limit", "Changes how far the wheels can move down from their original position.");
            var suspensionRaiseBtn = new MenuItem("Suspension Raise", "Changes the amount the suspension raises the body off the wheels.");
            var suspensionBiasBtn = new MenuItem("Suspension Bias Front", "Changes the force damping scale from front to back.");
            var antiRollBtn = new MenuItem("Anti Rollbar Force", "Changes the spring constant where larger numbers reduce body roll.");
            var antiRollBiasBtn = new MenuItem("Anti Rollbar Bias Front", "Changes the bias between the front and rear of the antiroll bar.");
            var rollCentreFrontBtn = new MenuItem("Roll Centre Height Front", "Changes the roll centre height for the front axle from the road in metres.");
            var rollCentreRearBtn = new MenuItem("Roll Centre Height Rear", "Change the roll center height for the rear axle from the road in metres.");
            var collisionDmgBtn = new MenuItem("Collision Damage", "Changes the multiplier for the game's collision damage.");
            var weaponDmgBtn = new MenuItem("Weapon Damage", "Changes the multiplier for the game's weapon damage.");
            var deformDmgBtn = new MenuItem("Deformation Damage", "Changes the multiplier for the game's visual deformation.");
            var engineDmgBtn = new MenuItem("Engine Damage", "Changes the multiplier for the game's engine damage.");
            var petrolVolBtn = new MenuItem("Petrol Tank Volume", "Changes the amount of petrol in the vehicle's petrol tank.");
            var oilVolBtn = new MenuItem("Oil Volume", "Changes the black smoke time before the engine dies.");

            VehicleHandlingMenu.AddMenuItem(massBtn);
            VehicleHandlingMenu.AddMenuItem(dragCoeffBtn);
            VehicleHandlingMenu.AddMenuItem(downforceBtn);
            VehicleHandlingMenu.AddMenuItem(submergedBtn);
            VehicleHandlingMenu.AddMenuItem(driveBiasBtn);
            VehicleHandlingMenu.AddMenuItem(driveGearsBtn);
            VehicleHandlingMenu.AddMenuItem(driveForceBtn);
            VehicleHandlingMenu.AddMenuItem(driveInertiaBtn);
            VehicleHandlingMenu.AddMenuItem(clutchRateUpBtn);
            VehicleHandlingMenu.AddMenuItem(clutchRateDownBtn);
            VehicleHandlingMenu.AddMenuItem(driveMaxFlatBtn);
            VehicleHandlingMenu.AddMenuItem(brakeBiasBtn);
            VehicleHandlingMenu.AddMenuItem(steeringLockBtn);
            VehicleHandlingMenu.AddMenuItem(tractionCurveMaxBtn);
            VehicleHandlingMenu.AddMenuItem(tractionCurveMinBtn);
            VehicleHandlingMenu.AddMenuItem(tractionCurveLateralBtn);
            VehicleHandlingMenu.AddMenuItem(tractionSpringDeltaBtn);
            VehicleHandlingMenu.AddMenuItem(lowSpeedTractionLossBtn);
            VehicleHandlingMenu.AddMenuItem(camberStiffBtn);
            VehicleHandlingMenu.AddMenuItem(tractionBiasFrontBtn);
            VehicleHandlingMenu.AddMenuItem(tractionLossBtn);
            VehicleHandlingMenu.AddMenuItem(suspensionForceBtn);
            VehicleHandlingMenu.AddMenuItem(suspensionCompBtn);
            VehicleHandlingMenu.AddMenuItem(suspensionReboundBtn);
            VehicleHandlingMenu.AddMenuItem(suspensionUpperBtn);
            VehicleHandlingMenu.AddMenuItem(suspensionLowerBtn);
            VehicleHandlingMenu.AddMenuItem(suspensionRaiseBtn);
            VehicleHandlingMenu.AddMenuItem(suspensionBiasBtn);
            VehicleHandlingMenu.AddMenuItem(antiRollBtn);
            VehicleHandlingMenu.AddMenuItem(antiRollBiasBtn);
            VehicleHandlingMenu.AddMenuItem(rollCentreFrontBtn);
            VehicleHandlingMenu.AddMenuItem(rollCentreRearBtn);
            VehicleHandlingMenu.AddMenuItem(collisionDmgBtn);
            VehicleHandlingMenu.AddMenuItem(weaponDmgBtn);
            VehicleHandlingMenu.AddMenuItem(deformDmgBtn);
            VehicleHandlingMenu.AddMenuItem(engineDmgBtn);
            VehicleHandlingMenu.AddMenuItem(petrolVolBtn);
            VehicleHandlingMenu.AddMenuItem(oilVolBtn);

            VehicleHandlingMenu.OnMenuOpen += (sender) =>
            {
                var veh = GetVehicle();
                if (veh != null && veh.Exists())
                {
                    double currentMass = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fMass");
                    massBtn.Label = currentMass.ToString("F2") + "kg";
                    
                    double currentDragCoeff = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fInitialDragCoeff");
                    dragCoeffBtn.Label = currentDragCoeff.ToString("F2") + "x";

                    double currentDownforce = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fDownForceModifier");
                    downforceBtn.Label = currentDownforce.ToString("F2") + "x";

                    double currentSubmerged = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fPercentSubmerged");
                    submergedBtn.Label = currentSubmerged.ToString("F2") + "%";

                    double currentDriveBias = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fDriveBiasFront");
                    driveBiasBtn.Label = currentDriveBias.ToString("F2");

                    double currentDriveGears = GetVehicleHandlingInt(veh.Handle, "CHandlingData", "nInitialDriveGears");
                    driveGearsBtn.Label = currentDriveGears.ToString("F2");

                    double currentDriveForce = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fInitialDriveForce");
                    driveForceBtn.Label = currentDriveForce.ToString("F2");

                    double currentDriveInertia = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fDriveInertia");
                    driveInertiaBtn.Label = currentDriveInertia.ToString("F2");

                    double currentClutchRateUp = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fClutchChangeRateScaleUpShift");
                    clutchRateUpBtn.Label = currentClutchRateUp.ToString("F2") + "x";

                    double currentClutchRateDown = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fClutchChangeRateScaleDownShift");
                    clutchRateDownBtn.Label = currentClutchRateDown.ToString("F2") + "x";

                    double currentDriveMaxFlat = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fInitialDriveMaxFlatVel");
                    driveMaxFlatBtn.Label = currentDriveMaxFlat.ToString("F2");

                    double currentBrakeForce = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fBrakeForce");
                    brakeForceBtn.Label = currentBrakeForce.ToString("F2") + "x";

                    double currentBrakeBias = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fBrakeBiasFront");
                    brakeBiasBtn.Label = currentBrakeBias.ToString("F2");

                    double currentHandbrakeForce = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fHandBrakeForce");
                    handbrakeBtn.Label = currentHandbrakeForce.ToString("F2");

                    double currentSteeringLock = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fSteeringLock");
                    steeringLockBtn.Label = currentSteeringLock.ToString("F2");

                    double currentTractionCurveMax = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fTractionCurveMax");
                    tractionCurveMaxBtn.Label = currentSteeringLock.ToString("F2");

                    double currentTractionCurveMin = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fTractionCurveMin");
                    tractionCurveMinBtn.Label = currentTractionCurveMin.ToString("F2");

                    double currentTractionCurveLateral = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fTractionCurveLateral");
                    tractionCurveLateralBtn.Label = currentTractionCurveLateral.ToString("F2");

                    double currentTractionSpringDelta = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fTractionSpringDeltaMax");
                    tractionSpringDeltaBtn.Label = currentTractionSpringDelta.ToString("F2");

                    double currentLowSpeedTractionLoss = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fLowSpeedTractionLossMult");
                    lowSpeedTractionLossBtn.Label = currentLowSpeedTractionLoss.ToString("F2");

                    double currentCamberStiff = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fCamberStiffnesss");
                    camberStiffBtn.Label = currentCamberStiff.ToString("F2");

                    double currentTractionBiasFront = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fTractionBiasFront");
                    tractionBiasFrontBtn.Label = currentTractionBiasFront.ToString("F2");

                    double currentTractionLoss = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fTractionLossMult");
                    tractionLossBtn.Label = currentTractionLoss.ToString("F2");

                    double currentSuspensionForce = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fSuspensionForce");
                    suspensionForceBtn.Label = currentSuspensionForce.ToString("F2");

                    double currentSuspensionCompression = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fSuspensionCompDamp");
                    suspensionCompBtn.Label = currentSuspensionCompression.ToString("F2");

                    double currentCompressionRebound = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fSuspensionReboundDamp");
                    suspensionReboundBtn.Label = currentCompressionRebound.ToString("F2");

                    double currentSuspensionUpper = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fSuspensionUpperLimit");
                    suspensionUpperBtn.Label = currentSuspensionUpper.ToString("F2");

                    double currentSuspensionLower = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fSuspensionLowerLimit");
                    suspensionLowerBtn.Label = currentSuspensionLower.ToString("F2");

                    double currentSuspensionRaise = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fSuspensionRaise");
                    suspensionRaiseBtn.Label = currentSuspensionRaise.ToString("F2");

                    double currentSuspensionBias = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fSuspensionBiasFront");
                    suspensionBiasBtn.Label = currentSuspensionBias.ToString("F2");

                    double currentAntiRoll = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fAntiRollBarForce");
                    antiRollBtn.Label = currentAntiRoll.ToString("F2");

                    double currentAntiRollBias = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fAntiRollBarBiasFront");
                    antiRollBiasBtn.Label = currentAntiRollBias.ToString("F2");

                    double currentRollCentreFront = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fRollCentreHeightFront");
                    rollCentreFrontBtn.Label = currentRollCentreFront.ToString("F2");

                    double currentRollCentreRear = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fRollCentreHeightRear");
                    rollCentreRearBtn.Label = currentRollCentreRear.ToString("F2");

                    double currentCollisionDmg = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fCollisionDamageMult");
                    collisionDmgBtn.Label = currentCollisionDmg.ToString("F2") + "x";

                    double currentWeaponDmg = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fWeaponDamageMult");
                    weaponDmgBtn.Label = currentWeaponDmg.ToString("F2") + "x";

                    double currentDeformDmg = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fDeformationDamageMult");
                    deformDmgBtn.Label = currentDeformDmg.ToString("F2") + "x";

                    double currentEngineDmg = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fEngineDamageMult");
                    engineDmgBtn.Label = currentEngineDmg.ToString("F2") + "x";

                    double currentPetrolVol = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fPetrolTankVolume");
                    petrolVolBtn.Label = currentPetrolVol.ToString("F2");

                    double currentOilVol = GetVehicleHandlingFloat(veh.Handle, "CHandlingData", "fOilVolume");
                    oilVolBtn.Label = currentOilVol.ToString("F2");
                }
            };

            VehicleHandlingMenu.OnItemSelect += async (sender, item, index) =>
            {
                var veh = GetVehicle();
                string fieldName = null;
                int? value = null;

                if (item == massBtn) await HandleVehicleStat(item, "fMass", "Set Mass", "kg");
                else if (item == dragCoeffBtn) await HandleVehicleStat(item, "fInitialDragCoeff", "Set Drag Coefficient", "x", 10.0, 120.0);
                else if (item == downforceBtn) await HandleVehicleStat(item, "fDownForceModifier", "Set Downforce", "x");
                else if (item == submergedBtn) await HandleVehicleStat(item, "fPercentSubmerged", "Set Submerged", "%", 0, 100);
                else if (item == driveBiasBtn) await HandleVehicleStat(item, "fDriveBiasFront", "Set Drive Bias", "", 0.0, 1.0);
                else if (item == driveGearsBtn) await HandleVehicleStat(item, "nInitialDriveGears", "Set Gears", "", 0, 10);
                else if (item == driveForceBtn) await HandleVehicleStat(item, "fInitialDriveForce", "Set Drive Force");
                else if (item == driveInertiaBtn) await HandleVehicleStat(item, "fDriveInertia", "Set Drive Inertia", "", 0.01, 2.0);
                else if (item == clutchRateUpBtn) await HandleVehicleStat(item, "fClutchChangeRateScaleUpShift", "Set Clutch Change Rate Up Shift", "x", 0.0, 13.0);
                else if (item == clutchRateDownBtn) await HandleVehicleStat(item, "fClutchChangeRateScaleDownShift", "Set Clutch Change Rate Down Shift", "x", 0.0, 13.0);
                else if (item == driveMaxFlatBtn) await HandleVehicleStat(item, "fInitialDriveMaxFlatVel", "");
                else if (item == brakeForceBtn) await HandleVehicleStat(item, "fBrakeForce", "Change Brake Force", "x");
                else if (item == brakeBiasBtn) await HandleVehicleStat(item, "fBrakeBiasFront", "Change Brake Bias", "", 0.0, 1.0);
                else if (item == handbrakeBtn) await HandleVehicleStat(item, "fHandBrakeForce", "Change Handbrke Force", "");
                else if (item == steeringLockBtn) await HandleVehicleStat(item, "fSteeringLock", "Change Steering Lock", "", 1.0, 90.0);
                else if (item == tractionCurveMaxBtn) await HandleVehicleStat(item, "fTractionCurveMax", "Change Traction Curve Max", "");
                else if (item == tractionCurveMinBtn) await HandleVehicleStat(item, "fTractionCurveMin", "Change Traction Curve Min", "");
                else if (item == tractionCurveLateralBtn) await HandleVehicleStat(item, "fTractionCurveLateral", "Change Traction Curve Lateral", "");
                else if (item == tractionSpringDeltaBtn) await HandleVehicleStat(item, "fTractionSpringDeltaMax", "Change Traction Spring Delta", "");
                else if (item == lowSpeedTractionLossBtn) await HandleVehicleStat(item, "fLowSpeedTractionLossMult", "Change Low Speed Traction Loss", "");
                else if (item == camberStiffBtn) await HandleVehicleStat(item, "fCamberStiffnesss", "Change Camber Stiffness", "");
                else if (item == tractionBiasFrontBtn) await HandleVehicleStat(item, "fTractionBiasFront", "Change Traction Bias", "", 0.01, 1.0);
                else if (item == tractionLossBtn) await HandleVehicleStat(item, "fTractionLossMult", "Change Traction Loss", "");
                else if (item == suspensionForceBtn) await HandleVehicleStat(item, "fSuspensionForce", "Change Suspension Force", "");
                else if (item == suspensionCompBtn) await HandleVehicleStat(item, "fSuspensionCompDamp", "Change Suspension Compression", "");
                else if (item == suspensionReboundBtn) await HandleVehicleStat(item, "fSuspensionReboundDamp", "Change Suspension Redbound", "");
                else if (item == suspensionUpperBtn) await HandleVehicleStat(item, "fSuspensionUpperLimit", "Change Upper Suspension", "");
                else if (item == suspensionLowerBtn) await HandleVehicleStat(item, "fSuspensionLowerLimit", "Change Lower Suspension", "");
                else if (item == suspensionRaiseBtn) await HandleVehicleStat(item, "fSuspensionRaise", "Change Suspension Raise", "");
                else if (item == suspensionBiasBtn) await HandleVehicleStat(item, "fSuspensionBiasFront", "Change Suspension Bias", "");
                else if (item == antiRollBtn) await HandleVehicleStat(item, "fAntiRollBarForce", "Change Anti Rollbar Force", "");
                else if (item == antiRollBiasBtn) await HandleVehicleStat(item, "fAntiRollBarBiasFront", "Change Anti Rollbar Bias", "", 0.0, 1.0);
                else if (item == rollCentreFrontBtn) await HandleVehicleStat(item, "fRollCentreHeightFront", "Change Roll Centre Height Front", "");
                else if (item == rollCentreRearBtn) await HandleVehicleStat(item, "fRollCentreHeightRear", "Change Roll Centre Height Rear", "");
                else if (item == collisionDmgBtn) await HandleVehicleStat(item, "fCollisionDamageMult", "Change Collision Damage", "x", 0.0, 10.0);
                else if (item == weaponDmgBtn) await HandleVehicleStat(item, "fWeaponDamageMult", "Change Weapon Damage", "x", 0.0, 10.0);
                else if (item == deformDmgBtn) await HandleVehicleStat(item, "fDeformationDamageMult", "Change Deformation Damage", "x", 0.0, 10.0);
                else if (item == engineDmgBtn) await HandleVehicleStat(item, "fEngineDamageMult", "Change Engine Damage", "x", 0.0, 10.0);
                else if (item == petrolVolBtn) await HandleVehicleStat(item, "fPetrolTankVolume", "Change Petrol Tank Volume", "");
                else if (item == oilVolBtn) await HandleVehicleStat(item, "fOilVolume", "Change Oil Volume", "");
                
                if (fieldName != null && value != null)
                {
                    SetVehicleHandlingInt(veh.Handle, "CHandlingData", fieldName, value.Value);
                    ModifyVehicleTopSpeed(veh.Handle, 1);
                }
                
            };
            #endregion
            
            #region Vehicle Engine Sound Submenu Stuff
            var jsonData = LoadResourceFile(GetCurrentResourceName(), "config/addons.json") ?? "{}";
            var addons = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonData);
            
            List<string> soundNameList = new List<string>();
            List<string> soundList = new List<string>();

            if (addons != null && addons.ContainsKey("engine_sounds"))
            {
                var soundDict = JObject.FromObject(addons["engine_sounds"])
                        .ToObject<Dictionary<string, string>>();

                foreach (var soundEntry in soundDict)
                {
                    soundNameList.Add(soundEntry.Key);
                    soundList.Add(soundEntry.Value);
                }

                Debug.WriteLine($"[VMENU] Loaded {soundDict.Count} engine sounds");
            }
            else
            {
                Debug.WriteLine("[VMENU] No engine sounds in addons.json");
            }

            var resetEngineSoundBtn = new MenuItem("Reset Engine Sound", "Resets vehicle engine sound to default");
            var soundMenuList = new MenuListItem("Set Engine Sound", soundNameList, 0, "Select the vehicle engine sound here");

            VehicleEngineSoundMenu.AddMenuItem(soundMenuList);
            VehicleEngineSoundMenu.AddMenuItem(resetEngineSoundBtn);
            
            menu.OnItemSelect += (sender, item, index) =>
            {
                if (item == engineSoundMenuBtn)
                {                   
                    if (soundList.Count == 0)
                    {
                        Notify.Error("No engine sounds found in the config file.");
                    }
                }
            };

            VehicleEngineSoundMenu.OnItemSelect += (sender, item, index) =>
            {
                var veh = GetVehicle();

                if (IsVehicleSirenOn(veh.Handle))
                {
                    Notify.Error("You must turn off your lights & sirens before changing the sound!");
                }
                else
                {
                    NetworkRegisterEntityAsNetworked(veh.Handle);
                    int vehicle = VehToNet(veh.Handle);             
                    
                    Notify.Success("Resetting your vehicle's engine sound");
                    TriggerServerEvent("vMenu:changeEngineSound", vehicle, "resetenginesound");
                }
            };

            VehicleEngineSoundMenu.OnListItemSelect += (sender, item, index, itemIndex) =>
            {
                var veh = GetVehicle();

                if (IsVehicleSirenOn(veh.Handle))
                {
                    Notify.Error("You must turn off your lights & sirens before changing the sound!");
                }
                else
                {
                    NetworkRegisterEntityAsNetworked(veh.Handle);
                    int vehicle = VehToNet(veh.Handle);

                    string name = soundNameList[index];
                    string selected = soundList[index];
                    
                    Notify.Success("Setting your vehicle's engine sound to " + name);
                    TriggerServerEvent("vMenu:changeEngineSound", vehicle, selected);
                }
            };

            #endregion

            #region Vehicle Components Submenu
            // when the components menu is opened.
            menu.OnItemSelect += (sender, item, index) =>
            {
                // If the components menu is opened.
                if (item == componentsMenuBtn)
                {
                    // Empty the menu in case there were leftover buttons from another vehicle.
                    if (VehicleComponentsMenu.Size > 0)
                    {
                        VehicleComponentsMenu.ClearMenuItems();
                        vehicleExtras.Clear();
                        VehicleComponentsMenu.RefreshIndex();
                        //VehicleComponentsMenu.UpdateScaleform();
                    }

                    // Get the vehicle.
                    var veh = GetVehicle();

                    // Check if the vehicle exists, it's actually a vehicle, it's not dead/broken and the player is in the drivers seat.
                    if (veh != null && veh.Exists() && !veh.IsDead && veh.Driver == Game.PlayerPed)
                    {
                        Dictionary<int, string> extraLabels;
                        if (!VehicleExtras.TryGetValue((uint)veh.Model.Hash, out extraLabels))
                        {
                            extraLabels = new Dictionary<int, string>();
                        }
                      
                        //List<int> extraIds = new List<int>();
                        // Loop through all possible extra ID's (AFAIK: 0-14).
                        for (var extra = 0; extra < 14; extra++)
                        {
                            // If this extra exists...
                            if (veh.ExtraExists(extra))
                            {
                                // Add it's ID to the list.
                                //extraIds.Add(extra);

                                // Create the checkbox label
                                string extraLabel;
                                if (!extraLabels.TryGetValue(extra, out extraLabel))
                                    extraLabel = $"Extra #{extra}";
                                // Create a checkbox for it.
                                var extraCheckbox = new MenuCheckboxItem(extraLabel, extra.ToString(), veh.IsExtraOn(extra));
                                // Add the checkbox to the menu.
                                VehicleComponentsMenu.AddMenuItem(extraCheckbox);

                                // Add it's ID to the dictionary.
                                vehicleExtras[extraCheckbox] = extra;
                            }
                        }



                        if (vehicleExtras.Count > 0)
                        {
                            var backBtn = new MenuItem("Go Back", "Go back to the Vehicle Options menu.");
                            VehicleComponentsMenu.AddMenuItem(backBtn);
                            VehicleComponentsMenu.OnItemSelect += (sender3, item3, index3) =>
                            {
                                VehicleComponentsMenu.GoBack();
                            };
                        }
                        else
                        {
                            var backBtn = new MenuItem("No Extras Available :(", "Go back to the Vehicle Options menu.")
                            {
                                Label = "Go Back"
                            };
                            VehicleComponentsMenu.AddMenuItem(backBtn);
                            VehicleComponentsMenu.OnItemSelect += (sender3, item3, index3) =>
                            {
                                VehicleComponentsMenu.GoBack();
                            };
                        }
                        // And update the submenu to prevent weird glitches.
                        VehicleComponentsMenu.RefreshIndex();
                        //VehicleComponentsMenu.UpdateScaleform();

                    }
                }
            };

            // Disable all extra options if vehicle is too damaged
            VehicleComponentsMenu.OnMenuOpen += (menu) =>
            {
                Vehicle vehicle;
                bool checkDamageBeforeChangingExtras = GetSettingsBool(Setting.vmenu_prevent_extras_when_damaged) && !IsAllowed(Permission.VOBypassExtraDamage);

                if (!checkDamageBeforeChangingExtras || !Entity.Exists(vehicle = GetVehicle()))
                {
                    return;
                }

                List<MenuItem> menuItems = menu.GetMenuItems();
                bool isTooDamaged = IsVehicleTooDamagedToChangeExtras(vehicle);

                menu.ClearMenuItems();

                if (isTooDamaged && !menuItems.Exists(i => i.Text.Contains("too damaged")))
                {
                    MenuItem spacer = GetSpacerMenuItem("Vehicle too damaged!", "Vehicle is too damaged to change extras, repair it first!");

                    // Place at the start of the menu
                    menuItems.Insert(0, spacer);
                }

                foreach (MenuItem item in menuItems)
                {
                    // Check for spacer
                    if (item.Text.Contains("too damaged"))
                    {
                        if (!isTooDamaged)
                        {
                            continue;
                        }
                    }
                    else if (item.Text != "Go Back")
                    {
                        item.Enabled = !isTooDamaged;
                    }

                    menu.AddMenuItem(item);
                }

                menu.RefreshIndex();
            };

            // when a checkbox in the components menu changes
            VehicleComponentsMenu.OnCheckboxChange += (sender, item, index, _checked) =>
            {
                // When a checkbox is checked/unchecked, get the selected checkbox item index and use that to get the component ID from the list.
                // Then toggle that extra.
                if (vehicleExtras.TryGetValue(item, out var extra))
                {
                    var veh = GetVehicle();

                    if (!Entity.Exists(veh))
                    {
                        Notify.Error(CommonErrors.NoVehicle);
                        return;
                    }

                    bool checkDamageBeforeChangingExtras = GetSettingsBool(Setting.vmenu_prevent_extras_when_damaged) && !IsAllowed(Permission.VOBypassExtraDamage);

                    if (checkDamageBeforeChangingExtras)
                    {
                        bool isTooDamaged = IsVehicleTooDamagedToChangeExtras(veh);

                        if (isTooDamaged)
                        {
                            // Send message to player when extra change is denied
                            Notify.Alert("Vehicle is too damaged to change extra, repair it first!", true, false);

                            // Send to previous menu
                            VehicleComponentsMenu.GoBack();
                            return;
                        }
                    }

                    veh.ToggleExtra(extra, _checked);
                }
            };
            #endregion

            #region Underglow Submenu
            var underglowFront = new MenuCheckboxItem("Enable Front Light", "Enable or disable the underglow on the front side of the vehicle. Note not all vehicles have lights.", false);
            var underglowBack = new MenuCheckboxItem("Enable Rear Light", "Enable or disable the underglow on the left side of the vehicle. Note not all vehicles have lights.", false);
            var underglowLeft = new MenuCheckboxItem("Enable Left Light", "Enable or disable the underglow on the right side of the vehicle. Note not all vehicles have lights.", false);
            var underglowRight = new MenuCheckboxItem("Enable Right Light", "Enable or disable the underglow on the back side of the vehicle. Note not all vehicles have lights.", false);
            var underglowColorsList = new List<string>();
            for (var i = 0; i < 13; i++)
            {
                underglowColorsList.Add(GetLabelText($"CMOD_NEONCOL_{i}"));
            }
            var underglowColor = new MenuListItem(GetLabelText("CMOD_NEON_1"), underglowColorsList, 0, "Select the color of the neon underglow.");

            VehicleUnderglowMenu.AddMenuItem(underglowFront);
            VehicleUnderglowMenu.AddMenuItem(underglowBack);
            VehicleUnderglowMenu.AddMenuItem(underglowLeft);
            VehicleUnderglowMenu.AddMenuItem(underglowRight);

            VehicleUnderglowMenu.AddMenuItem(underglowColor);

            menu.OnItemSelect += (sender, item, index) =>
            {
                #region reset checkboxes state when opening the menu.
                if (item == underglowMenuBtn)
                {
                    var veh = GetVehicle();
                    if (veh != null)
                    {
                        if (veh.Mods.HasNeonLights)
                        {
                            underglowFront.Checked = veh.Mods.HasNeonLight(VehicleNeonLight.Front) && veh.Mods.IsNeonLightsOn(VehicleNeonLight.Front);
                            underglowBack.Checked = veh.Mods.HasNeonLight(VehicleNeonLight.Back) && veh.Mods.IsNeonLightsOn(VehicleNeonLight.Back);
                            underglowLeft.Checked = veh.Mods.HasNeonLight(VehicleNeonLight.Left) && veh.Mods.IsNeonLightsOn(VehicleNeonLight.Left);
                            underglowRight.Checked = veh.Mods.HasNeonLight(VehicleNeonLight.Right) && veh.Mods.IsNeonLightsOn(VehicleNeonLight.Right);

                            underglowFront.Enabled = true;
                            underglowBack.Enabled = true;
                            underglowLeft.Enabled = true;
                            underglowRight.Enabled = true;

                            underglowFront.LeftIcon = MenuItem.Icon.NONE;
                            underglowBack.LeftIcon = MenuItem.Icon.NONE;
                            underglowLeft.LeftIcon = MenuItem.Icon.NONE;
                            underglowRight.LeftIcon = MenuItem.Icon.NONE;
                        }
                        else
                        {
                            underglowFront.Checked = false;
                            underglowBack.Checked = false;
                            underglowLeft.Checked = false;
                            underglowRight.Checked = false;

                            underglowFront.Enabled = false;
                            underglowBack.Enabled = false;
                            underglowLeft.Enabled = false;
                            underglowRight.Enabled = false;

                            underglowFront.LeftIcon = MenuItem.Icon.LOCK;
                            underglowBack.LeftIcon = MenuItem.Icon.LOCK;
                            underglowLeft.LeftIcon = MenuItem.Icon.LOCK;
                            underglowRight.LeftIcon = MenuItem.Icon.LOCK;
                        }
                    }
                    else
                    {
                        underglowFront.Checked = false;
                        underglowBack.Checked = false;
                        underglowLeft.Checked = false;
                        underglowRight.Checked = false;

                        underglowFront.Enabled = false;
                        underglowBack.Enabled = false;
                        underglowLeft.Enabled = false;
                        underglowRight.Enabled = false;

                        underglowFront.LeftIcon = MenuItem.Icon.LOCK;
                        underglowBack.LeftIcon = MenuItem.Icon.LOCK;
                        underglowLeft.LeftIcon = MenuItem.Icon.LOCK;
                        underglowRight.LeftIcon = MenuItem.Icon.LOCK;
                    }

                    underglowColor.ListIndex = GetIndexFromColor();
                }
                #endregion
            };
            // handle item selections
            VehicleUnderglowMenu.OnCheckboxChange += (sender, item, index, _checked) =>
            {
                if (Game.PlayerPed.IsInVehicle())
                {
                    var veh = GetVehicle();
                    if (veh.Mods.HasNeonLights)
                    {
                        veh.Mods.NeonLightsColor = GetColorFromIndex(underglowColor.ListIndex);
                        if (item == underglowLeft)
                        {
                            veh.Mods.SetNeonLightsOn(VehicleNeonLight.Left, veh.Mods.HasNeonLight(VehicleNeonLight.Left) && _checked);
                        }
                        else if (item == underglowRight)
                        {
                            veh.Mods.SetNeonLightsOn(VehicleNeonLight.Right, veh.Mods.HasNeonLight(VehicleNeonLight.Right) && _checked);
                        }
                        else if (item == underglowBack)
                        {
                            veh.Mods.SetNeonLightsOn(VehicleNeonLight.Back, veh.Mods.HasNeonLight(VehicleNeonLight.Back) && _checked);
                        }
                        else if (item == underglowFront)
                        {
                            veh.Mods.SetNeonLightsOn(VehicleNeonLight.Front, veh.Mods.HasNeonLight(VehicleNeonLight.Front) && _checked);
                        }
                    }
                }
            };

            VehicleUnderglowMenu.OnListIndexChange += (sender, item, oldIndex, newIndex, itemIndex) =>
            {
                if (item == underglowColor)
                {
                    if (Game.PlayerPed.IsInVehicle())
                    {
                        var veh = GetVehicle();
                        if (veh.Mods.HasNeonLights)
                        {
                            veh.Mods.NeonLightsColor = GetColorFromIndex(newIndex);
                        }
                    }
                }
            };
            #endregion

            #region Handle menu-opening refreshing license plate
            menu.OnMenuOpen += (sender) =>
            {
                menu.GetMenuItems().ForEach((item) =>
                {
                    var veh = GetVehicle(true);

                    if (item == setLicensePlateType && item is MenuListItem listItem && veh != null && veh.Exists())
                    {
                        // Set the license plate style.
                        switch (veh.Mods.LicensePlateStyle)
                        {
                            case LicensePlateStyle.BlueOnWhite1:
                                listItem.ListIndex = 0;
                                break;
                            case LicensePlateStyle.BlueOnWhite2:
                                listItem.ListIndex = 1;
                                break;
                            case LicensePlateStyle.BlueOnWhite3:
                                listItem.ListIndex = 2;
                                break;
                            case LicensePlateStyle.YellowOnBlue:
                                listItem.ListIndex = 3;
                                break;
                            case LicensePlateStyle.YellowOnBlack:
                                listItem.ListIndex = 4;
                                break;
                            case LicensePlateStyle.NorthYankton:
                                listItem.ListIndex = 5;
                                break;
                            case LicensePlateStyle.ECola:
                                listItem.ListIndex = 6;
                                break;
                            case LicensePlateStyle.LasVenturas:
                                listItem.ListIndex = 7;
                                break;
                            case LicensePlateStyle.LibertyCity:
                                listItem.ListIndex = 8;
                                break;
                            case LicensePlateStyle.LSCarMeet:
                                listItem.ListIndex = 9;
                                break;
                            case LicensePlateStyle.LSPanic:
                                listItem.ListIndex = 10;
                                break;
                            case LicensePlateStyle.LSPounders:
                                listItem.ListIndex = 11;
                                break;
                            case LicensePlateStyle.Sprunk:
                                listItem.ListIndex = 12;
                                break;
                            default:
                                break;
                        }
                    }
                });
            };
            #endregion

        }
        #endregion

        /// <summary>
        /// Public get method for the menu. Checks if the menu exists, if not create the menu first.
        /// </summary>
        /// <returns>Returns the Vehicle Options menu.</returns>
        public Menu GetMenu()
        {
            // If menu doesn't exist. Create one.
            if (menu == null)
            {
                CreateMenu();
            }
            // Return the menu.
            return menu;
        }

        #region Update Vehicle Mods Menu
        /// <summary>
        /// Refreshes the mods page. The selectedIndex allows you to go straight to a specific index after refreshing the menu.
        /// This is used because when the wheel type is changed, the menu is refreshed to update the available wheels list.
        /// </summary>
        /// <param name="selectedIndex">Pass this if you want to go straight to a specific mod/index.</param>
        public void UpdateMods(int selectedIndex = 0)
        {
            // If there are items, remove all of them.
            if (VehicleModMenu.Size > 0)
            {
                if (selectedIndex != 0)
                {
                    VehicleModMenu.ClearMenuItems(true);
                }
                else
                {
                    VehicleModMenu.ClearMenuItems(false);
                }

            }

            // Get the vehicle.
            var veh = GetVehicle();

            // Check if the vehicle exists, is still drivable/alive and it's actually a vehicle.
            if (veh != null && veh.Exists() && !veh.IsDead)
            {
                #region initial setup & dynamic vehicle mods setup
                // Set the modkit so we can modify the car.
                SetVehicleModKit(veh.Handle, 0);

                // Get all mods available on this vehicle.
                var mods = GetAllVehicleMods(veh);

                // Loop through all the mods.
                foreach (var mod in mods)
                {
                    veh = GetVehicle();

                    // Get the proper localized mod type (suspension, armor, etc) name.
                    var typeName = mod.LocalizedModTypeName;

                    // Create a list to all available upgrades for this modtype.
                    var modlist = new List<string>();

                    // Get the current item index ({current}/{max upgrades})
                    var currentItem = $"[1/{mod.ModCount + 1}]";

                    // Add the stock value for this mod.
                    var name = $"Stock {typeName} {currentItem}";
                    modlist.Add(name);

                    // Loop through all available upgrades for this specific mod type.
                    for (var x = 0; x < mod.ModCount; x++)
                    {
                        // Create the item index.
                        currentItem = $"[{2 + x}/{mod.ModCount + 1}]";

                        // Create the name (again, converting to proper case), then add the name.
                        name = mod.GetLocalizedModName(x) != "" ? $"{ToProperString(mod.GetLocalizedModName(x))} {currentItem}" : $"{typeName} #{x} {currentItem}";
                        modlist.Add(name);
                    }

                    // Create the MenuListItem for this mod type.
                    var currIndex = GetVehicleMod(veh.Handle, (int)mod.ModType) + 1;
                    var modTypeListItem = new MenuListItem(
                        typeName,
                        modlist,
                        currIndex,
                        $"Choose a ~y~{typeName}~s~ upgrade, it will be automatically applied to your vehicle."
                    )
                    {
                        ItemData = (int)mod.ModType
                    };

                    // Add the list item to the menu.
                    VehicleModMenu.AddMenuItem(modTypeListItem);
                }
                #endregion

                #region more variables and setup
                veh = GetVehicle();
                // Create the wheel types list & listitem and add it to the menu.
                var wheelTypes = new List<string>()
                {
                    "Sports",       // 0
                    "Muscle",       // 1
                    "Lowrider",     // 2
                    "SUV",          // 3
                    "Offroad",      // 4
                    "Tuner",        // 5
                    "Bike Wheels",  // 6
                    "High End",     // 7
                    "Benny's (1)",  // 8
                    "Benny's (2)",  // 9
                    "Open Wheel",   // 10
                    "Street",       // 11
                    "Track"         // 12    
                };
                var vehicleWheelType = new MenuListItem("Wheel Type", wheelTypes, MathUtil.Clamp(GetVehicleWheelType(veh.Handle), 0, 12), $"Choose a ~y~wheel type~s~ for your vehicle.");
                if (!veh.Model.IsBoat && !veh.Model.IsHelicopter && !veh.Model.IsPlane && !veh.Model.IsBicycle && !veh.Model.IsTrain)
                {
                    VehicleModMenu.AddMenuItem(vehicleWheelType);
                }

                // Create the checkboxes for some options.
                var toggleCustomWheels = new MenuCheckboxItem("Toggle Custom Wheels", "Press this to add or remove ~y~custom~s~ wheels.", GetVehicleModVariation(veh.Handle, 23));
                var xenonHeadlights = new MenuCheckboxItem("Xenon Headlights", "Enable or disable ~b~xenon ~s~headlights.", IsToggleModOn(veh.Handle, 22));
                var turbo = new MenuCheckboxItem("Turbo", "Enable or disable the ~y~turbo~s~ for this vehicle.", IsToggleModOn(veh.Handle, 18));
                var bulletProofTires = new MenuCheckboxItem("Bullet Proof Tires", "Enable or disable ~y~bullet proof tires~s~ for this vehicle.", !GetVehicleTyresCanBurst(veh.Handle));

                // Add the checkboxes to the menu.
                VehicleModMenu.AddMenuItem(toggleCustomWheels);
                VehicleModMenu.AddMenuItem(xenonHeadlights);
                var currentHeadlightColor = GetHeadlightsColorForVehicle(veh);
                if (currentHeadlightColor is < 0 or > 12)
                {
                    currentHeadlightColor = 13;
                }
                var headlightColor = new MenuListItem("Headlight Color", new List<string>() { "White", "Blue", "Electric Blue", "Mint Green", "Lime Green", "Yellow", "Golden Shower", "Orange", "Red", "Pony Pink", "Hot Pink", "Purple", "Blacklight", "Default Xenon" }, currentHeadlightColor, "New in the Arena Wars GTA V update: Colored headlights. Note you must enable Xenon Headlights first.");
                VehicleModMenu.AddMenuItem(headlightColor);
                VehicleModMenu.AddMenuItem(turbo);
                if (IsAllowed(Permission.VOBulletproofTires)) // BULLETPROOF TIRES
                {
                    VehicleModMenu.AddMenuItem(bulletProofTires);
                }

                bool isLowGripAvailable = GetGameBuildNumber() >= 2372;
                var lowGripTires = new MenuCheckboxItem("Low Grip Tires", "Enable or disable ~y~low grip tires~s~ for this vehicle.", isLowGripAvailable ? GetDriftTyresEnabled(veh.Handle) : false);
                if (isLowGripAvailable)
                {
                    VehicleModMenu.AddMenuItem(lowGripTires);
                }

                // Create a list of tire smoke options.
                var tireSmokes = new List<string>() { "Red", "Orange", "Yellow", "Gold", "Light Green", "Dark Green", "Light Blue", "Dark Blue", "Purple", "Pink", "Black" };
                var tireSmokeColors = new Dictionary<string, int[]>()
                {
                    ["Red"] = new int[] { 244, 65, 65 },
                    ["Orange"] = new int[] { 244, 167, 66 },
                    ["Yellow"] = new int[] { 244, 217, 65 },
                    ["Gold"] = new int[] { 181, 120, 0 },
                    ["Light Green"] = new int[] { 158, 255, 84 },
                    ["Dark Green"] = new int[] { 44, 94, 5 },
                    ["Light Blue"] = new int[] { 65, 211, 244 },
                    ["Dark Blue"] = new int[] { 24, 54, 163 },
                    ["Purple"] = new int[] { 108, 24, 192 },
                    ["Pink"] = new int[] { 192, 24, 172 },
                    ["Black"] = new int[] { 1, 1, 1 }
                };
                int smoker = 0, smokeg = 0, smokeb = 0;
                GetVehicleTyreSmokeColor(veh.Handle, ref smoker, ref smokeg, ref smokeb);
                var item = tireSmokeColors.ToList().Find((f) => { return f.Value[0] == smoker && f.Value[1] == smokeg && f.Value[2] == smokeb; });
                var index = tireSmokeColors.ToList().IndexOf(item);
                if (index < 0)
                {
                    index = 0;
                }

                var tireSmoke = new MenuListItem("Tire Smoke Color", tireSmokes, index, $"Choose a ~y~tire smoke color~s~ for your vehicle.");
                VehicleModMenu.AddMenuItem(tireSmoke);

                // Create the checkbox to enable/disable the tiresmoke.
                var tireSmokeEnabled = new MenuCheckboxItem("Tire Smoke", "Enable or disable ~y~tire smoke~s~ for your vehicle. ~h~~r~Important:~s~ When disabling tire smoke, you'll need to drive around before it takes affect.", IsToggleModOn(veh.Handle, 20));
                VehicleModMenu.AddMenuItem(tireSmokeEnabled);

                // Create list for window tint
                var windowTints = new List<string>() { "Stock [1/7]", "None [2/7]", "Limo [3/7]", "Light Smoke [4/7]", "Dark Smoke [5/7]", "Pure Black [6/7]", "Green [7/7]" };
                var currentTint = GetVehicleWindowTint(veh.Handle);
                if (currentTint == -1)
                {
                    currentTint = 4; // stock
                }

                // Convert window tint to the correct index of the list above.
                switch (currentTint)
                {
                    case 0:
                        currentTint = 1; // None
                        break;
                    case 1:
                        currentTint = 5; // Pure Black
                        break;
                    case 2:
                        currentTint = 4; // Dark Smoke
                        break;
                    case 3:
                        currentTint = 3; // Light Smoke
                        break;
                    case 4:
                        currentTint = 0; // Stock
                        break;
                    case 5:
                        currentTint = 2; // Limo
                        break;
                    case 6:
                        currentTint = 6; // Green
                        break;
                    default:
                        break;
                }

                var windowTint = new MenuListItem("Window Tint", windowTints, currentTint, "Apply tint to your windows.");
                VehicleModMenu.AddMenuItem(windowTint);

                #endregion

                #region Checkbox Changes
                // Handle checkbox changes.
                VehicleModMenu.OnCheckboxChange += (sender2, item2, index2, _checked) =>
                {
                    veh = GetVehicle();

                    // Xenon Headlights
                    if (item2 == xenonHeadlights)
                    {
                        ToggleVehicleMod(veh.Handle, 22, _checked);
                    }
                    // Turbo
                    else if (item2 == turbo)
                    {
                        ToggleVehicleMod(veh.Handle, 18, _checked);
                    }
                    // Bullet Proof Tires
                    else if (item2 == bulletProofTires)
                    {
                        SetVehicleTyresCanBurst(veh.Handle, !_checked);
                    }
                    // Low Grip Tyres
                    else if (item2 == lowGripTires)
                    {
                        SetDriftTyresEnabled(veh.Handle, _checked);
                    }
                    // Custom Wheels
                    else if (item2 == toggleCustomWheels)
                    {
                        SetVehicleMod(veh.Handle, 23, GetVehicleMod(veh.Handle, 23), !GetVehicleModVariation(veh.Handle, 23));

                        // If the player is on a motorcycle, also change the back wheels.
                        if (IsThisModelABike((uint)GetEntityModel(veh.Handle)))
                        {
                            SetVehicleMod(veh.Handle, 24, GetVehicleMod(veh.Handle, 24), GetVehicleModVariation(veh.Handle, 23));
                        }
                    }
                    // Toggle Tire Smoke
                    else if (item2 == tireSmokeEnabled)
                    {
                        // If it should be enabled:
                        if (_checked)
                        {
                            // Enable it.
                            ToggleVehicleMod(veh.Handle, 20, true);
                            // Get the selected color values.
                            var r = tireSmokeColors[tireSmokes[tireSmoke.ListIndex]][0];
                            var g = tireSmokeColors[tireSmokes[tireSmoke.ListIndex]][1];
                            var b = tireSmokeColors[tireSmokes[tireSmoke.ListIndex]][2];
                            // Set the color.
                            SetVehicleTyreSmokeColor(veh.Handle, r, g, b);
                        }
                        // If it should be disabled:
                        else
                        {
                            // Set the smoke to white.
                            SetVehicleTyreSmokeColor(veh.Handle, 255, 255, 255);
                            // Disable it.
                            ToggleVehicleMod(veh.Handle, 20, false);
                            // Remove the mod.
                            RemoveVehicleMod(veh.Handle, 20);
                        }
                    }
                };
                #endregion

                #region List Changes
                // Handle list selections
                VehicleModMenu.OnListIndexChange += (sender2, item2, oldIndex, newIndex, itemIndex) =>
                {
                    // Get the vehicle and set the mod kit.
                    veh = GetVehicle();
                    SetVehicleModKit(veh.Handle, 0);

                    #region handle the dynamic (vehicle-specific) mods
                    // If the affected list is actually a "dynamically" generated list, continue. If it was one of the manual options, go to else.
                    if (item2.ItemData is int modType)
                    {
                        var selectedUpgrade = item2.ListIndex - 1;
                        var customWheels = GetVehicleModVariation(veh.Handle, 23);

                        SetVehicleMod(veh.Handle, modType, selectedUpgrade, customWheels);
                    }
                    #endregion
                    // If it was not one of the lists above, then it was one of the manual lists/options selected, 
                    // either: vehicle Wheel Type, tire smoke color, or window tint:
                    #region Handle the items available on all vehicles.
                    // Wheel types
                    else if (item2 == vehicleWheelType)
                    {
                        var vehicleClass = GetVehicleClass(veh.Handle);
                        var isBikeOrOpenWheel = (newIndex == 6 && veh.Model.IsBike) || (newIndex == 10 && vehicleClass == 22);
                        var isNotBikeNorOpenWheel = newIndex != 6 && !veh.Model.IsBike && newIndex != 10 && vehicleClass != 22;
                        var isCorrectVehicleType = isBikeOrOpenWheel || isNotBikeNorOpenWheel;
                        if (!isCorrectVehicleType)
                        {
                            // Go past the index if it's not a bike.
                            if (!veh.Model.IsBike && vehicleClass != 22)
                            {
                                if (newIndex > oldIndex)
                                {
                                    item2.ListIndex++;
                                }
                                else
                                {
                                    item2.ListIndex--;
                                }
                            }
                            // Reset the index to 6 if it is a bike
                            else
                            {
                                item2.ListIndex = veh.Model.IsBike ? 6 : 10;
                            }
                        }
                        // Set the wheel type
                        SetVehicleWheelType(veh.Handle, item2.ListIndex);

                        var customWheels = GetVehicleModVariation(veh.Handle, 23);

                        // Reset the wheel mod index for front wheels
                        SetVehicleMod(veh.Handle, 23, -1, customWheels);

                        // If the model is a bike, do the same thing for the rear wheels.
                        if (veh.Model.IsBike)
                        {
                            SetVehicleMod(veh.Handle, 24, -1, customWheels);
                        }

                        // Refresh the menu with the item index so that the view doesn't change
                        UpdateMods(selectedIndex: itemIndex);
                    }
                    // Tire smoke
                    else if (item2 == tireSmoke)
                    {
                        // Get the selected color values.
                        var r = tireSmokeColors[tireSmokes[newIndex]][0];
                        var g = tireSmokeColors[tireSmokes[newIndex]][1];
                        var b = tireSmokeColors[tireSmokes[newIndex]][2];

                        // Set the color.
                        SetVehicleTyreSmokeColor(veh.Handle, r, g, b);
                    }
                    // Window Tint
                    else if (item2 == windowTint)
                    {
                        // Stock = 4,
                        // None = 0,
                        // Limo = 5,
                        // LightSmoke = 3,
                        // DarkSmoke = 2,
                        // PureBlack = 1,
                        // Green = 6,

                        switch (newIndex)
                        {
                            case 1:
                                SetVehicleWindowTint(veh.Handle, 0); // None
                                break;
                            case 2:
                                SetVehicleWindowTint(veh.Handle, 5); // Limo
                                break;
                            case 3:
                                SetVehicleWindowTint(veh.Handle, 3); // Light Smoke
                                break;
                            case 4:
                                SetVehicleWindowTint(veh.Handle, 2); // Dark Smoke
                                break;
                            case 5:
                                SetVehicleWindowTint(veh.Handle, 1); // Pure Black
                                break;
                            case 6:
                                SetVehicleWindowTint(veh.Handle, 6); // Green
                                break;
                            case 0:
                            default:
                                SetVehicleWindowTint(veh.Handle, 4); // Stock
                                break;
                        }
                    }
                    else if (item2 == headlightColor)
                    {
                        if (newIndex == 13) // default
                        {
                            SetHeadlightsColorForVehicle(veh, 255);
                        }
                        else if (newIndex is > (-1) and < 13)
                        {
                            SetHeadlightsColorForVehicle(veh, newIndex);
                        }
                    }
                    #endregion
                };

                #endregion
            }
            // Refresh Index and update the scaleform to prevent weird broken menus.
            if (selectedIndex == 0)
            {
                VehicleModMenu.RefreshIndex();
            }

            //VehicleModMenu.UpdateScaleform();

            // Set the selected index to the provided index (0 by default)
            // Used for example, when the wheelstype is changed, the menu is refreshed and we want to set the
            // selected item back to the "wheelsType" list so the user doesn't have to scroll down each time they
            // change the wheels type.
            //VehicleModMenu.CurrentIndex = selectedIndex;
        }

        internal static void SetHeadlightsColorForVehicle(Vehicle veh, int newIndex)
        {

            if (veh != null && veh.Exists() && veh.Driver == Game.PlayerPed)
            {
                if (newIndex is > (-1) and < 13)
                {
                    SetVehicleHeadlightsColour(veh.Handle, newIndex);
                }
                else
                {
                    SetVehicleHeadlightsColour(veh.Handle, -1);
                }
            }
        }

        internal static int GetHeadlightsColorForVehicle(Vehicle vehicle)
        {
            if (vehicle != null && vehicle.Exists())
            {
                if (IsToggleModOn(vehicle.Handle, 22))
                {
                    var val = GetVehicleHeadlightsColour(vehicle.Handle);
                    if (val is > (-1) and < 13)
                    {
                        return val;
                    }
                    return -1;
                }
            }
            return -1;
        }
        #endregion

        #region GetColorFromIndex function (underglow)

        private readonly List<int[]> _VehicleNeonLightColors = new()
        {
            { new int[3] { 255, 255, 255 } },   // White
            { new int[3] { 2, 21, 255 } },      // Blue
            { new int[3] { 3, 83, 255 } },      // Electric blue
            { new int[3] { 0, 255, 140 } },     // Mint Green
            { new int[3] { 94, 255, 1 } },      // Lime Green
            { new int[3] { 255, 255, 0 } },     // Yellow
            { new int[3] { 255, 150, 5 } },     // Golden Shower
            { new int[3] { 255, 62, 0 } },      // Orange
            { new int[3] { 255, 0, 0 } },       // Red
            { new int[3] { 255, 50, 100 } },    // Pony Pink
            { new int[3] { 255, 5, 190 } },     // Hot Pink
            { new int[3] { 35, 1, 255 } },      // Purple
            { new int[3] { 15, 3, 255 } },      // Blacklight
        };

        /// <summary>
        /// Converts a list index to a <see cref="System.Drawing.Color"/> struct.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private System.Drawing.Color GetColorFromIndex(int index)
        {
            if (index is >= 0 and < 13)
            {
                return System.Drawing.Color.FromArgb(_VehicleNeonLightColors[index][0], _VehicleNeonLightColors[index][1], _VehicleNeonLightColors[index][2]);
            }
            return System.Drawing.Color.FromArgb(255, 255, 255);
        }

        /// <summary>
        /// Returns the color index that is applied on the current vehicle. 
        /// If a color is active on the vehicle which is not in the list, it'll return the default index 0 (white).
        /// </summary>
        /// <returns></returns>
        private int GetIndexFromColor()
        {
            var veh = GetVehicle();

            if (veh == null || !veh.Exists() || !veh.Mods.HasNeonLights)
            {
                return 0;
            }

            int r = 255, g = 255, b = 255;

            GetVehicleNeonLightsColour(veh.Handle, ref r, ref g, ref b);

            if (r == 255 && g == 0 && b == 255) // default return value when the vehicle has no neon kit selected.
            {
                return 0;
            }

            if (_VehicleNeonLightColors.Any(a => { return a[0] == r && a[1] == g && a[2] == b; }))
            {
                return _VehicleNeonLightColors.FindIndex(a => { return a[0] == r && a[1] == g && a[2] == b; });
            }

            return 0;
        }
        #endregion

        private bool IsVehicleTooDamagedToChangeExtras(Vehicle vehicle)
        {
            float bodyHealth = vehicle.BodyHealth;
            float engineHealth = vehicle.EngineHealth;
            float allowedBodyHealth = GetSettingsInt(Setting.vmenu_allowed_body_damage_for_extra_change);
            float allowedEngineHealth = GetSettingsInt(Setting.vmenu_allowed_engine_damage_for_extra_change);

            return bodyHealth < allowedBodyHealth || engineHealth < allowedEngineHealth;
        }
    }
}
