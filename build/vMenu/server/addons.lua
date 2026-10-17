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
    -- json.encode can't tell an empty object from an empty list and writes an empty Lua table as [],
    -- which C# can't read where it expects an object. Dropping empties instead leaves the key absent,
    -- which the C# side treats as "none configured".
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

local resourceName = GetCurrentResourceName()

local function sendConfig()
    local cfg = Config or {}
    -- Sections with nothing configured are left out rather than sent as {}: json.encode writes an empty
    -- Lua table as [], which the C# side can't read as an object. `version` keeps the root non-empty.
    local payload = json.encode({
        version        = 1,
        addons         = pruneEmpty(cfg.Addons),
        modelWhitelist = pruneEmpty(cfg.ModelWhitelist),
        extras         = pruneEmpty(stringifyKeys(cfg.Extras)),
        locations      = pruneEmpty(cfg.Locations),
    })
    -- An export rather than an event: C# event handlers also accept events sent by clients, and
    -- this payload decides which ACE each addon is gated behind.
    local ok, err = pcall(function()
        exports[resourceName]:LoadConfig(payload)
    end)
    if not ok then
        -- The C# script registers the export when it loads, which is before this file runs, so
        -- this only happens if the DLL failed to load. It will ask again via RequestConfig.
        lib.print.warn(('Could not hand the config to the C# script yet: %s'):format(err))
    end
end

-- The C# side asks for the config once it has initialised. Also sent unprompted on resource start
-- so the order the two runtimes come up in does not matter.
AddEventHandler('vMenu:Internal:RequestConfig', sendConfig)

AddEventHandler('onResourceStart', function(resource)
    if resource ~= GetCurrentResourceName() then return end
    sendConfig()
end)
