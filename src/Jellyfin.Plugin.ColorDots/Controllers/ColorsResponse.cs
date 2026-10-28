using System.Collections.Generic;

namespace Jellyfin.Plugin.ColorDots.Controllers;

public sealed record ColorsResponse(Dictionary<string, int[]> Colors);
