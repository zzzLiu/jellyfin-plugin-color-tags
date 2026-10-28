using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.ColorDots.Services;

public static class LibraryMatcher
{
	public readonly record struct LibraryLocation(string Name, IEnumerable<string>? Locations);

	public readonly record struct LibraryHit(string Name, string Root);

	public static string? Match(string? itemPath, IEnumerable<LibraryLocation> libraries)
	{
		return Find(itemPath, libraries)?.Name;
	}

	public static LibraryHit? Find(string? itemPath, IEnumerable<LibraryLocation> libraries)
	{
		string text = Normalize(itemPath);
		if (text.Length == 0 || libraries == null)
		{
			return null;
		}
		LibraryHit? result = null;
		int num = -1;
		foreach (LibraryLocation library in libraries)
		{
			if (string.IsNullOrWhiteSpace(library.Name) || library.Locations == null)
			{
				continue;
			}
			foreach (string location in library.Locations)
			{
				string text2 = Normalize(location);
				if (text2.Length != 0 && text2.Length > num && IsPrefix(text, text2))
				{
					num = text2.Length;
					result = new LibraryHit(library.Name.Trim(), text2);
				}
			}
		}
		return result;
	}

	public static string? DescribePlace(string? libraryName, string? itemPath, string? libraryRoot)
	{
		string text = (string.IsNullOrWhiteSpace(libraryName) ? null : libraryName.Trim());
		string text2 = RelativeDirectory(itemPath, libraryRoot);
		if (text == null)
		{
			if (!string.IsNullOrEmpty(text2))
			{
				return text2;
			}
			return null;
		}
		if (string.IsNullOrEmpty(text2))
		{
			return text;
		}
		return text + " / " + text2;
	}

	private static string RelativeDirectory(string? itemPath, string? libraryRoot)
	{
		string text = Normalize(itemPath);
		string text2 = Normalize(libraryRoot);
		if (text.Length == 0 || text2.Length == 0 || !IsPrefix(text, text2))
		{
			return string.Empty;
		}
		string text3 = ((text.Length == text2.Length) ? string.Empty : text.Substring(text2.Length + 1));
		if (text3.Length == 0)
		{
			return string.Empty;
		}
		int num = text3.LastIndexOf('/');
		if (!((num >= 0) ? text3.Substring(num + 1) : text3).Contains('.', StringComparison.Ordinal))
		{
			return text3;
		}
		if (num < 0)
		{
			return string.Empty;
		}
		return text3.Substring(0, num);
	}

	private static bool IsPrefix(string item, string prefix)
	{
		if (item.Equals(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (!item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || item.Length == prefix.Length)
		{
			return false;
		}
		return item[prefix.Length] == '/';
	}

	private static string Normalize(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}
		return path.Trim().Replace('\\', '/').TrimEnd('/');
	}
}
