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