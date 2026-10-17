-- Shared plumbing for the vehicle, outfit and loadout share codes (client/*Codes.lua).
-- The server owns the database and the real limits (server/lib/codes.lua). Everything a code contains is
-- checked and rebuilt by the C# side (SharedCodes.cs, MpPedCustomization.cs) before it is saved or applied,
-- so these files only move data between the dialogs, the server and C#.

ShareCodes = {}

local resource = GetCurrentResourceName()
local coolingDown = {}

function ShareCodes.notify(message, kind, duration)
    Config.Notify('vMenu', message, kind or 'error', duration or 6500)
end

---Whether the server has this kind of code turned on (the vmenu_*_codes convars are replicated).
function ShareCodes.enabled(convar)
    return GetConvar(convar, 'false') == 'true'
end

---Claims a client-side cooldown before any request is sent. Returns false while `key` is cooling down.
function ShareCodes.claim(key, ms)
    if coolingDown[key] then
        return false
    end
    coolingDown[key] = true
    SetTimeout(ms, function()
        coolingDown[key] = nil
    end)
    return true
end

---A share code as a positive integer, or nil.
function ShareCodes.toId(value)
    local number = tonumber(value)
    number = number and math.tointeger(number)
    if number and number > 0 then
        return number
    end
    return nil
end

---Asks the player for a code and a name to save it under. Returns id, name or nil.
function ShareCodes.prompt(label)
    local input = lib.inputDialog(('Enter %s Code'):format(label), {
        { type = 'number', label = ('%s Code'):format(label), description = 'The sharing code you were given.', icon = 'hashtag', required = true },
        { type = 'input', label = 'Save Name', description = 'What should it be saved as?', icon = 'tag', max = 30, min = 3, required = true },
    })
    if not input then
        return nil
    end
    local id = ShareCodes.toId(input[1])
    local name = type(input[2]) == 'string' and input[2]:match('^%s*(.-)%s*$') or ''
    if not id or #name < 3 then
        return nil
    end
    return id, name
end

---Fetches a code's data. Returns the JSON string, or nil and the message to show.
function ShareCodes.fetch(kind, id)
    local ok, data, reason = pcall(lib.callback.await, ('vMenu:%s:Request'):format(kind), false, id)
    if not ok then
        return nil, 'The server did not respond.'
    end
    if type(data) ~= 'string' then
        return nil, reason or 'That code is invalid!'
    end
    return data
end

---Stores data as a new code. Returns the code, or nil and the message to show.
function ShareCodes.create(kind, encoded)
    local ok, id, reason = pcall(lib.callback.await, ('vMenu:%s:Generate'):format(kind), false, encoded)
    if not ok then
        return nil, 'The server did not respond.'
    end
    if not id then
        return nil, reason
    end
    return id
end

---Calls one of the C# share-code exports. They return an empty string on success, or the message to show.
function ShareCodes.apply(exportName, ...)
    local ok, result = pcall(function(...)
        return exports[resource][exportName](exports[resource], ...)
    end, ...)
    if not ok then
        lib.print.error(('%s failed: %s'):format(exportName, result))
        return false, 'Something went wrong while loading that code.'
    end
    if result ~= '' then
        return false, result
    end
    return true
end

---Shows a freshly generated code and copies it to the clipboard.
function ShareCodes.announce(label, id)
    lib.setClipboard(tostring(id))
    ShareCodes.notify(('%s code #%s has been generated and copied to your clipboard.'):format(label, id), 'success', 10000)
end
