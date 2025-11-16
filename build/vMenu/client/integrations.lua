-- For exports, they must remain in this file. However, you can add the events into your other scripts like infiniteFuelToggled, licensePlateUpdated, and noclipToggled etc

---@class logAction
---@field action string
---@field data table
AddEventHandler("vMenu:Integrations:Action", function(action, data)
    if action == "infinitefuel" then
        ---@cast data {enabled: boolean}
        lib.print.debug("Infinite Fuel: " .. tostring(data.enabled))
    elseif action == "licenseplate" then
        ---@cast data {handle: integer, plate: string}
        lib.print.debug("License Plate Updated: " .. data.handle .. " - " .. data.plate)
        --[[
            Example Usage:
            if doesTextContainBlacklistedWord(data.plate) then
                SetVehicleNumberPlateText(data.handle, "PLATE")
                TriggerServerEvent("banplayer")
            end
        --]]
    elseif action == "noclip" then
        ---@cast data {enabled: boolean}
        lib.print.debug("NoClip: " .. tostring(data.enabled))
    elseif action == "playernames" then
        ---@cast data {enabled: boolean}
        lib.print.debug("Player Names: " .. tostring(data.enabled))
    elseif action == "playerblips" then
        ---@cast data {enabled: boolean}
        lib.print.debug("Player Blips: " .. tostring(data.enabled))
    end
end)

--#region Example Interaction Checks
--[[
        Example Usage:
        -- Prevent weapon spawning while in vehicles or dead
        if type == "spawnweapon" then

            if cache.vehicle then
                lib.notify({
                    title = "Cannot Spawn Weapon",
                    description = "Exit vehicle first",
                    type = "error"
                })
                return false
            end

            if LocalPlayer.state.isDead then
                lib.notify({
                    title = "Cannot Spawn Weapon",
                    description = "You are dead",
                    type = "error"
                })
                return false
            end

            -- Example integration check
            local isInRestrictedArea = exports.zones:isInRestrictedArea()

            if isInRestrictedArea then
                lib.notify({
                    title = "Restricted Area",
                    description = "Cannot spawn weapons here",
                    type = "error"
                })
                return false
            end
        end

        -- Vehicle spawn restrictions based on player state and location
        if type == "spawnvehicle" then

            if IsEntityInWater(cache.ped) then
                lib.notify({
                    title = "Cannot Spawn Vehicle",
                    description = "Get out of water first",
                    type = "error"
                })
                return false
            end

            -- Example integration check
            local isAtSpawnPoint = exports.locations:isAtVehicleSpawn()

            if not isAtSpawnPoint then
                lib.notify({
                    title = "Invalid Location",
                    description = "Find a vehicle spawn point",
                    type = "error"
                })
                return false
            end
        end

        -- Loadout restrictions based on player state and permissions
        if type == "spawnloadout" then

            if IsPedRagdoll(cache.ped) then
                lib.notify({
                    title = "Cannot Spawn Loadout",
                    description = "Cannot equip while ragdolled",
                    type = "error"
                })
                return false
            end

            -- Example integration check
            local hasPermission = exports.permissions:hasLoadoutAccess()

            if not hasPermission then
                lib.notify({
                    title = "Access Denied",
                    description = "Unauthorized for loadouts",
                    type = "error"
                })
                return false
            end
        end

        -- Ammo refill restrictions based on combat and events
        if type == "refillammo" then
            if IsPedInMeleeCombat(cache.ped) then
                lib.notify({
                    title = "Cannot Refill Ammo",
                    description = "Not while in combat",
                    type = "error"
                })
                return false
            end

            -- Example integration check
            local eventActive = exports.events:isEventRunning()

            if eventActive then
                lib.notify({
                    title = "Event Active",
                    description = "Cannot refill during events",
                    type = "error"
                })
                return false
            end
        end
    --]]
--#endregion

---@class canDoInteraction
---@field action string
---@return boolean Returns true if the player can do the interaction, false otherwise
exports("canDoInteraction", function(action)
    if action == "spawnvehicle" then

    elseif action == "refillammo" then

    elseif action == "spawnweapon" then

    elseif action == "spawnloadout" then

    elseif action == "noclip" then

    elseif action == "nightvision" then

    elseif action == "thermalvision" then

    elseif action == "playernames" then

    elseif action == "playerblips" then

    end
    return true -- Always leave as true, and handle each interaction type as needed with an if statement.
end)

---@class customNotify
---@field description string
---@field ntype string
AddEventHandler("vMenu:CustomNotify", function(description, ntype)
    Config.Notify("vMenu", description, ntype, 6500)
end)

