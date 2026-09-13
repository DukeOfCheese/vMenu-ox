-- Hands the Lua-authored configuration (config/config_server.lua, config/locations.lua) to the
-- C# server script. C# owns everything else: ACE evaluation, the KVP-backed runtime teleport
-- locations, and building the per-player payload. Nothing here decides who gets what.

local function stringifyKeys(t)
    -- json.encode turns a table with numeric keys into an array, and drops sparse entries, which
    -- would mangle Config.Extras. Forcing string keys gives a stable object on the C# side.
    if type(t) ~= 'table' then return t end
    local out = {}
    for k, v in pairs(t) do
        out[tostring(k)] = type(v) == 'table' and stringifyKeys(v) or v
    end
    return out
end

local function pruneEmpty(value)
    -- An empty Lua table encodes as {} rather than [], which C# cannot read as a list and which
    -- would take the whole config down with it. Dropping empties instead leaves the key absent,
    -- which the C# side already treats as "none configured".
    if type(value) ~= 'table' then return value end
    local out, count = {}, 0
    for k, v in pairs(value) do
        local pruned = pruneEmpty(v)
        if pruned ~= nil then
            out[k] = pruned
            count = count + 1
        end
    end
    if count == 0 then return nil end
    return out
end

local function sendConfig()
    local cfg = Config or {}
    TriggerEvent('vMenu:Internal:LoadConfig', json.encode({
        addons    = pruneEmpty(cfg.Addons) or {},
        extras    = pruneEmpty(stringifyKeys(cfg.Extras)) or {},
        locations = pruneEmpty(cfg.Locations) or {},
    }))
end

-- The C# side asks for the config once it has initialised. Also sent unprompted on resource start
-- so the order the two runtimes come up in does not matter.
AddEventHandler('vMenu:Internal:RequestConfig', sendConfig)

AddEventHandler('onResourceStart', function(resource)
    if resource ~= GetCurrentResourceName() then return end
    sendConfig()
end)
