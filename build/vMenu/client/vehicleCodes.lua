-- Saved vehicle share codes. Shared helpers: client/lib/codes.lua. The vehicle data is checked and
-- normalised by the C# side (SharedCodes.cs) before it is saved; spawning it later goes through the same
-- checks as any other saved vehicle.

local CONVAR = 'vmenu_vehicle_codes'
local VEHICLE_PREFIX = 'veh_'

---Asks for a code and saves the vehicle under a new name. Used by the Saved Vehicles menu.
exports('loadSharedVehicle', function()
    if not ShareCodes.enabled(CONVAR) then
        ShareCodes.notify('Vehicle codes are disabled on this server.')
        return false
    end
    if not ShareCodes.claim('vehicle:load', 5000) then
        ShareCodes.notify('You must wait before loading another vehicle!')
        return false
    end

    local id, name = ShareCodes.prompt('Vehicle')
    if not id then
        ShareCodes.notify('You must enter a valid vehicle code and a save name!')
        return false
    end
    if GetResourceKvpString(VEHICLE_PREFIX .. name) then
        ShareCodes.notify('You have a vehicle saved with that name already!')
        return false
    end

    local data, reason = ShareCodes.fetch('Vehicles', id)
    if not data then
        ShareCodes.notify(reason)
        return false
    end

    local ok, message = ShareCodes.apply('ImportSharedVehicle', name, data)
    if not ok then
        ShareCodes.notify(message)
        return false
    end

    ShareCodes.notify(('Vehicle saved as **%s** in **Uncategorized**.'):format(name), 'success')
    return true
end)

-- Fired by the Saved Vehicles menu with the vehicle's name, without the KVP prefix.
AddEventHandler('vMenu:Vehicles:GenerateCode', function(name)
    if type(name) ~= 'string' or not ShareCodes.enabled(CONVAR) then
        return
    end
    if not ShareCodes.claim('vehicle:generate', 15000) then
        ShareCodes.notify('You must wait before generating another vehicle code!')
        return
    end

    local saved = GetResourceKvpString(VEHICLE_PREFIX .. name)
    if not saved then
        ShareCodes.notify('Error fetching vehicle data.')
        return
    end

    local id, reason = ShareCodes.create('Vehicles', saved)
    if not id then
        ShareCodes.notify(reason or 'Failed to generate vehicle code.')
        return
    end
    ShareCodes.announce('Vehicle', id)
end)
