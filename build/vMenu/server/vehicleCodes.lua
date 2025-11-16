local userRequestCooldowns = {}
lib.callback.register(
    "vMenu:Vehicles:Request",
    function(source, id)

        if userRequestCooldowns[source] then
            -- shouldnt be possible with the client side cooldowns but this is to protect against event spam
            lib.print.error(string.format("%s [%s] tried to request vehicle too quickly.", GetPlayerName(source), source))
            return false, "You are requesting vehicles too quickly. Please wait a moment."
        end

        if not id then
            lib.print.error(string.format("%s [%s] tried to load vehicle with no id?", GetPlayerName(source), source))
            return false
        end

        if type(id) ~= "number" then
            lib.print.error(string.format("%s [%s] tried to load vehicle with invalid id?", GetPlayerName(source), source))
            return false
        end

        userRequestCooldowns[source] = true

        local response = exports.oxmysql:query_async('SELECT `data` FROM `vmenu_vehicles` WHERE `id` = ?', {
            id
        })

        SetTimeout(3500, function()
            userRequestCooldowns[source] = nil
        end)

        if response then
            return response[1] and response[1].data or false
        end

        return false, "Failed to load vehicle from database."
    end
)

local userGenerateCooldowns = {}

lib.callback.register(
    "vMenu:Vehicles:Generate",
    function(source, data)

        if userGenerateCooldowns[source] then
            -- shouldnt be possible with the client side cooldowns but this is to protect against event spam
            lib.print.error(string.format("%s [%s] tried to generate vehicle too quickly.", GetPlayerName(source), source))
            return false, "You are generating vehicles too quickly. Please wait a moment."
        end

        if not data then
            lib.print.error(string.format("%s [%s] tried to save vehicle with no data?", GetPlayerName(source), source))
            return false, "No data provided."
        end

        if type(data) ~= "table" then
            lib.print.error(string.format("%s [%s] tried to save vehicle with invalid data?", GetPlayerName(source), source))
            return false, "Invalid data provided."
        end

        local discord = GetPlayerIdentifierByType(source, "discord") and
            GetPlayerIdentifierByType(source, "discord"):gsub("discord:", "") or false
        if not discord then
            return false, "You need to have Discord linked to generate vehicle codes."
        end

        userGenerateCooldowns[source] = true

        local id = exports.oxmysql:insert_async('INSERT INTO `vmenu_vehicles` (`discord_id`, `data`) VALUES (?, ?)', {
            discord, json.encode(data)
        })

        SetTimeout(12500, function()
            userGenerateCooldowns[source] = nil
        end)

        return id or false
    end
)

CreateThread(function()
    if not GetConvarBool("vmenu_vehiclecodes", false) then
        return
    end

    if GetResourceState("oxmysql") ~= "started" then
        for i = 1, 10 do
            lib.print.error("Vehicle Code System is enabled but oxmysql is not started..")
        end
        return
    end

    lib.print.info("Vehicle Code System is enabled.")
end)
