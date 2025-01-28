-- For exports, they must remain in this file. However, you can add the events into your other scripts like infiniteFuelToggled, licensePlateUpdated, and noclipToggled etc


---@class infiniteFuelToggled
---@field enabled boolean
AddEventHandler("vMenu:Integrations:InfiniteFuel", function(enabled)
    lib.print.debug("Infinite Fuel: " .. tostring(enabled))
end)

---@class licensePlateUpdated
---@field handle integer
---@field plate string
AddEventHandler("vMenu:Integrations:LicensePlateUpdated", function(handle, plate)
    lib.print.debug("License Plate Updated: " .. handle .. " - " .. plate)
    --[[
        Example Usage:
        if doesTextContainBlacklistedWord(plate) then
            SetVehicleNumberPlateText(handle, "PLATE")
            TriggerServerEvent("banplayer")
        end
    --]]
end)

---@class noclipToggled
---@field enabled boolean
AddEventHandler("vMenu:Integrations:NoClip", function(enabled)
    lib.print.debug("NoClip: " .. tostring(enabled))
end)

-- InteractionType "spawnvehicle" | "refillammo" | "spawnweapon" | "spawnloadout" | "noclip"
---@class canDoInteraction
---@field type string
---@return boolean Returns true if the player can do the interaction, false otherwise
exports("canDoInteraction", function(type)
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

    return true -- Always leave as true, and handle each interaction type as needed with an if statement.
end)
