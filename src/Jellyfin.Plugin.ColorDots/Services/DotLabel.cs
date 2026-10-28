namespace Jellyfin.Plugin.ColorDots.Services;

public sealed record DotLabel(int Id, string Name, string Color, bool Fixed)
{
	public bool ForceTranscode { get; init; }

	public int? MaxBitrateBps { get; init; }
}
