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

-- Show Player Names. Each client keeps its own copy of this table: it asks for the whole table once
-- its menu is set up (vMenu:RequestOverheadNames) and after that only receives what changed, where an
-- entry of `false` means that player left. Keys are always strings (tostring(id)) so the table is
-- sent as a map, never as a list.
local overheadNames = {}
local overheadNamesRequested = {}

local function buildOverheadName(id)
    return {
        -- Parse EXACTLY what you want to show as the name (incl. staff ranks etc.)
        name = string.format("%s [%d]", GetPlayerName(id) or "Unknown", tonumber(id)),
        -- https://docs.fivem.net/docs/game-references/hud-colors/
        colour = 18
    }
end

---@param target integer server id, or -1 for everyone
---@param names table never empty: an empty table would arrive as a list
---@param isDelta boolean false replaces the client's table, true merges into it
local function sendOverheadNames(target, names, isDelta)
    TriggerClientEvent('vMenu:SyncOverheadNames', target, names, isDelta)
end

for _, id in ipairs(GetPlayers()) do
    overheadNames[tostring(id)] = buildOverheadName(id)
end

-- Server-only events: AddEventHandler, so clients cannot fake a join or a drop.
AddEventHandler('playerJoining', function()
    local key = tostring(source)
    local entry = buildOverheadName(key)
    overheadNames[key] = entry
    sendOverheadNames(-1, { [key] = entry }, true)
end)

AddEventHandler('playerDropped', function()
    local playerId = source
    overheadNamesRequested[playerId] = nil

    local key = tostring(playerId)
    if overheadNames[key] ~= nil then
        overheadNames[key] = nil
        sendOverheadNames(-1, { [key] = false }, true)
    end
end)

-- playerJoining fires before the client's scripts are listening, so the client asks for the full
-- table itself once it is ready.
RegisterNetEvent('vMenu:RequestOverheadNames', function()
    local playerId = source
    local now = GetGameTimer()
    -- The client asks once; anything more often than every few seconds is ignored.
    local last = overheadNamesRequested[playerId]
    if last and now - last < 5000 then
        return
    end
    overheadNamesRequested[playerId] = now

    if next(overheadNames) ~= nil then
        sendOverheadNames(playerId, overheadNames, false)
    end
end)

-- Names can change while connected, and this also catches a missed join or drop: compare the names
-- every 5 seconds and only broadcast what changed.
CreateThread(function()
    while true do
        Wait(5000)

        local changes = {}
        local changed = false
        local online = {}

        for _, id in ipairs(GetPlayers()) do
            local key = tostring(id)
            online[key] = true
            local entry = buildOverheadName(key)
            local current = overheadNames[key]
            if current == nil or current.name ~= entry.name or current.colour ~= entry.colour then
                overheadNames[key] = entry
                changes[key] = entry
                changed = true
            end
        end

        for key in pairs(overheadNames) do
            if not online[key] then
                overheadNames[key] = nil
                changes[key] = false
                changed = true
            end
        end

        if changed then
            sendOverheadNames(-1, changes, true)
        end
    end
end)