CreateThread(function()
    if Config.DisableAI then
        SetRoutingBucketPopulationEnabled(0, false)
        lib.print.info("Disabled AI from spawning.")
    end
end)

-- WARNING: The internal vMenu events used here also use IsPlayerAceAllowed server sided to ensure the source player is not event
-- exploiting. If you write a custom event, ensure you also include those checks to prevent abuse.

---@class TempBanPlayer
---@field source integer
---@field targetPlayer integer
---@field banDurationHours number
---@field banReason string
RegisterNetEvent("vMenu:TempBanPlayer", function(targetPlayer, banDurationHours, banReason)
    TriggerEvent("vMenu:Internal:TempBanPlayer", source, targetPlayer, banDurationHours, banReason)
end)

---@class PermBanPlayer
---@field source integer
---@field targetPlayer integer
---@field banReason string
RegisterNetEvent("vMenu:PermBanPlayer", function(targetPlayer, banReason)
    TriggerEvent("vMenu:Internal:PermBanPlayer", source, targetPlayer, banReason)
end)

-- Store deferrals for each connecting player
local connectingDeferrals = {}

RegisterNetEvent("playerConnecting", function(playerName, setKickReason, deferrals)
    local playerId = source
    connectingDeferrals[playerId] = deferrals
    
    -- Trigger C# ban check
    TriggerEvent("vMenu:Internal:playerConnecting", playerId, playerName)
    
    -- Clean up deferrals after a timeout (in case player connects successfully)
    SetTimeout(30000, function()
        connectingDeferrals[playerId] = nil
    end)
end)

-- Handle ban rejection from C#
AddEventHandler("vMenu:Internal:RejectConnection", function(playerId, banMessage)
    local deferrals = connectingDeferrals[playerId]
    if deferrals then
        deferrals.done(banMessage)
        connectingDeferrals[playerId] = nil
    end
end)

---@class RequestPlayerUnban
---@field source integer
---@field uuid string
RegisterNetEvent("vMenu:RequestPlayerUnban", function(uuid)
    TriggerEvent("vMenu:Internal:RequestedPlayerUnban", source, uuid)
end)

---@class RequestBanList
---@field source integer
RegisterNetEvent("vMenu:RequestBanList", function()
    TriggerEvent("vMenu:Internal:RequestBanList", source)
end)

-- Refreshes Show Player Names
Citizen.CreateThread(function()
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

        TriggerClientEvent('vMenu:SyncOverheadNames', -1, cache)
        Citizen.Wait(1000)
    end
end)