-- Weapon loadout share codes. Shared helpers: client/lib/codes.lua. The loadout is rebuilt and equipped by
-- the C# side (SharedCodes.cs), which applies this player's weapon permissions and the "spawnloadout"
-- interaction check, so a code can never hand out a weapon the player couldn't spawn themselves.

local CONVAR = 'vmenu_loadout_codes'
local LOADOUT_PREFIX = 'vmenu_string_saved_weapon_loadout_'

---Asks for a code, saves the loadout under a new name and equips it. Used by the Weapon Loadouts menu.
exports('loadSharedLoadout', function()
    if not ShareCodes.enabled(CONVAR) then
        ShareCodes.notify('Loadout codes are disabled on this server.')
        return false
    end
    if not ShareCodes.claim('loadout:load', 5000) then
        ShareCodes.notify('You must wait before loading another loadout!')
        return false
    end

    local id, name = ShareCodes.prompt('Loadout')
    if not id then
        ShareCodes.notify('You must enter a valid loadout code and a save name!')
        return false
    end
    if GetResourceKvpString(LOADOUT_PREFIX .. name) then
        ShareCodes.notify('You have a weapon loadout with that name already!')
        return false
    end

    local data, reason = ShareCodes.fetch('Loadouts', id)
    if not data then
        ShareCodes.notify(reason)
        return false
    end

    local ok, message = ShareCodes.apply('ImportSharedLoadout', name, data)
    if not ok then
        ShareCodes.notify(message)
        return false
    end

    ShareCodes.notify('Weapon loadout has been successfully loaded!', 'success')
    return true
end)

---Equips the loadout behind a code without saving it. For other resources and the /loadloadoutfromcode command.
---@param loadoutCode integer
exports('loadLoadoutFromCode', function(loadoutCode)
    local id = ShareCodes.toId(loadoutCode)
    if not id then
        lib.print.error('loadLoadoutFromCode: a positive integer code is required.')
        return false
    end
    if not ShareCodes.enabled(CONVAR) then
        return false
    end
    if not ShareCodes.claim('loadout:load', 5000) then
        ShareCodes.notify('You must wait before loading another loadout!')
        return false
    end

    local data, reason = ShareCodes.fetch('Loadouts', id)
    if not data then
        lib.print.debug(('loadLoadoutFromCode: could not load code %s: %s'):format(id, reason))
        return false
    end

    local ok, message = ShareCodes.apply('EquipSharedLoadout', data)
    if not ok then
        ShareCodes.notify(message)
        return false
    end
    return true
end)

---Deletes every saved weapon loadout, after asking.
local function clearLoadouts()
    local confirmed = lib.alertDialog({
        header = 'vMenu - Delete all weapon loadouts',
        content = 'This deletes every weapon loadout you have saved. It cannot be undone.',
        centered = true,
        cancel = true,
    })
    if confirmed ~= 'confirm' then
        return
    end

    -- Collect first: deleting while a KVP search is open skips entries.
    local keys = {}
    local handle = StartFindKvp(LOADOUT_PREFIX)
    repeat
        local key = FindKvp(handle)
        if key then
            keys[#keys + 1] = key
        end
    until not key
    EndFindKvp(handle)

    for _, key in ipairs(keys) do
        DeleteResourceKvpNoSync(key)
    end
    FlushResourceKvp()

    ShareCodes.notify(('Deleted %d saved weapon loadouts.'):format(#keys), 'success')
end
RegisterCommand('clearloadouts', clearLoadouts, false)
-- The original (misspelt) command name, kept so existing habits and binds still work.
RegisterCommand('clearlayouts', clearLoadouts, false)

if Config.LoadoutSharing.CommandEnabled then
    TriggerEvent('chat:addSuggestion', '/' .. Config.LoadoutSharing.CommandName, 'Equip a weapon loadout from a code', {
        { name = 'code', help = 'The loadout code you were given.' }
    })
    RegisterCommand(Config.LoadoutSharing.CommandName, function(_, args)
        local id = ShareCodes.toId(args[1])
        if #args ~= 1 or not id then
            ShareCodes.notify(('Invalid usage!\n\n/%s <code>'):format(Config.LoadoutSharing.CommandName))
            return
        end
        exports[GetCurrentResourceName()]:loadLoadoutFromCode(id)
    end, false)
end

-- Fired by the Weapon Loadouts menu (WeaponLoadouts.cs) with the loadout's name, without the KVP prefix.
AddEventHandler('vMenu:Loadout:GenerateCode', function(name)
    if type(name) ~= 'string' or not ShareCodes.enabled(CONVAR) then
        return
    end
    if not ShareCodes.claim('loadout:generate', 15000) then
        ShareCodes.notify('You must wait before generating another loadout code!')
        return
    end

    local saved = GetResourceKvpString(LOADOUT_PREFIX .. name)
    if not saved then
        ShareCodes.notify('You do not have a weapon loadout saved with this name!')
        return
    end

    local id, reason = ShareCodes.create('Loadouts', saved)
    if not id then
        ShareCodes.notify(reason or 'Failed to generate loadout code!')
        return
    end
    ShareCodes.announce('Loadout', id)
end)
