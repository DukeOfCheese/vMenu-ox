---@class infiniteFuelToggled
---@field enabled boolean
AddEventHandler("vMenu:Integrations:InfiniteFuel", function(enabled)
    lib.print.debug("Infinite Fuel: " .. tostring(enabled))
end)

---@class licensePlateUpdated
---@field vehicleHandle integer
---@field plate string
AddEventHandler("vMenu:Integrations:LicensePlateUpdated", function(handle, plate)
    lib.print.debug("License Plate Updated: " .. handle .. " - " .. plate)
    --[[
        Example Usage:
        if doesTextContainBlacklistedWord(plate) then
            SetVehicleNumberPlateText(handle, "PLATE")
            TriggerServerEvent("banplayer")
        end
    --]]
end)

---@class canDoInteraction
---@return boolean Returns true if the player can do the interaction, false otherwise
exports("canDoInteraction", function()
    --[[
        Example Usage:
        if LocalPlayer.state.dead then
            return false
        end
    --]]

    return true
end)
