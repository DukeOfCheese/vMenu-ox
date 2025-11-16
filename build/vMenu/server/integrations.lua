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

RegisterNetEvent("playerConnecting", function(playerName, setKickReason, deferrals)
    TriggerEvent("vMenu:Internal:playerConnecting", playerName, setKickReason, deferrals)
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