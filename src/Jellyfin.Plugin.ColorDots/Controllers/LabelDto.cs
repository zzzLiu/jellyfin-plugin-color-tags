using Jellyfin.Plugin.ColorDots.Services;

namespace Jellyfin.Plugin.ColorDots.Controllers;

public sealed class LabelDto
{
	public int Id { get; init; }

	public string Name { get; init; } = string.Empty;

	public string Color { get; init; } = "#f4f4f5";

	public bool Fixed { get; init; }

	public int Count { get; init; }

	public bool ForceTranscode { get; init; }

	public int? MaxBitrateBps { get; init; }

	public static LabelDto From(DotLabel label, int count = 0)
	{
		return new LabelDto
		{
			Id = label.Id,
			Name = label.Name,
			Color = label.Color,
			Fixed = label.Fixed,
			Count = count,
			ForceTranscode = label.ForceTranscode,
			MaxBitrateBps = label.MaxBitrateBps
		};
	}
}
