namespace Jellyfin.Plugin.ColorDots.Controllers;

public sealed record ReportItem(string Id, string Name, string Type, int? Year, string? Path, string? Library, string? Place)
{
	public bool Missing { get; init; }

	public string? FileName { get; init; }

	public long? Size { get; set; }

	public long? Duration { get; set; }
}
