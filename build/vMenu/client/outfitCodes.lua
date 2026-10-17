-- MP ped outfit share codes. Shared helpers: client/lib/codes.lua. The outfit is checked against the
-- player's current ped and their EUP permission by the C# side (MpPedCustomization.cs) before it is saved
-- or worn.

local CONVAR = 'vmenu_outfit_codes'
local PED_PREFIX = 'mp_ped_'

---Asks for a code and saves the outfit as a new MP character based on the one currently loaded.
---Used by the MP Ped Customization menu.
VMenuBridge.export('loadSharedOutfit', function()
    if not ShareCodes.enabled(CONVAR) then
        ShareCodes.notify('Outfit codes are disabled on this server.')
        return false
    end
    if not ShareCodes.claim('outfit:load', 5000) then
        ShareCodes.notify('You must wait before loading another outfit!')
        return false
    end

    local id, name = ShareCodes.prompt('Outfit')
    if not id then
        ShareCodes.notify('You must enter a valid outfit code and a save name!')
        return false
    end
    if GetResourceKvpString(PED_PREFIX .. name) then
        ShareCodes.notify('You have an outfit saved with that name already!')
        return false
    end

    local data, reason = ShareCodes.fetch('Outfits', id)
    if not data then
        ShareCodes.notify(reason)
        return false
    end

    local ok, message = ShareCodes.apply('ImportSharedOutfit', name, data)
    if not ok then
        ShareCodes.notify(message)
        return false
    end

    ShareCodes.notify('Outfit has been successfully loaded!', 'success')
    return true
end)

---Puts the outfit behind a code on the player's current ped without saving it. For other resources and
---the /loadoutfitfromcode command.
---@param outfitCode integer
VMenuBridge.export('loadOutfitFromCode', function(outfitCode)
    local id = ShareCodes.toId(outfitCode)
    if not id then
        lib.print.error('loadOutfitFromCode: a positive integer code is required.')
        return false
    end
    if not ShareCodes.enabled(CONVAR) then
        return false
    end
    if not ShareCodes.claim('outfit:load', 5000) then
        ShareCodes.notify('You must wait before loading another outfit!')
        return false
    end

    local data, reason = ShareCodes.fetch('Outfits', id)
    if not data then
        lib.print.debug(('loadOutfitFromCode: could not load code %s: %s'):format(id, reason))
        return false
    end

    local ok, message = ShareCodes.apply('ApplySharedOutfit', data)
    if not ok then
        ShareCodes.notify(message)
        return false
    end
    return true
end)

if Config.OutfitSharing.CommandEnabled then
    TriggerEvent('chat:addSuggestion', '/' .. Config.OutfitSharing.CommandName, 'Wear an outfit from a code', {
        { name = 'code', help = 'The outfit code you were given.' }
    })
    RegisterCommand(Config.OutfitSharing.CommandName, function(_, args)
        local id = ShareCodes.toId(args[1])
        if #args ~= 1 or not id then
            ShareCodes.notify(('Invalid usage!\n\n/%s <code>'):format(Config.OutfitSharing.CommandName))
            return
        end
        exports[GetCurrentResourceName()]:loadOutfitFromCode(id)
    end, false)
end

-- Fired by the MP Ped Customization menu with the character's full KVP key (mp_ped_<name>).
AddEventHandler('vMenu:Outfits:GenerateCode', function(saveName)
    if type(saveName) ~= 'string' or not ShareCodes.enabled(CONVAR) then
        return
    end
    if not ShareCodes.claim('outfit:generate', 15000) then
        ShareCodes.notify('You must wait before generating another outfit code!')
        return
    end

    local saved = GetResourceKvpString(saveName)
    local character = saved and json.decode(saved)
    if type(character) ~= 'table' then
        ShareCodes.notify('You do not have a MP ped saved with this name!')
        return
    end

    -- Only the clothing and props are shared, never the face or the rest of the character.
    local outfit = { Clothes = {}, Props = {} }
    local clothes = character.DrawableVariations and character.DrawableVariations.clothes or {}
    local props = character.PropVariations and character.PropVariations.props or {}
    for slot, item in pairs(clothes) do
        outfit.Clothes[tostring(slot)] = { Item = item.Key, Texture = item.Value }
    end
    for slot, item in pairs(props) do
        outfit.Props[tostring(slot)] = { Item = item.Key, Texture = item.Value }
    end

    local id, reason = ShareCodes.create('Outfits', json.encode(outfit))
    if not id then
        ShareCodes.notify(reason or 'Failed to generate outfit code!')
        return
    end
    ShareCodes.announce('Outfit', id)
end)
