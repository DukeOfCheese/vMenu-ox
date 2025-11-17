Config = {

    OutfitSharing = {
        CommandName = 'loadoutfitfromcode',
        CommandEnabled = false, -- Enable the /loadoutfitfromcode command
    },

    LoadoutSharing = {
        CommandName = 'loadloadoutfromcode',
        CommandEnabled = false, -- Enable the /loadloadoutfromcode command
    },

    Notify = function(title, msg, ntype, time)
        msg = msg:gsub('<C>', ''):gsub('</C>', '')
        -- these are just different examples i did for ox_lib, you can change these of course how you like
        if ntype == "error" then
            lib.notify({
                id = 'error_' .. string.sub(msg, 1, 10),
                title = title,
                description = msg,
                position = 'center-right',
                style = {
                    backgroundColor = '#141517',
                    color = '#C1C2C5',
                    ['.description'] = {
                      color = '#909296'
                    }
                },
                icon = 'ban',
                iconColor = '#C53030'
            })
        elseif ntype == "alert" then
            lib.notify({
                id = 'alert_' .. string.sub(msg, 1, 10),
                title = title,
                description = msg,
                position = 'center-right',
                style = {
                    backgroundColor = "#ff963b",
                    color = "#000000",
                    [".description"] = {
                        color = "#000000"
                    }
                },
                icon = "fa-solid fa-triangle-exclamation",
                iconColor = "#C53030"
            })
        else
            lib.notify({
                id = 'default_' .. string.sub(msg, 1, 10),
                title = title,
                description = msg,
                type = ntype,
                duration = time,
                position = "center-right"
            })
        end
    end,

}
