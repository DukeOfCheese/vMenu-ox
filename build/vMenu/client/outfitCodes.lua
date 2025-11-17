local Cooldown = false
local GenerateCooldown = false
format = string.format

local function isValidComp(ped, comp, drawable, texture)
    local maxDraw = GetNumberOfPedDrawableVariations(ped, comp)
    if drawable < 0 or drawable >= maxDraw then return false end

    local maxTex = GetNumberOfPedTextureVariations(ped, comp, drawable)
    if texture < 0 or texture >= maxTex then return false end

    return true
end

---@class loadSharedOutfit
exports("loadSharedOutfit", function()
    if Cooldown then
        Config.Notify("vMenu", "You must wait before loading another outfit!", "error", 6500)
        return false
    end

    local input = lib.inputDialog("Enter Outfit Code", {
        { type = 'number', label = 'Outfit Code', description = "The sharing code you were given.", icon = 'hashtag', required = true },
        { type = 'input', label = 'Save Name', description = "What should we save this outfit as?", icon = 'tag', max = 30, min = 3, required = true },
    })

    if not input then
        Config.Notify("vMenu", "You must enter a valid outfit code!", "error", 6500)
        return false
    end

    local code = input[1]
    local newName = input[2]

    local nameExists = GetResourceKvpString(format("mp_ped_%s", newName))
    if nameExists then
        Config.Notify("vMenu", "You have an outfit saved with that name already!", "error", 6500)
        return false
    end

    local Valid = lib.callback.await("vMenu:Outfits:Request", false, code)
    Cooldown = true
    SetTimeout(5000, function()
        Cooldown = false
        lib.print.debug("Outfit load cooldown expired.")
    end)

    if not Valid then
        Config.Notify("vMenu", "The outfit code you entered is invalid!", "error", 6500)
        return false
    end

    local validData = json.decode(Valid)
    local charJson = exports[GetCurrentResourceName()]:GetMpCharacterData()
    if charJson == nil then
        Config.Notify("vMenu", "Failed to fetch character data!", "error", 6500)
        return false
    end

    local charData = json.decode(charJson)

    for k, v in pairs(validData.Clothes) do
        local comp = tonumber(k)
        if isValidComp(cache.ped, comp, v.Item, v.Texture) then
            SetPedComponentVariation(cache.ped, comp, v.Item, v.Texture, 0)
        else
            lib.print.warn(("Invalid clothing: comp %s drawable %s texture %s"):format(comp, v.Item, v.Texture))
        end
    end

    for k, v in pairs(validData.Props) do
        SetPedPropIndex(cache.ped, tonumber(k), v.Item, v.Texture, true)
    end

    print('SET CLOTHES')

    charData.DrawableVariations = charData.DrawableVariations or {}
    charData.DrawableVariations.clothes = charData.DrawableVariations.clothes or {}

    charData.PropVariations = charData.PropVariations or {}
    charData.PropVariations.props = charData.PropVariations.props or {}

    for k, v in pairs(validData.Clothes) do
        local key = tostring(k)

        charData.DrawableVariations.clothes[key] =
            charData.DrawableVariations.clothes[key] or {}

        charData.DrawableVariations.clothes[key].Key = v.Item
        charData.DrawableVariations.clothes[key].Value = v.Texture
    end

    for k, v in pairs(validData.Props) do
        local key = tostring(k)

        charData.PropVariations.props[key] =
            charData.PropVariations.props[key] or {}

        charData.PropVariations.props[key].Key = v.Item
        charData.PropVariations.props[key].Value = v.Texture
    end

    print('SET KVP TABLE')

    SetResourceKvp(format("mp_ped_%s", newName), json.encode(charData))

    print('SAVED')
    
    Config.Notify("vMenu", "Outfit has been successfully loaded!", "success", 6500)

    return true
end)

exports("loadOutfitFromCode", function(outfitCode)
    if not outfitCode then
        return lib.print.error("export: Tried to load outfit failed, no code provided.")
    end
    if type(outfitCode) ~= "number" then
        return lib.print.error("export: Tried to load outfit failed, invalid code provided. (not a number)")
    end

    local Valid = lib.callback.await("vMenu:Outfits:Request", false, outfitCode)
    if not Valid then
        lib.print.debug(format("export: Failed to load outfit with code: %s", outfitCode))
        return false
    end

    local validData = json.decode(Valid)

    for k, v in pairs(validData.Clothes) do
        local comp = tonumber(k)
        if isValidComp(cache.ped, comp, v.Item, v.Texture) then
            SetPedComponentVariation(cache.ped, comp, v.Item, v.Texture, 0)
        else
            lib.print.warn(("Invalid clothing: comp %s drawable %s texture %s"):format(comp, v.Item, v.Texture))
        end
    end

    for k, v in pairs(validData.Props) do
        SetPedPropIndex(PlayerPedId(), k, v.Item, v.Texture, true)
    end

    return true
end)

if Config.OutfitSharing.CommandEnabled then
    TriggerEvent("chat:addSuggestion", Config.OutfitSharing.CommandName, "Load an outfit from a code", {
        { name = "code", help = "The outfit code you were given." }
    })
    RegisterCommand(Config.OutfitSharing.CommandName, function(source, args)
        if #args ~= 1 then
            Config.Notify("vMenu", "Invalid Usage! \n\n /" .. Config.OutfitSharing.CommandName .. " <code>", "error", 6500)
            return
        end
        if not tonumber(args[1]) then
            Config.Notify("vMenu", "You must provide a valid outfit code!", "error", 6500)
            return
        end
        exports[GetCurrentResourceName()]:loadOutfitFromCode(tonumber(args[1]))
    end, false)
end

AddEventHandler("vMenu:Outfits:GenerateCode", function(name)
    name = string.sub(name, 8) -- this removes the prefix of mp_ped_ from the name used to store kvp data

    if GenerateCooldown then
        Config.Notify("vMenu", "You must wait before generating another outfit code!", "error", 6500)
        return
    end

    local Existing = GetResourceKvpString(format("mp_ped_%s", name))
    if not Existing then
        Config.Notify("vMenu", "You do not have a MP ped saved with this name!", "error", 6500)
        return
    end

    local Data = json.decode(Existing)

    local ToSave = {
        Clothes = {},
        Props = {}
    }
    for k, v in pairs(Data.DrawableVariations.clothes) do
        ToSave.Clothes[k] = { Item = v.Key, Texture = v.Value }
    end

    for k, v in pairs(Data.PropVariations.props) do
        ToSave.Props[k] = { Item = v.Key, Texture = v.Value }
    end

    local Generated, errorMessage = lib.callback.await("vMenu:Outfits:Generate", false, ToSave)
    GenerateCooldown = true
    SetTimeout(15000, function()
        GenerateCooldown = false
        lib.print.debug("Outfit load cooldown expired.")
    end)

    if not Generated then
        Config.Notify("vMenu", errorMessage or "Failed to generate outfit code!", "error", 6500)
        return
    end

    print(format("Outfit code created: %s", Generated))
    Config.Notify("vMenu", "Outfit code has been generated #" .. Generated, "success", 10000)
    lib.setClipboard(Generated)
end)