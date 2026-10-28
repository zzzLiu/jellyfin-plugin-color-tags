using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.ColorDots.Services;

public static class TagRelink
{
	private readonly record struct MediaKey(string Leaf, string Parent, string Grandparent, bool File);

	private const int MaxWalkDepth = 8;

	private const int MaxWalkDirs = 8000;

	private static readonly Regex SeasonNameRegex = new Regex("^(?:season[\\s._-]*\\d+|s\\d{1,2}|specials|第\\s*\\d+\\s*季)$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public static bool Same(string? hintPath, string? candidatePath)
	{
		string text = Normalize(hintPath);
		string text2 = Normalize(candidatePath);
		if (text.Length == 0 || text2.Length == 0)
		{
			return false;
		}
		if (text.Equals(text2, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		MediaKey? mediaKey = KeyFrom(text);
		MediaKey? mediaKey2 = KeyFrom(text2);
		if (!mediaKey.HasValue || !mediaKey2.HasValue)
		{
			return false;
		}
		if (SameKey(mediaKey.Value, mediaKey2.Value))
		{
			return true;
		}
		if (!FolderOfFile(mediaKey.Value, mediaKey2.Value))
		{
			return FolderOfFile(mediaKey2.Value, mediaKey.Value);
		}
		return true;
	}

	public static IReadOnlyList<string> CandidatePaths(string? hintPath, IEnumerable<string>? libraryRoots)
	{
		MediaKey? mediaKey = KeyFrom(hintPath);
		if (!mediaKey.HasValue)
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>();
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (Exists(hintPath))
		{
			Add(list, seen, hintPath);
		}
		if (libraryRoots == null)
		{
			return list;
		}
		foreach (string libraryRoot in libraryRoots)
		{
			if (!string.IsNullOrWhiteSpace(libraryRoot) && Directory.Exists(libraryRoot))
			{
				string text = Relative(mediaKey.Value);
				if (text != null)
				{
					Seek(libraryRoot, text, list, seen);
				}
			}
		}
		return list;
	}

	public static string? FileName(string? path)
	{
		string text = Normalize(path);
		if (text.Length == 0)
		{
			return null;
		}
		string fileName = Path.GetFileName(text);
		if (!string.IsNullOrWhiteSpace(fileName))
		{
			return fileName;
		}
		return null;
	}

	public static bool SameFileName(string? hintPath, string? candidatePath)
	{
		string text = FileName(hintPath);
		string text2 = FileName(candidatePath);
		if (text != null && text2 != null)
		{
			return text.Equals(text2, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	public static bool SameSizeAndDuration(long? size, long? durationTicks, long? candidateSize, long? candidateDurationTicks)
	{
		if (size.HasValue && durationTicks.HasValue && size == candidateSize)
		{
			return durationTicks == candidateDurationTicks;
		}
		return false;
	}

	public static bool Exists(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}
		if (!File.Exists(path))
		{
			return Directory.Exists(path);
		}
		return true;
	}

	private static string? Relative(MediaKey key)
	{
		if (key.File)
		{
			if (IsSeason(key.Parent))
			{
				if (key.Grandparent.Length != 0)
				{
					return Path.Combine(key.Grandparent, key.Parent, key.Leaf);
				}
				return null;
			}
			if (key.Parent.Length != 0)
			{
				return Path.Combine(key.Parent, key.Leaf);
			}
			return null;
		}
		if (IsSeason(key.Leaf))
		{
			if (key.Parent.Length != 0)
			{
				return Path.Combine(key.Parent, key.Leaf);
			}
			return null;
		}
		return key.Leaf;
	}

	private static void Seek(string root, string relative, List<string> found, HashSet<string> seen)
	{
		int walked = 0;
		Walk(root, relative, found, seen, 0, ref walked);
	}

	private static void Walk(string dir, string relative, List<string> found, HashSet<string> seen, int depth, ref int walked)
	{
		AddIfExists(found, seen, Path.Combine(dir, relative));
		if (depth >= 8 || walked >= 8000)
		{
			return;
		}
		foreach (string item in SafeDirs(dir))
		{
			walked++;
			Walk(item, relative, found, seen, depth + 1, ref walked);
			if (walked >= 8000)
			{
				break;
			}
		}
	}

	private static bool SameKey(MediaKey left, MediaKey right)
	{
		if (left.File != right.File || !left.Leaf.Equals(right.Leaf, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (left.File)
		{
			if (IsSeason(left.Parent))
			{
				if (left.Grandparent.Length > 0 && left.Parent.Equals(right.Parent, StringComparison.OrdinalIgnoreCase))
				{
					return left.Grandparent.Equals(right.Grandparent, StringComparison.OrdinalIgnoreCase);
				}
				return false;
			}
			if (left.Parent.Length > 0)
			{
				return left.Parent.Equals(right.Parent, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
		if (IsSeason(left.Leaf))
		{
			if (left.Parent.Length > 0)
			{
				return left.Parent.Equals(right.Parent, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
		return true;
	}

	private static bool FolderOfFile(MediaKey file, MediaKey folder)
	{
		if (!file.File || folder.File || IsSeason(file.Parent) || file.Parent.Length == 0)
		{
			return false;
		}
		return file.Parent.Equals(folder.Leaf, StringComparison.OrdinalIgnoreCase);
	}

	private static void AddIfExists(List<string> found, HashSet<string> seen, string path)
	{
		if (Exists(path))
		{
			Add(found, seen, path);
		}
	}

	private static void Add(List<string> found, HashSet<string> seen, string path)
	{
		if (seen.Add(Normalize(path)))
		{
			found.Add(path);
		}
	}

	private static IEnumerable<string> SafeDirs(string root)
	{
		IEnumerable<string> enumerable;
		try
		{
			enumerable = Directory.EnumerateDirectories(root);
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			yield break;
		}
		foreach (string item in enumerable)
		{
			yield return item;
		}
	}

	private static MediaKey? KeyFrom(string? path)
	{
		string[] array = Normalize(path).Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 0)
		{
			return null;
		}
		string text = array[^1];
		string parent = ((array.Length >= 2) ? array[^2] : string.Empty);
		string grandparent = ((array.Length >= 3) ? array[^3] : string.Empty);
		return new MediaKey(text, parent, grandparent, text.Contains('.'));
	}

	private static bool IsSeason(string name)
	{
		if (name.Length > 0)
		{
			return SeasonNameRegex.IsMatch(name);
		}
		return false;
	}

	private static string Normalize(string? path)
	{
		if (!string.IsNullOrWhiteSpace(path))
		{
			return path.Trim().Replace('\\', '/').TrimEnd('/');
		}
		return string.Empty;
	}
}
