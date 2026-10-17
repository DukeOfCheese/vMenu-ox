-- Shared plumbing for the vehicle, outfit and loadout share codes (server/*Codes.lua).
-- Everything security related lives here so the three kinds cannot drift apart again:
--   * the callbacks only exist while the kind's convar (vmenu_*_codes) is on,
--   * cooldowns are claimed before any work is done, generation cooldowns are tied to the Discord id
--     (so reconnecting does not reset them) and there is a daily cap per Discord id,
--   * the stored data is size-capped and must match the shape the kind expects.
-- Codes are looked up by id and are public by design: anyone who has the number can load it.

ShareCodes = {}

local MAX_BYTES = 65536
local DAILY_LIMIT = 25
local REQUEST_COOLDOWN_MS = 3500
local GENERATE_COOLDOWN_MS = 12500

local lastCall = {}

---Claims a cooldown slot. Returns false if the key was used less than `interval` ms ago.
local function claim(key, interval)
    local now = GetGameTimer()
    local last = lastCall[key]
    if last and now - last < interval then
        return false
    end
    lastCall[key] = now
    return true
end

local function discordId(source)
    local identifier = GetPlayerIdentifierByType(source, 'discord')
    if not identifier then return nil end
    return (identifier:gsub('^discord:', ''))
end

local function playerLabel(source)
    return ('%s [%s]'):format(GetPlayerName(source) or 'unknown', source)
end

AddEventHandler('playerDropped', function()
    local prefix = ('req:%s:'):format(source)
    for key in pairs(lastCall) do
        if key:sub(1, #prefix) == prefix then
            lastCall[key] = nil
        end
    end
end)

--#region shape checks
-- Small helpers for the per-kind validators. They only check what the client actually reads; the C#
-- side rebuilds the data from its own lists before anything is applied.

function ShareCodes.isInteger(value, min, max)
    if type(value) ~= 'number' or math.tointeger(value) == nil then
        return false
    end
    return (min == nil or value >= min) and (max == nil or value <= max)
end

function ShareCodes.isNumber(value)
    return type(value) == 'number' and value == value and value ~= math.huge and value ~= -math.huge
end

function ShareCodes.isString(value, maxLength)
    return type(value) == 'string' and #value <= maxLength
end

---Checks every key/value pair of `t` with `check`. False if `t` isn't a table, has more than `maxEntries`
---entries, or any pair fails.
function ShareCodes.eachEntry(t, maxEntries, check)
    if type(t) ~= 'table' then return false end
    local count = 0
    for k, v in pairs(t) do
        count = count + 1
        if count > maxEntries or not check(k, v) then
            return false
        end
    end
    return true
end
--#endregion

---@class ShareCodeKind
---@field name string        Used in the callback names: vMenu:<name>:Request / vMenu:<name>:Generate
---@field label string       Shown to players, e.g. "loadout"
---@field table string       Database table
---@field convar string      vmenu_*_codes convar that turns the feature on
---@field validate fun(data: any): boolean

---Registers the request and generate callbacks for one kind of share code.
---@param kind ShareCodeKind
function ShareCodes.register(kind)
    if not GetConvarBool(kind.convar, false) then
        return
    end

    if GetResourceState('oxmysql') == 'missing' then
        lib.print.error(('%s codes are enabled (%s) but oxmysql is not installed.'):format(kind.label, kind.convar))
        return
    end

    local selectQuery = ('SELECT `data` FROM `%s` WHERE `id` = ?'):format(kind.table)
    local countQuery = ('SELECT COUNT(*) FROM `%s` WHERE `discord_id` = ? AND `created` > (NOW() - INTERVAL 1 DAY)'):format(kind.table)
    local insertQuery = ('INSERT INTO `%s` (`discord_id`, `data`) VALUES (?, ?)'):format(kind.table)

    lib.callback.register(('vMenu:%s:Request'):format(kind.name), function(source, id)
        if not claim(('req:%s:%s'):format(source, kind.name), REQUEST_COOLDOWN_MS) then
            return false, ('You are loading %s codes too quickly. Please wait a moment.'):format(kind.label)
        end

        id = type(id) == 'number' and math.tointeger(id) or nil
        if not id or id < 1 then
            return false, ('That is not a valid %s code.'):format(kind.label)
        end

        local ok, rows = pcall(function() return exports.oxmysql:query_async(selectQuery, { id }) end)
        if not ok then
            lib.print.error(('Loading %s code %s failed: %s'):format(kind.label, id, rows))
            return false, 'The server could not load that code right now.'
        end

        local data = rows and rows[1] and rows[1].data
        if type(data) ~= 'string' then
            return false, ('No %s exists with that code.'):format(kind.label)
        end
        return data
    end)

    lib.callback.register(('vMenu:%s:Generate'):format(kind.name), function(source, encoded)
        local discord = discordId(source)
        if not discord then
            return false, ('You need to have Discord linked to generate %s codes.'):format(kind.label)
        end

        -- Claimed before any other work, and per Discord id so reconnecting does not reset it.
        if not claim(('gen:%s:%s'):format(discord, kind.name), GENERATE_COOLDOWN_MS) then
            return false, ('You are generating %s codes too quickly. Please wait a moment.'):format(kind.label)
        end

        if type(encoded) ~= 'string' or #encoded == 0 or #encoded > MAX_BYTES then
            lib.print.warn(('%s sent an empty or oversized %s code (%s bytes).'):format(playerLabel(source), kind.label, type(encoded) == 'string' and #encoded or 0))
            return false, ('Your %s is too large to share.'):format(kind.label)
        end

        local decodedOk, decoded = pcall(json.decode, encoded)
        if not decodedOk or not kind.validate(decoded) then
            lib.print.warn(('%s sent %s code data that does not look like a %s.'):format(playerLabel(source), kind.label, kind.label))
            return false, ('That %s cannot be shared.'):format(kind.label)
        end

        local countOk, count = pcall(function() return exports.oxmysql:scalar_async(countQuery, { discord }) end)
        if not countOk then
            lib.print.error(('Counting %s codes failed: %s'):format(kind.label, count))
            return false, 'The server could not create a code right now.'
        end
        if (tonumber(count) or 0) >= DAILY_LIMIT then
            return false, ('You can create at most %d %s codes a day.'):format(DAILY_LIMIT, kind.label)
        end

        local insertOk, id = pcall(function() return exports.oxmysql:insert_async(insertQuery, { discord, encoded }) end)
        if not insertOk or not id then
            lib.print.error(('Saving a %s code failed: %s'):format(kind.label, id))
            return false, 'The server could not create a code right now.'
        end
        return id
    end)

    lib.print.info(('Share codes enabled: %s.'):format(kind.label))
end
