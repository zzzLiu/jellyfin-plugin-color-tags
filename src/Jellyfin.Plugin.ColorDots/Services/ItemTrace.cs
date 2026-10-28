namespace Jellyfin.Plugin.ColorDots.Services;

public sealed record ItemTrace(string Path, string Name)
{
	public string? FileName { get; init; }

	public long? Size { get; init; }

	public long? Duration { get; init; }
}
