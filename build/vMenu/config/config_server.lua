Config = {
    EnableServerList = GetConvarBool("vmenu_disable_server_info_convars", false) == false, -- If you want to show the framework version in your server list info convars.
    DisableAI = GetConvarBool("vmenu_disable_ai", false), -- This will disable NPC spawning in bucket 0 (if you don't touch buckets, you don't need to worry about this). This is useful for people who want a quick and easy way to disable AI.
}