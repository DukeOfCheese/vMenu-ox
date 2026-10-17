-- Manifest data
fx_version 'cerulean'
games { 'gta5' }

description 'vMenu Fork - github.com/DukeOfCheese/vMenu-ox'
version '3.0.0'
author 'Tom Grobbe (vMenu), Gravxd & DukeOfCheese (vMenu-ox)'
ui_page 'storage.html'

lua54 "yes"
shared_scripts {
    "@ox_lib/init.lua"
}

-- Adds additional logging, useful when debugging issues.
client_debug_mode 'false'
server_debug_mode 'false'

-- Adds extra commands for testing and development
experimental_features_enabled '0'

-- Files & scripts
files {
    'Newtonsoft.Json.dll',
    'MenuAPI.dll',
    'storage.html'
}

client_scripts {
    'vMenuClient.net.dll',
    'config/config_client.lua',
    -- Shared helpers first: the files in client/ use them as they load.
    'client/lib/*.lua',
    'client/*.lua'
}
server_scripts {
    'vMenuServer.net.dll',
    -- config_server.lua defines Config; locations.lua extends it, so order matters here.
    'config/config_server.lua',
    'config/locations.lua',
    -- Shared helpers first: the files in server/ use them as they load.
    'server/lib/*.lua',
    'server/*.lua'
}

dependencies {
    'ox_lib'
}
