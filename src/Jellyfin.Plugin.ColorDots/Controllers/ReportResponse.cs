using System.Collections.Generic;

namespace Jellyfin.Plugin.ColorDots.Controllers;

public sealed record ReportResponse(IReadOnlyList<ReportGroup> Groups);
