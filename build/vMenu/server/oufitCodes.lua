local userRequestCooldowns = {}
lib.callback.register(
    "vMenu:Outfits:Request",
    function(source, id)

        if userRequestCooldowns[source] then
            -- shouldnt be possible with the client side cooldowns but this is to protect against event spam
            lib.print.error(string.format("%s [%s] tried to request outfit too quickly.", GetPlayerName(source), source))
            return false, "You are requesting outfits too quickly. Please wait a moment."
        end

        if not id then
            lib.print.error(string.format("%s [%s] tried to load outfit with no id?", GetPlayerName(source), source))
            return false
        end

        if type(id) ~= "number" then
            lib.print.error(string.format("%s [%s] tried to load outfit with invalid id?", GetPlayerName(source), source))
            return false
        end

        userRequestCooldowns[source] = true

        local response = exports.oxmysql:query_async('SELECT `data` FROM `vmenu_outfits` WHERE `id` = ?', {
            id
        })

        SetTimeout(3500, function()
            userRequestCooldowns[source] = nil
        end)

        if response then
            return response[1] and response[1].data or false
        end

        return false, "Failed to load outfit from database."
    end
)

local userGenerateCooldowns = {}
lib.callback.register(
    "vMenu:Outfits:Generate",
    function(source, data)

        if userGenerateCooldowns[source] then
            -- shouldnt be possible with the client side cooldowns but this is to protect against event spam
            lib.print.error(string.format("%s [%s] tried to generate outfit too quickly.", GetPlayerName(source), source))
            return false, "You are generating outfits too quickly. Please wait a moment."
        end

        if not data then
            lib.print.error(string.format("%s [%s] tried to save outfit with no data?", GetPlayerName(source), source))
            return false
        end

        if type(data) ~= "table" then
            lib.print.error(string.format("%s [%s] tried to save outfit with invalid data?", GetPlayerName(source), source))
            return false
        end

        local discord = GetPlayerIdentifierByType(source, "discord") and
            GetPlayerIdentifierByType(source, "discord"):gsub("discord:", "") or false
        if not discord then
            return false, "You need to have Discord linked to generate outfit codes."
        end

        userGenerateCooldowns[source] = true

        local id = exports.oxmysql:insert_async('INSERT INTO `vmenu_outfits` (`discord_id`, `data`) VALUES (?, ?)', {
            discord, json.encode(data)
        })

        SetTimeout(12500, function()
            userGenerateCooldowns[source] = nil
        end)

        return id or false
    end
)

CreateThread(function()
    if not GetConvarBool("vmenu_outfit_codes", false) then
        return
    end

    if GetResourceState("oxmysql") ~= "started" then
        for i = 1, 10 do
            lib.print.error("Outfit Code System is enabled but oxmysql is not started..")
        end
        return
    end

    lib.print.info("Outfit Code System is enabled.")
end)
