exports(
	"inputDialog",
	function(windowTitle, defaultText, maxLength)
		local input =
			lib.inputDialog(
			"vMenu",
			{
				{type = "input", label = windowTitle, default = defaultText, max = maxLength or 30}
			}
		)
		return input and input[1] or ""
	end
)

function rgbToHex(r, g, b)
    -- Ensure values are within RGB bounds (0-255)
    r = math.max(0, math.min(255, r))
    g = math.max(0, math.min(255, g))
    b = math.max(0, math.min(255, b))
    
    -- Format RGB values to hexadecimal string
    return string.format("#%02X%02X%02X", r, g, b)
end

exports("colourDialog", function(type)
	if not cache.vehicle then return "" end -- edge case
	local defaultColour = type == 1 and rgbToHex(GetVehicleCustomPrimaryColour(cache.vehicle)) or rgbToHex(GetVehicleCustomSecondaryColour(cache.vehicle))
    local input = lib.inputDialog("vMenu", {
        {type = 'color', label = 'Select a Colour', default = defaultColour },
    })
    return input and input[1] or ""
end)

RegisterCommand("test", function()
	print("das")
	exports.vMenu:colourDialog(1)
end)
