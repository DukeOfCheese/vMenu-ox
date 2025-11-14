CreateThread(function()
    lib.versionCheck("Gravxd/vMenu-ox")

    if Config.EnableServerList then
        local currentVersion = GetResourceMetadata(GetCurrentResourceName(), "version", 0)
        SetConvarServerInfo("Framework", ("vMenu-ox - %s"):format(currentVersion))
        lib.print.info("vMenu-ox loaded! Version: " .. currentVersion)
    end
end)