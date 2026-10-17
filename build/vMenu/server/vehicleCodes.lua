-- Saved vehicle share codes. The plumbing (cooldowns, quota, size cap, convar gate) is in server/lib/codes.lua.

-- The fields the saved vehicles menu reads (CommonFunctions.VehicleInfo), and what each must be.
local integerFields = { livery = true, plateStyle = true, version = true, wheelType = true, windowTint = true, headlightColor = true }
local booleanFields = {
    customWheels = true, neonBack = true, neonFront = true, neonLeft = true, neonRight = true,
    turbo = true, tyreSmoke = true, xenonHeadlights = true, bulletProofTires = true,
}

local function isVehicle(data)
    if type(data) ~= 'table' or not ShareCodes.isInteger(data.model, 1, 0xFFFFFFFF) then return false end

    for field in pairs(integerFields) do
        if data[field] ~= nil and not ShareCodes.isInteger(data[field], -1, 100000) then return false end
    end
    for field in pairs(booleanFields) do
        if data[field] ~= nil and type(data[field]) ~= 'boolean' then return false end
    end
    if data.enveffScale ~= nil and not ShareCodes.isNumber(data.enveffScale) then return false end
    if data.name ~= nil and not ShareCodes.isString(data.name, 64) then return false end
    if data.plateText ~= nil and not ShareCodes.isString(data.plateText, 16) then return false end
    if data.Category ~= nil and not ShareCodes.isString(data.Category, 64) then return false end

    if data.mods ~= nil and not ShareCodes.eachEntry(data.mods, 64, function(modType, index)
            return ShareCodes.isInteger(tonumber(modType), -1, 100) and ShareCodes.isInteger(index, -1, 1000)
        end) then
        return false
    end
    if data.colors ~= nil and not ShareCodes.eachEntry(data.colors, 32, function(name, value)
            return ShareCodes.isString(name, 32) and ShareCodes.isInteger(value, -1, 1000)
        end) then
        return false
    end
    if data.extras ~= nil and not ShareCodes.eachEntry(data.extras, 32, function(extra, enabled)
            return ShareCodes.isInteger(tonumber(extra), 0, 31) and type(enabled) == 'boolean'
        end) then
        return false
    end
    return true
end

ShareCodes.register({
    name = 'Vehicles',
    label = 'vehicle',
    table = 'vmenu_vehicles',
    convar = 'vmenu_vehicle_codes',
    validate = isVehicle,
})
