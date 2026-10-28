namespace Jellyfin.Plugin.ColorDots.Controllers;

public sealed class LabelWriteRequest
{
	public string? Name { get; set; }

	public string? Color { get; set; }

	public bool ForceTranscode { get; set; }

	public int? MaxBitrateBps { get; set; }
}
