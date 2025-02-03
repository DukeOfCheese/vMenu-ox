local Cooldown = false
local GenerateCooldown = false

---@class loadSharedVehicle
exports("loadSharedVehicle", function()
    
    if Cooldown then
        Config.Notify("vMenu", "You must wait before loading another vehicle!", "error", 6500)
        return false
    end

    local input = lib.inputDialog("Enter Vehicle Code", {
        { type = 'number', label = 'Vehicle Code', description = "The sharing code you were given.", icon = 'hashtag', required = true },
        { type = 'input', label = 'Save Name', description = "What should we save this vehicle as?", icon = 'tag', max = 30, min = 3, required = true },
    })

    if not input then
        Config.Notify("vMenu", "You must enter a valid vehicle code!", "error", 6500)
        return false
    end

    local code = input[1]
    local newName = input[2]
    local nameExists = GetResourceKvpString(format("veh_%s", newName))
    if nameExists then
        Config.Notify("vMenu", "You have a vehicle saved with that name already!", "error", 6500)
        return false
    end

    local Valid = lib.callback.await("vMenu:Vehicles:Request", false, code)
    Cooldown = true
    SetTimeout(5000, function()
        Cooldown = false
        lib.print.debug("Vehicle load cooldown expired.")
    end)

    if not Valid then
        Config.Notify("vMenu", "The vehicle code you entered is invalid!", "error", 6500)
        return false
    end

    local decoded = json.decode(Valid)
    local vehicleClass = GetVehicleClassFromName(decoded.model)
    local vehicleClassLabel = "Not Found"
    if IsModelInCdimage(decoded.model) and IsModelValid(decoded.model) then
        -- in case this vehicle save is for a car that is disabled / not being streamed currently
        vehicleClassLabel = GetFilenameForAudioConversation(format("VEH_CLASS_%s", vehicleClass))
    end

    if json.encode(decoded.extras) == "[]" then
        -- fix json fuckery from db
        decoded.extras = json.decode("{}")
        Valid = json.encode(decoded)
    end

    SetResourceKvp(format("veh_%s", newName), Valid)
    Config.Notify("vMenu", format("Name: **%s** \n\n Category: **Uncategorized** \n\n Class: **%s**", newName, vehicleClassLabel), "success", 6500)

    return true

end)

---@class generateVehicleCode
---@param saveName string
AddEventHandler("vMenu:Vehicles:GenerateCode", function(saveName)

    if not saveName or type(saveName) ~= "string" then
        lib.print.error("export: generateVehicleCode - saveName is required and must be a string.")
        return false
    end

    if GenerateCooldown then
        Config.Notify("vMenu", "You must wait before generating another vehicle code!", "error", 6500)
        return false
    end

    local vehicleData = GetResourceKvpString(format("veh_%s", saveName))
    if not vehicleData then
        lib.print.debug("Vehicle data does not exist somehow?")
        Config.Notify("vMenu", "Error fetching vehicle data.", "error", 6500)
        return false
    end

    local decoded = json.decode(vehicleData)
    decoded.Category = "Uncategorized" -- We don't want to break something by having the origin user having a custom category name that doesnt exist on other clients

    local generated, error = lib.callback.await("vMenu:Vehicles:Generate", false, decoded)
    GenerateCooldown = true
    SetTimeout(15000, function()
        GenerateCooldown = false
        lib.print.debug("Vehicle generate cooldown expired.")
    end)

    if not generated then
        Config.Notify("vMenu", error or "Failed to generate vehicle code.", "error", 6500)
        return false
    end

    print(format("Vehicle code generated: %s", generated))
    Config.Notify("vMenu", format("Vehicle code has been generated - id: #%s", generated), "success", 6500)

end)