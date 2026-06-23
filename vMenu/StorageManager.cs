using System.Collections.Generic;

using CitizenFX.Core;

using Newtonsoft.Json;

using static CitizenFX.Core.Native.API;
using static vMenuClient.CommonFunctions;

namespace vMenuClient
{

    public static class StorageManager
    {
        /// <summary>
        /// Save Dictionary(string, string) to local storage.
        /// </summary>
        /// <param name="saveName">Name (including prefix) to save.</param>
        /// <param name="data">Data (dictionary) to save.</param>
        /// <param name="overrideExistingData">When true, will override existing save data with the same name. 
        /// If false, it will cancel the save if existing data is found and return false.</param>
        /// <returns>A boolean value indicating if the save was successful.</returns>
        public static bool SaveDictionary(string saveName, Dictionary<string, string> data, bool overrideExistingData)
        {
            // If the savename doesn't exist yet or we're allowed to override it.
            if (GetResourceKvpString(saveName) == null || overrideExistingData)
            {
                // Get the json string from the dictionary.
                //string jsonString = CommonFunctions.DictionaryToJson(data);
                var jsonString = JsonConvert.SerializeObject(data);
                Log($"Saving: [name: {saveName}, json:{jsonString}]");

                // Save the kvp.
                SetResourceKvp(saveName, jsonString);

                // Return true if the kvp was set successfully, false if it wasn't set successfully.
                return GetResourceKvpString(saveName) == jsonString;
            }
            // If the data already exists and we are not allowed to override it.
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a collection of saved peds.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, PedInfo> GetSavedPeds()
        {
            var savedPeds = new Dictionary<string, PedInfo>();

            var handle = StartFindKvp("ped_");
            try
            {
                while (true)
                {
                    var kvp = FindKvp(handle);
                    if (string.IsNullOrEmpty(kvp))
                    {
                        break;
                    }
                    try
                    {
                        savedPeds.Add(kvp, JsonConvert.DeserializeObject<PedInfo>(GetResourceKvpString(kvp)));
                    }
                    catch (JsonException e)
                    {
                        Debug.WriteLine(e.Message);
                        continue;
                    }
                }
            }
            finally
            {
                EndFindKvp(handle);
            }
            return savedPeds;
        }

        /// <summary>
        /// Returns a <see cref="PedInfo"/> struct containing the data of the saved ped.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static PedInfo GetSavedPedInfo(string name)
        {
            return JsonToPedInfo(GetResourceKvpString(name));
        }

        /// <summary>
        /// Saves an (old/nomral) ped data to storage.
        /// </summary>
        /// <param name="saveName"></param>
        /// <param name="pedData"></param>
        /// <param name="overrideExisting"></param>
        /// <returns></returns>
        public static bool SavePedInfo(string saveName, PedInfo pedData, bool overrideExisting)
        {
            if (overrideExisting || string.IsNullOrEmpty(GetResourceKvpString(saveName)))
            {
                var json = JsonConvert.SerializeObject(pedData);
                SetResourceKvp(saveName, json);
                return GetResourceKvpString(saveName) == json;
            }
            return false;

        }

        public static List<MpPedDataManager.MultiplayerPedData> GetSavedMpPeds()
        {
            var peds = new List<MpPedDataManager.MultiplayerPedData>();
            var handle = StartFindKvp("mp_ped_");
            while (true)
            {
                var foundName = FindKvp(handle);
                if (string.IsNullOrEmpty(foundName))
                {
                    break;
                }
                else
                {
                    peds.Add(GetSavedMpCharacterData(foundName));
                }
            }
            EndFindKvp(handle);
            peds.Sort((a, b) => a.SaveName.ToLower().CompareTo(b.SaveName.ToLower()));
            return peds;
        }

        /// <summary>
        /// Delete the specified saved item from local storage.
        /// </summary>
        /// <param name="saveName">The full name of the item to remove.</param>
        public static void DeleteSavedStorageItem(string saveName)
        {
            DeleteResourceKvp(saveName);
        }

        /// <summary>
        /// New function used to save vehicle info to local storage.
        /// </summary>
        /// <param name="saveName"></param>
        /// <param name="vehicleInfo"></param>
        /// <param name="overrideOldVersion"></param>
        /// <returns></returns>
        public static bool SaveVehicleInfo(string saveName, VehicleInfo vehicleInfo, bool overrideOldVersion)
        {
            if (string.IsNullOrEmpty(GetResourceKvpString(saveName)) || overrideOldVersion)
            {
                if (!string.IsNullOrEmpty(saveName) && saveName.Length > 4)
                {
                    // convert
                    var json = JsonConvert.SerializeObject(vehicleInfo);

                    // log
                    Log($"[vMenu] Saving!\nName: {saveName}\nVehicle Data: {json}\n");

                    // save
                    SetResourceKvp(saveName, json);

                    // confirm
                    return GetResourceKvpString(saveName) == json;
                }
            }
            // if something isn't right, then the save is aborted and return false ("failed" state).
            return false;
        }

        /// <summary>
        /// New function to get vehicle information from a saved vehicle.
        /// </summary>
        /// <param name="saveName">Saved vehicle name to get info from. (name includes "veh_")</param>
        /// <returns></returns>
        public static VehicleInfo GetSavedVehicleInfo(string saveName)
        {
            var json = GetResourceKvpString(saveName);
            return JsonConvert.DeserializeObject<VehicleInfo>(json);
        }

        /// <summary>
        /// Save json data. Returns true if save was successfull.
        /// </summary>
        /// <param name="saveName">Name to store the data under.</param>
        /// <param name="jsonData">The data to store.</param>
        /// <param name="overrideExistingData">If the saveName is already in use, can we override it?</param>
        /// <returns>Whether or not the data was saved successfully.</returns>
        public static bool SaveJsonData(string saveName, string jsonData, bool overrideExistingData)
        {
            if (!string.IsNullOrEmpty(saveName) && !string.IsNullOrEmpty(jsonData))
            {
                var existingData = GetResourceKvpString(saveName); // check for existing data.

                if (!string.IsNullOrEmpty(existingData)) // data already exists for this save name.
                {
                    if (!overrideExistingData)
                    {
                        return false; // data already exists, and we are not allowed to override it.
                    }
                }

                // write data.
                SetResourceKvp(saveName, jsonData);

                // return true if the data is successfully written, otherwise return false.
                return (GetResourceKvpString(saveName) ?? "") == jsonData;
            }
            return false; // input parameters are invalid.
        }

        /// <summary>
        /// Returns the saved json data for the provided save name. Returns null if no data exists.
        /// </summary>
        /// <param name="saveName"></param>
        /// <returns></returns>
        public static string GetJsonData(string saveName)
        {
            if (!string.IsNullOrEmpty(saveName))
            {
                //Debug.WriteLine("not null");
                var data = GetResourceKvpString(saveName);
                //Debug.Write(data + "\n");
                if (!string.IsNullOrEmpty(data))
                {
                    return data;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a <see cref="MpPedDataManager.MultiplayerPedData"/> struct containing the data of the saved MP Character.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static MpPedDataManager.MultiplayerPedData GetSavedMpCharacterData(string name)
        {
            var output = new MpPedDataManager.MultiplayerPedData();
            if (string.IsNullOrEmpty(name))
            {
                return output;
            }
            var jsonString = GetResourceKvpString(name.StartsWith("mp_ped_") ? name : "mp_ped_" + name);
            if (string.IsNullOrEmpty(jsonString))
            {
                return output;
            }
            try
            {
                output = JsonConvert.DeserializeObject<MpPedDataManager.MultiplayerPedData>(jsonString);
            }
            catch (JsonException e)
            {
                Debug.WriteLine(e.Message);
            }
            Log(jsonString);
            return output;
        }

        /// <summary>
        /// Returns a <see cref="MpCharacterCategory"/> struct containing the data of the saved MP Character Category.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static menus.MpPedCustomization.MpCharacterCategory GetSavedMpCharacterCategoryData(string name)
        {
            var output = new menus.MpPedCustomization.MpCharacterCategory();
            if (string.IsNullOrEmpty(name))
            {
                return output;
            }
            var jsonString = GetResourceKvpString(name.StartsWith("mp_character_category_") ? name : "mp_character_category_" + name);
            if (string.IsNullOrEmpty(jsonString))
            {
                return output;
            }
            try
            {
                output = JsonConvert.DeserializeObject<menus.MpPedCustomization.MpCharacterCategory>(jsonString);
            }
            catch (JsonException e)
            {
                Debug.WriteLine(e.Message);
            }
            Log(jsonString);
            return output;
        }

        /// <summary>
        /// Returns a <see cref="SavedVehicleCategory"/> struct containing the data of the saved Vehicle Category.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static menus.SavedVehicles.SavedVehicleCategory GetSavedVehicleCategoryData(string name)
        {
            var output = new menus.SavedVehicles.SavedVehicleCategory();
            if (string.IsNullOrEmpty(name))
            {
                return output;
            }
            var jsonString = GetResourceKvpString(name.StartsWith("saved_veh_category_") ? name : "saved_veh_category_" + name);
            if (string.IsNullOrEmpty(jsonString))
            {
                return output;
            }
            try
            {
                output = JsonConvert.DeserializeObject<menus.SavedVehicles.SavedVehicleCategory>(jsonString);
            }
            catch (JsonException e)
            {
                Debug.WriteLine(e.Message);
            }
            Log(jsonString);
            return output;
        }
    }
}
