CreateThread(function()
    if Config.DisableAI then
        SetRoutingBucketPopulationEnabled(0, false)
        lib.print.info("Disabled AI from spawning.")
    end
end)

-- The ban handlers live in the C# script (vMenuServer/BanManager.cs) and are reached through exports,
-- never through events: on the C# runtime every event handler also accepts events sent by clients, so a
-- handler taking the acting player's id as an argument could be called with an admin's id by anyone.
-- Always pass the real network `source` as the first argument. The C# side checks that player's ACE
-- permissions before doing anything.
local vMenu = exports[GetCurrentResourceName()]

---@class TempBanPlayer
---@field source integer
---@field targetPlayer integer
---@field banDurationHours number
---@field banReason string
RegisterNetEvent("vMenu:TempBanPlayer", function(targetPlayer, banDurationHours, banReason)
    vMenu:TempBanPlayer(source, targetPlayer, banDurationHours, banReason)
end)

---@class PermBanPlayer
---@field source integer
---@field targetPlayer integer
---@field banReason string
RegisterNetEvent("vMenu:PermBanPlayer", function(targetPlayer, banReason)
    vMenu:PermBanPlayer(source, targetPlayer, banReason)
end)

-- A server-only event: AddEventHandler, so clients cannot trigger it themselves.
AddEventHandler("playerConnecting", function(_, _, deferrals)
    local playerId = source
    deferrals.defer()
    -- FiveM requires a tick between defer() and the next deferral call.
    Wait(0)
    deferrals.update("Checking ban status...")

    local ok, banMessage = pcall(function()
        return vMenu:CheckBan(playerId)
    end)

    if ok and type(banMessage) == "string" and banMessage ~= "" then
        deferrals.done(banMessage)
    else
        if not ok then
            lib.print.error(("Ban check failed for connecting player %s: %s"):format(playerId, banMessage))
        end
        deferrals.done()
    end
end)

---@class RequestPlayerUnban
---@field source integer
---@field uuid string
RegisterNetEvent("vMenu:RequestPlayerUnban", function(uuid)
    if type(uuid) ~= "string" then return end
    vMenu:UnbanPlayer(source, uuid)
end)

---@class RequestBanList
---@field source integer
RegisterNetEvent("vMenu:RequestBanList", function()
    vMenu:SendBanList(source)
end)

-- Refreshes Show Player Names
Citizen.CreateThread(function()
    local lastEncoded = nil
    local ticksSinceBroadcast = 0
    while true do
        local players = GetPlayers()
        local cache = {}

        for _, id in ipairs(players) do
            cache[id] = {
                -- Parse EXACTLY what you want to show as the name (incl. staff ranks etc.)
                name=string.format("%s [%d]", GetPlayerName(id) or "Unknown", id),
                -- https://docs.fivem.net/docs/game-references/hud-colors/
                colour=18
            }
        end

        -- Only broadcast when the name table actually changes (join/drop/name change),
        -- plus a periodic heartbeat (~every 10s) so newly-connected clients reliably
        -- receive the current names even if they missed a change-triggered broadcast.
        local encoded = json.encode(cache)
        ticksSinceBroadcast = ticksSinceBroadcast + 1
        if encoded ~= lastEncoded or ticksSinceBroadcast >= 10 then
            lastEncoded = encoded
            ticksSinceBroadcast = 0
            TriggerClientEvent('vMenu:SyncOverheadNames', -1, cache)
        end

        Citizen.Wait(1000)
    end
end)