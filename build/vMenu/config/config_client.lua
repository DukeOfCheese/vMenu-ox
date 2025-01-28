Config = {
    Notify = function(title, msg, ntype, time)
        lib.notify({
            title = title,
            description = msg,
            type = ntype,
            duration = time,
            position = "center-right"
        })
    end,
}
