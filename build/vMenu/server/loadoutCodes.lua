local userRequestCooldowns = {}
lib.callback.register(
    'vMenu:Loadouts:Request',
    function(source, id)

        if userRequestCooldowns[source] then
            lib.print.error(string.format('%s [%s] tried to request loadouts too quickly.', GetPlayerName(source), source))
            return false, 'You are requesting loadouts too quickly. Please wait a moment.'
        end

        if not id then
            lib.print.error(string.format('%s [%s] tried to load loadout with no id?', GetPlayerName(source), source))
            return false
        end

        if type(id) ~= 'number' then
            lib.print.error(string.format('%s [%s] tried to load loadout with invalid id?', GetPlayerName(source), source))
            return false
        end

        userRequestCooldowns[source] = true

        local response = exports.oxmysql:query_async('SELECT `data` FROM `vmenu_loadouts` WHERE `id` = ?', {
            id
        })

        SetTimeout(3500, function()
            userRequestCooldowns[source] = nil
        end)

        if response then
            return response[1] and response[1].data or false
        end

        return false, 'Failed to load loadout from database.'
    end
)

local userGenerateCooldowns = {}
lib.callback.register(
    'vMenu:Loadouts:Generate',
    function(source, data)

        if userGenerateCooldowns[source] then
            lib.print.error(string.format('%s [%s] tried to generate loadouts too quickly.', GetPlayerName(source), source))
            return false
        end

        if type(data) ~= "table" then
            lib.print.error(string.format('%s [%s] tried to save loadout with invalid data?', GetPlayerName(source), source))
            return false
        end

        local discord = GetPlayerIdentifierByType(source, "discord") and
            GetPlayerIdentifierByType(source, "discord"):gsub("discord:", "") or false
        if not discord then
            return false, 'You need to have Discord linked to generate loadout codes.'
        end

        userGenerateCooldowns[source] = true

        local id = exports.oxmysql:insert_async('INSERT INTO `vmenu_loadouts` (`discord_id`, `data`) VALUES (?, ?)', {
            discord, json.encode(data)
        })

        SetTimeout(12500, function()
            userGenerateCooldowns[source] = nil
        end)

        return id or false
    end
)

CreateThread(function()
    if not GetConvarBool('vmenu_loadoutcodes', false) then
        return
    end

    if GetResourceState('oxmysql') ~= 'started' then
        for i = 1, 10 do
            lib.print.error('Loadout Code System is enabled but oxmysql is not started..')
        end
        return
    end

    lib.print.info('Loadout Code System is enabled.')
end)