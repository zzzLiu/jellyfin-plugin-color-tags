using System.Collections.Generic;

namespace Jellyfin.Plugin.ColorDots.Controllers;

public sealed class ColorsRequest
{
	public List<string>? Ids { get; set; }
}
