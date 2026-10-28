using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.ColorDots.Services;

public static class TagEdit
{
	public static string[] Apply(IReadOnlyList<string>? current, IReadOnlyList<string>? desired, IReadOnlyCollection<string>? managed)
	{
		HashSet<string> managedSet = new HashSet<string>(StringComparer.Ordinal);
		if (managed != null)
		{
			foreach (string name in managed)
			{
				if (!string.IsNullOrWhiteSpace(name))
				{
					managedSet.Add(name);
				}
			}
		}

		List<string> desiredList = new List<string>();
		HashSet<string> desiredSet = new HashSet<string>(StringComparer.Ordinal);
		if (desired != null)
		{
			foreach (string name in desired)
			{
				if (!string.IsNullOrWhiteSpace(name) && desiredSet.Add(name))
				{
					desiredList.Add(name);
				}
			}
		}

		List<string> result = new List<string>();
		HashSet<string> have = new HashSet<string>(StringComparer.Ordinal);
		if (current != null)
		{
			foreach (string tag in current)
			{
				if (string.IsNullOrWhiteSpace(tag))
				{
					continue;
				}
				if (managedSet.Contains(tag) && !desiredSet.Contains(tag))
				{
					continue;
				}
				if (have.Add(tag))
				{
					result.Add(tag);
				}
			}
		}
		foreach (string name in desiredList)
		{
			if (have.Add(name))
			{
				result.Add(name);
			}
		}
		return result.ToArray();
	}

	public static bool Same(IReadOnlyList<string>? current, IReadOnlyList<string> next)
	{
		int left = 0;
		if (current != null)
		{
			foreach (string tag in current)
			{
				if (!string.IsNullOrWhiteSpace(tag))
				{
					left++;
				}
			}
		}
		if (left != next.Count)
		{
			return false;
		}
		int index = 0;
		if (current != null)
		{
			foreach (string tag in current)
			{
				if (string.IsNullOrWhiteSpace(tag))
				{
					continue;
				}
				if (!string.Equals(tag, next[index], StringComparison.Ordinal))
				{
					return false;
				}
				index++;
			}
		}
		return true;
	}
}
