Config = {
    EnableServerList = GetConvarBool("vmenu_disable_server_info_convars", false) == false, -- If you want to show the framework version in your server list info convars.
    DisableAI = GetConvarBool("vmenu_disable_ai", false), -- This will disable NPC spawning in bucket 0 (if you don't touch buckets, you don't need to worry about this). This is useful for people who want a quick and easy way to disable AI.

    -- Runtime addon vehicles/weapons/EUP. Each item is gated by a FiveM ACE permission,
    -- evaluated server-side (see server/addons.lua). Add access in your server.cfg, e.g.:
    --   add_ace group.cops vMenu.Addons.vehicles.police3 allow
    --   add_ace group.cops vMenu.Addons.weapons.weapon_raypistol allow
    --   add_ace group.cops vMenu.Addons.eup allow
    Addons = {
        -- ACE auto-derived as vMenu.Addons.vehicles.<spawn> unless `permission` is set.
        vehicles = {
            { spawn = 'police3',   label = 'LSPD Cruiser' },
            { spawn = 'ambulance', label = 'Ambulance' },
            { spawn = 'adder',     label = 'VIP Adder', permission = 'vMenu.Addons.vip' },
        },
        -- ACE auto-derived as vMenu.Addons.weapons.<spawn> unless `permission` is set.
        weapons = {
            { spawn = 'weapon_raypistol',     label = 'Up-n-Atomizer' },
            { spawn = 'weapon_militaryrifle', label = 'Military Rifle' },
        },
        eup = {
            -- NOTE: EUP slider gating only takes effect once vMenu/data/EupBaseCounts.cs is
            -- populated with this server's base-game drawable counts (capture via the /eupdump
            -- procedure in the plan). Until then it fails OPEN (full ranges shown to everyone).
            enabled    = true,                 -- false => no EUP slider gating at all
            permission = 'vMenu.Addons.eup',   -- single group ACE for the extended clothing ranges
        },
    },
}