-- Manifest data
fx_version 'bodacious'
games { 'gta5' }

description 'vMenu Fork - github.com/Gravxd/vMenu-ox'
version '1.2.0'
author 'Tom Grobbe (vMenu), Gravxd (vMenu-ox)'
ui_page 'storage.html'

lua54 "yes"
shared_scripts {
    "@ox_lib/init.lua",
    "config/config_shared.lua",
}

-- Adds additional logging, useful when debugging issues.
client_debug_mode 'false'
server_debug_mode 'false'

-- Leave this set to '0' to prevent compatibility issues
-- and to keep the save files your users.
experimental_features_enabled '0'

-- Files & scripts
files {
    'Newtonsoft.Json.dll',
    'MenuAPI.dll',
    'config/locations.json',
    'config/addons.json',
    'storage.html'
}

client_scripts {
    'vMenuClient.net.dll',
    'config/config_client.lua',
    'client/*.lua'
}
server_scripts {
    'vMenuServer.net.dll',
    '@oxmysql/lib/MySQL.lua', -- Only uncomment if you are using outfit system
    'config/config_server.lua',
    'server/*.lua'
}
