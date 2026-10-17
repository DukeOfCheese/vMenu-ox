-- How the C# side of vMenu calls into this resource's Lua (dialogs, clipboard, canDoInteraction, share codes).
--
-- C# used to call these as exports, but calling an export from C# goes through the C# `dynamic` binder,
-- and on FiveM's runtime that binder fails as soon as MenuAPI 4 is loaded (it cannot read that assembly's
-- attributes: "TypeLoadException: Could not resolve type with token 0100000e"). So C# triggers the local
-- event below with a callback instead, and gets the function's first return value back (nil on failure).
--
-- Every function is still a normal export as well, so other resources can keep calling them.

VMenuBridge = {}

local resourceName = GetCurrentResourceName()
local targets = {}

---Registers `fn` as an export of this resource and as something the C# side can call.
---@param name string
---@param fn function
function VMenuBridge.export(name, fn)
    targets[name] = fn
    exports(name, fn)
end

-- Local event only (no RegisterNetEvent): clients and the server cannot reach it, only this client.
AddEventHandler('vMenu:Bridge:Call', function(name, cb, ...)
    local fn = targets[name]
    local ok, result = pcall(function(...)
        if fn then
            return fn(...)
        end
        -- Not registered here, e.g. a customised client/integrations.lua from an older version that still
        -- uses plain exports(); call it as an export instead.
        return exports[resourceName][name](exports[resourceName], ...)
    end, ...)

    if not ok then
        lib.print.error(('vMenu bridge call "%s" failed: %s'):format(tostring(name), tostring(result)))
        result = nil
    end

    if cb then
        cb(result)
    end
end)
