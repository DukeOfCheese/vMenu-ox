-- Weapon loadout share codes. The plumbing (cooldowns, quota, size cap, convar gate) is in server/lib/codes.lua.

local function isWeapon(index, weapon, count)
    if not ShareCodes.isInteger(index, 1, count) or type(weapon) ~= 'table' then return false end
    -- Weapon hashes are stored unsigned.
    if not ShareCodes.isInteger(weapon.Hash, 0, 0xFFFFFFFF) then return false end
    if weapon.Components ~= nil and not ShareCodes.eachEntry(weapon.Components, 64, function(name, hash)
            return ShareCodes.isString(name, 64) and ShareCodes.isInteger(hash, 0, 0xFFFFFFFF)
        end) then
        return false
    end
    if weapon.CurrentAmmo ~= nil and not ShareCodes.isInteger(weapon.CurrentAmmo, -1, 100000) then return false end
    if weapon.CurrentTint ~= nil and not ShareCodes.isInteger(weapon.CurrentTint, -1, 64) then return false end
    return true
end

ShareCodes.register({
    name = 'Loadouts',
    label = 'loadout',
    table = 'vmenu_loadouts',
    convar = 'vmenu_loadout_codes',
    validate = function(data)
        if type(data) ~= 'table' then return false end
        local count = #data
        if count == 0 or count > 128 then return false end
        return ShareCodes.eachEntry(data, count, function(index, weapon)
            return isWeapon(index, weapon, count)
        end)
    end,
})
