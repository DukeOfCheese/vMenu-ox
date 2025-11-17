local Cooldown = false
local GenerateCooldown = false
format = string.format

---@class loadSharedLoadout
exports('loadSharedLoadout', function()
    if Cooldown then
        Config.Notify('vMenu', 'You must wait before loading another loadout!', 'error', 6500)
        return false
    end

    local input = lib.inputDialog('Enter Loadout Code', {
        { type = 'number', label = 'Loadout Code', description = 'Shared code you were given', icon = 'hashtag', required = true },
        { type = 'input', label = 'Save Name', description = 'What should we save this loadout as?', icon = 'tag', max = 30, min = 3, required = true },
    })

    if not input then
        Config.Notify('vMenu', 'You must enter a valid loadout code!', 'error', 6500)
        return false
    end

    local code = input[1]
    local newName = input[2]

    local nameExists = GetResourceKvpString(format('vmenu_string_saved_weapon_loadout_%s', newName))
    if nameExists then
        Config.Notify('vMenu', 'You have a weapon loadout with that name already!', 'error', 6500)
        return false
    end

    local Valid = lib.callback.await('vMenu:Loadouts:Request', false, code)
    Cooldown = true
    SetTimeout(5000, function()
        Cooldown = false
        lib.print.debug('Loadout load cooldown expired.')
    end)

    print(Valid)
    print(type(Valid))

    if not Valid then
        Config.Notify('vMenu', 'The loadout code you entered is invalid!', 'error', 6500)
    end

    local validData = json.decode(Valid)

    for _, weapon in ipairs(validData) do
        GiveWeaponToPed(cache.ped, weapon.Hash, weapon.GetMaxAmmo, true, false)

        if weapon.Components and next(weapon.Components) ~= nil then
            for compName, compHash in pairs(weapon.Components) do
                GiveWeaponComponentToPed(cache.ped, weapon.Hash, compHash)
            end
        end
    end

    for _, weapon in ipairs(validData) do
        if type(weapon.Components) ~= "table" or next(weapon.Components) == nil then
            weapon.Components = {}
        end
    end

    local saveJson = json.encode(validData)
    SetResourceKvp(string.format('vmenu_string_saved_weapon_loadout_%s', newName), saveJson)

    Config.Notify("vMenu", "Weapon loadout has been successfully loaded!", "success", 6500)

    return true
end)

RegisterCommand("clearlayouts", function()
    local prefix = "vmenu_string_saved_weapon_loadout_"

    local handle = StartFindKvp(prefix)
    local key = nil
    local count = 0

    repeat
        key = FindKvp(handle)
        if key then
            DeleteResourceKvpNoSync(key)
            count = count + 1
        end
    until not key

    EndFindKvp(handle)

    print(("Deleted %d saved weapon loadouts."):format(count))
end)


exports('loadLoadoutFromCode', function(loadoutCode)
    if not loadoutCode then
        return lib.print.error('export: Attempt to load loadout failed, no loadout code provided.')
    end

    if type(loadoutCode) ~= 'number' then
        return lib.print.error('export: Attempt to load loadout failed, invalid code provided. (NaN)')
    end

    local Valid = lib.callback.await('vMenu:Loadouts:Request', false, loadoutCode)
    if not Valid then
        lib.print.debug(format('export: Failed to load outfit with code: %s', loadoutCode))
        return false
    end

    local validData = json.decode(Valid)

    for _, weapon in ipairs(validData) do
        GiveWeaponToPed(cache.ped, weapon.Hash, weapon.GetMaxAmmo, true, false)

        if weapon.Components and next(weapon.Components) ~= nil then
            for compName, compHash in pairs(weapon.Components) do
                GiveWeaponComponentToPed(cache.ped, weapon.Hash, compHash)
            end
        end
    end

    Config.Notify("vMenu", "Weapon loadout has been successfully loaded!", "success", 6500)

    return true
end)

if Config.LoadoutSharing.CommandEnabled then
    TriggerEvent("chat:addSuggestion", Config.LoadoutSharing.CommandName, "Load an outfit from a code", {
        { name = "code", help = "The outfit code you were given." }
    })
    RegisterCommand(Config.LoadoutSharing.CommandName, function(source, args)
        if #args ~= 1 then
            Config.Notify("vMenu", "Invalid Usage! \n\n /" .. Config.LoadoutSharing.CommandName .. " <code>", "error", 6500)
            return
        end
        if not tonumber(args[1]) then
            Config.Notify("vMenu", "You must provide a valid loadout code!", "error", 6500)
            return
        end
        exports[GetCurrentResourceName()]:loadLoadoutFromCode(tonumber(args[1]))
    end, false)
end

AddEventHandler('vMenu:Loadout:GenerateCode', function(name)
    if GenerateCooldown then
        Config.Notify('vMenu', 'You must wait before generating another loadout code!', 'error', 6500)
        return
    end

    local Existing = GetResourceKvpString(format('vmenu_string_saved_weapon_loadout_%s', name))
    if not Existing then
        Config.Notify('vMenu', 'You do not have a weapon loadout saved with this name!', 'error', 6500)
        return
    end

    local Data = json.decode(Existing)

    local Generated, errorMessage = lib.callback.await('vMenu:Loadouts:Generate', false, Data)
    GenerateCooldown = true
    SetTimeout(15000, function()
        GenerateCooldown = false
        lib.print.debug('Loadout load cooldown expired.')
    end)

    if not Generated then
        Config.Notify('vMenu', errorMessage or 'Failed to generate loadout code!', 'error', 6500)
        return
    end

    print(format('Loadout code created: %s', Generated))
    Config.Notify('vMenu', 'Loadout code has been generated #' .. Generated, 'success', 10000)
    lib.setClipboard(Generated)
end)