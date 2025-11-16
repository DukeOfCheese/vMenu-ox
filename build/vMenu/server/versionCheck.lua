CreateThread(function()
    lib.versionCheck("DukeOfCheese/vMenu-ox")

    if not Config.EnableServerList then return end

    local currentVersion = GetResourceMetadata(GetCurrentResourceName(), "version", 0)
    SetConvarServerInfo("Framework", ("vMenu-ox - %s"):format(currentVersion))
    lib.print.info("vMenu-ox loaded! Version: " .. currentVersion)
end)