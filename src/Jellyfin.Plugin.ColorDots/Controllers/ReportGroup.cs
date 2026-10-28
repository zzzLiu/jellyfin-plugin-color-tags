using System.Collections.Generic;

namespace Jellyfin.Plugin.ColorDots.Controllers;

public sealed record ReportGroup(int Id, string Name, string Color, IReadOnlyList<ReportItem> Items)
{
	public int Count => Items.Count;

	public int Number { get; init; }
}
