-- MP ped outfit share codes. The plumbing (cooldowns, quota, size cap, convar gate) is in server/lib/codes.lua.

local function isSlotList(t)
    if t == nil then return true end
    return ShareCodes.eachEntry(t, 16, function(slot, item)
        return ShareCodes.isInteger(tonumber(slot), 0, 12)
            and type(item) == 'table'
            and ShareCodes.isInteger(item.Item, -1, 4096)
            and ShareCodes.isInteger(item.Texture, -1, 512)
    end)
end

ShareCodes.register({
    name = 'Outfits',
    label = 'outfit',
    table = 'vmenu_outfits',
    convar = 'vmenu_outfit_codes',
    validate = function(data)
        return type(data) == 'table' and (data.Clothes ~= nil or data.Props ~= nil)
            and isSlotList(data.Clothes) and isSlotList(data.Props)
    end,
})
