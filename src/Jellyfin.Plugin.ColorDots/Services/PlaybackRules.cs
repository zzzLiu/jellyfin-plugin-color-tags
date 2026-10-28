using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.ColorDots.Services;

public static class PlaybackRules
{
	public const int AssumedBitrateWhenUnknown = 40000000;

	public static PlaybackRule Combine(IEnumerable<PlaybackRule> rules)
	{
		int? maxBitrateBps = null;
		bool forceTranscode = false;
		foreach (PlaybackRule rule in rules)
		{
			int? maxBitrateBps2 = rule.MaxBitrateBps;
			if (maxBitrateBps2.HasValue)
			{
				int valueOrDefault = maxBitrateBps2.GetValueOrDefault();
				if (valueOrDefault > 0)
				{
					maxBitrateBps = ((!maxBitrateBps.HasValue) ? valueOrDefault : Math.Min(maxBitrateBps.Value, valueOrDefault));
				}
			}
			if (rule.ForceTranscode)
			{
				forceTranscode = true;
			}
		}
		return new PlaybackRule(maxBitrateBps, forceTranscode);
	}

	public static int? ClientBitrate(int? queryBitrate, int? bodyBitrate, int? profileBitrate)
	{
		if (queryBitrate.HasValue && queryBitrate.GetValueOrDefault() > 0)
		{
			return queryBitrate;
		}
		if (bodyBitrate.HasValue && bodyBitrate.GetValueOrDefault() > 0)
		{
			return bodyBitrate;
		}
		if (profileBitrate.HasValue && profileBitrate.GetValueOrDefault() > 0)
		{
			return profileBitrate;
		}
		return null;
	}

	public static int? CappedRequestBitrate(int? clientBitrate, PlaybackRule rule)
	{
		int? maxBitrateBps = rule.MaxBitrateBps;
		if (maxBitrateBps.HasValue)
		{
			int valueOrDefault = maxBitrateBps.GetValueOrDefault();
			if (valueOrDefault > 0)
			{
				if (clientBitrate.HasValue)
				{
					int valueOrDefault2 = clientBitrate.GetValueOrDefault();
					if (valueOrDefault2 > 0)
					{
						return Math.Min(valueOrDefault2, valueOrDefault);
					}
				}
				return valueOrDefault;
			}
		}
		return null;
	}

	public static bool BitrateExceedsCap(int? sourceBitrate, PlaybackRule rule)
	{
		int? maxBitrateBps = rule.MaxBitrateBps;
		if (maxBitrateBps.HasValue)
		{
			int valueOrDefault = maxBitrateBps.GetValueOrDefault();
			if (valueOrDefault > 0)
			{
				int num = ((sourceBitrate.HasValue && sourceBitrate.GetValueOrDefault() > 0) ? sourceBitrate.Value : 40000000);
				return num > valueOrDefault;
			}
		}
		return false;
	}

	public static bool BlocksDirectPlay(int? sourceBitrate, PlaybackRule rule)
	{
		if (!rule.ForceTranscode)
		{
			return BitrateExceedsCap(sourceBitrate, rule);
		}
		return true;
	}
}
