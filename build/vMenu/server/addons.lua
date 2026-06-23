-- Sends each player the addon vehicles/weapons they are permitted to use, evaluated against
-- FiveM ACE permissions. Definitions live in config_server.lua (Config.Addons).
-- See docs/superpowers/specs/2026-06-23-runtime-addons-permissions-design.md

local function deriveAce(kind, entry)
    if type(entry.permission) == 'string' and entry.permission ~= '' then
        return entry.permission
    end
    return ('vMenu.Addons.%s.%s'):format(kind, entry.spawn:lower())
end

local function isValid(entry, isWeapon)
    if type(entry) ~= 'table' then return false end
    if type(entry.spawn) ~= 'string' or #entry.spawn == 0 or #entry.spawn > 64 then return false end
    if isWeapon and entry.spawn:sub(1, 7) ~= 'weapon_' then return false end
    return true
end

local function collectAllowed(src, kind, list)
    local out = {}
    if type(list) ~= 'table' then return out end
    local isWeapon = (kind == 'weapons')
    for idx, entry in ipairs(list) do
        if isValid(entry, isWeapon) then
            if IsPlayerAceAllowed(src, deriveAce(kind, entry)) then
                out[#out + 1] = { spawn = entry.spawn, label = entry.label or entry.spawn }
            end
        else
            print(('^3[vMenu] [Addons] Skipping malformed %s entry #%d (spawn=%s) in Config.Addons^7'):format(kind, idx, tostring(type(entry) == 'table' and entry.spawn or entry)))
        end
    end
    return out
end

RegisterNetEvent('vMenu:RequestAddons', function()
    local src = source
    local cfg = (Config and Config.Addons) or {}

    local payload = {
        vehicles = collectAllowed(src, 'vehicles', cfg.vehicles),
        weapons  = collectAllowed(src, 'weapons',  cfg.weapons),
        eup      = { allowed = true },
    }

    local eup = cfg.eup
    if type(eup) == 'table' and eup.enabled then
        payload.eup.allowed = IsPlayerAceAllowed(src, eup.permission or 'vMenu.Addons.eup')
    end

    TriggerClientEvent('vMenu:SetAddons', src, json.encode(payload))
end)
