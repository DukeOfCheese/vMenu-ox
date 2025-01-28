-- You should not be editing this file unless you know what you are doing. This shouldn't need to be changed.



--#region Input Dialogs
---@class inputDialog
---@param windowTitle string
---@param defaultText string
---@param maxLength integer | 30
---@return string
exports(
	"inputDialog",
	function(windowTitle, defaultText, maxLength)
		local input =
			lib.inputDialog(
				"vMenu",
				{
					{ type = "input", label = windowTitle, default = defaultText, max = maxLength or 30 }
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

---@class colourDialog
---@param type integer 1 for primary, 2 for secondary
---@return string
exports("colourDialog", function(type)
	if not cache.vehicle then return "" end -- edge case
	local defaultColour = type == 1 and rgbToHex(GetVehicleCustomPrimaryColour(cache.vehicle)) or
		rgbToHex(GetVehicleCustomSecondaryColour(cache.vehicle))
	local input = lib.inputDialog("vMenu", {
		{ type = 'color', label = 'Select a Colour', default = defaultColour },
	})
	return input and input[1] or ""
end)
--#endregion

--#region Misc Exports
---@class copyToClipboard
---@param text string
exports("copyToClipboard", function(text)
	print("Copied to clipboard: " .. text)
	lib.setClipboard(text)
	return ""
end)
--#endregion

--#region Client Stuff
CreateThread(function()
	-- # Sets whether or not players can lose their head props when they are hit/pushed.
	-- # true = Props will stay on player (default vMenu and GTA Online behavior)
	-- # false = vMenu will not touch this feature, by default this means that head props will fall off when the player is hit, which is the default behavior for GTA V Single Player peds.
	-- setr keep_player_head_props false (this is configured in your permissions.cfg)

	-- this is just to replace needing to run a constant thread loop on the client
	if GetConvar("keep_player_head_props", "false") == "false" then
		lib.onCache("ped", function(value)
			SetPedCanLosePropsOnDamage(value, false, 0)
			SetPedConfigFlag(value, 427, true)
		end)
	end

	-- so blackouts dont affect vehicle headlights
	if GetConvar("vmenu_blackout_affect_vehicles", "false") == "false" then
		SetArtificialLightsStateAffectsVehicles(false)
	end
end)
--#endregion
