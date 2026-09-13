Config = {
    EnableServerList = GetConvarBool("vmenu_disable_server_info_convars", false) == false, -- If you want to show the framework version in your server list info convars.
    DisableAI = GetConvarBool("vmenu_disable_ai", false), -- This will disable NPC spawning in bucket 0 (if you don't touch buckets, you don't need to worry about this). This is useful for people who want a quick and easy way to disable AI.

    -- Addon content. Every entry is gated by a FiveM ACE permission that is evaluated server-side
    -- before the item is ever sent to a client, so a player who is not permitted never learns the
    -- item exists. Permitted items are merged into the normal menus: an allowed weapon shows under
    -- Weapons > Rifles, an allowed vehicle under its vehicle class, an allowed ped under its category.
    --
    -- The ACE is auto-derived into the same namespace vMenu already uses for base-game items, so an
    -- addon is permissioned exactly like anything else in that menu. Set `permission` on an entry to
    -- override it. ACEs are hierarchical, so you can grant in bulk instead of listing every item:
    --   add_ace group.admin "vMenu.WeaponOptions"               allow  -- every weapon, addon or not
    --   add_ace group.cops  "vMenu.WeaponOptions.weapon_mk18md1" allow  -- just that one
    --   add_ace group.cops  "vMenu.VehicleSpawner.redeye"        allow
    --   add_ace group.cops  "vMenu.PlayerAppearance.wick2"       allow
    --
    -- NOTE: addon items are gated ONLY by their own derived ACE. The blanket "...All" permissions
    -- (vMenu.WeaponOptions.All, vMenu.VehicleSpawner.All) cover base-game content only -- otherwise
    -- the default permissions.cfg, which grants those to everyone, would hand every addon to every
    -- player and per-item control would be meaningless.
    --
    -- Spawn names are matched case-insensitively; write them however you like.
    Addons = {
        -- ACE: vMenu.VehicleSpawner.<spawn>
        vehicles = {
            { spawn = 'redeye', label = 'Red Eye' },
        },

        -- ACE: vMenu.WeaponOptions.<spawn>
        -- Components are part of the weapon they belong to and inherit its permission: if you can
        -- spawn the weapon you can fit its attachments. vMenu still asks the game whether the
        -- weapon actually accepts each component before showing it.
        weapons = {
            {
                spawn = 'weapon_mk18md1',
                label = 'MK18MD1',
                components = {
                    { spawn = 'COMPONENT_AT_SCOPE_MK18MD',  label = 'Scope' },
                    { spawn = 'COMPONENT_MK18MD_CLIP_01',   label = 'Clip' },
                    { spawn = 'COMPONENT_AT_MK18MD_SUPP',   label = 'Suppressor' },
                    { spawn = 'COMPONENT_AT_MK18MD_FLSH',   label = 'Flashlight' },
                },
            },
        },

        -- ACE: vMenu.PlayerAppearance.<spawn>
        -- category is one of: main, animal, male, female, other (defaults to other).
        peds = {
            { spawn = 'wick2', label = 'Wick', category = 'male' },
        },

        -- ACE: vMenu.VehicleOptions.<spawn>
        -- Applied from Vehicle Options > Engine Sound. The spawn name is the vehicle model whose
        -- engine sound you want to borrow.
        engineSounds = {
            { spawn = 'adder',             label = 'Adder' },
            { spawn = 'baller',            label = 'Baller' },
            { spawn = 'dodgehemihellcat',  label = 'Dodge Hemi Hellcat' },
        },

        eup = {
            -- NOTE: EUP slider gating only takes effect once vMenu/data/EupBaseCounts.cs is
            -- populated with this server's base-game drawable counts (capture via the /eupdump
            -- procedure in the plan). Until then it fails OPEN (full ranges shown to everyone).
            enabled    = true,                 -- false => no EUP slider gating at all
            permission = 'vMenu.PlayerAppearance.EUP', -- single ACE for the extended clothing ranges
        },
    },

    -- Named vehicle extras, shown in Vehicle Options > Vehicle Extras.
    -- Keyed by vehicle model, then by extra index. Not ACE gated: these are only labels for
    -- extras the player can already toggle, and the menu itself is permission gated.
    Extras = {
        policecharger = {
            [1] = '1: Push Bar',
            [2] = '2: Push Bar Wrap',
            [3] = '3: Light Bar',
            [4] = '4: Visor Lights',
            [5] = '5: Antennas',
            [9] = '9: Rear Deck Lights',
        },
        policecvpi = {
            [1]  = 'Push Bar',
            [2]  = 'Push Bar Wrap',
            [3]  = 'Light Bar',
            [4]  = 'Visor Lights',
            [5]  = 'Antennas',
            [7]  = 'Rear Deck Lights',
            [11] = 'Cage',
            [12] = 'Trunk Modem/Antennas',
        },
    },
}
