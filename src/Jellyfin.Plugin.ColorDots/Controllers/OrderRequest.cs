using System.Collections.Generic;

namespace Jellyfin.Plugin.ColorDots.Controllers;

public sealed class OrderRequest
{
	public List<int>? Ids { get; set; }
}
