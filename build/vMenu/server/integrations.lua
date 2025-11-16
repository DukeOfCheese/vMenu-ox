CreateThread(function()
    if Config.DisableAI then
        SetRoutingBucketPopulationEnabled(0, false)
        lib.print.info("Disabled AI from spawning.")
    end
end)