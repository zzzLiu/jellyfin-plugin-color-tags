namespace Jellyfin.Plugin.ColorDots.Services;

public readonly record struct PlaybackRule(int? MaxBitrateBps, bool ForceTranscode)
{
	public bool Applies
	{
		get
		{
			if (!ForceTranscode)
			{
				int? maxBitrateBps = MaxBitrateBps;
				if (maxBitrateBps.HasValue)
				{
					return maxBitrateBps.GetValueOrDefault() > 0;
				}
				return false;
			}
			return true;
		}
	}

	public static PlaybackRule None => new PlaybackRule(null, ForceTranscode: false);
}
