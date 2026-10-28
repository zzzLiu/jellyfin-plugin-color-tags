using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.ColorDots.Services;

public static class DotPalette
{
	public const int WhiteId = 0;

	public const string WhiteHex = "#f4f4f5";

	public const int MaxLabelLength = 24;

	public const int MaxLabels = 24;

	public const int MaxBitrateBps = 1000000000;

	private static readonly Regex HexColor = new Regex("^[0-9a-fA-F]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public static bool TryMapLegacy(string? value, out int id)
	{
		id = 0;
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}
		switch (value.Trim().ToLowerInvariant())
		{
		case "red":
			id = 1;
			return true;
		case "green":
			id = 2;
			return true;
		case "blue":
			id = 3;
			return true;
		case "white":
			id = 0;
			return true;
		default:
			return false;
		}
	}

	public static bool TryNormalizeHex(string? value, out string hex)
	{
		hex = "#f4f4f5";
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}
		string text = value.Trim();
		if (text.StartsWith('#'))
		{
			text = text.Substring(1);
		}
		if (!HexColor.IsMatch(text))
		{
			return false;
		}
		hex = "#" + text.ToLower(CultureInfo.InvariantCulture);
		return true;
	}

	public static bool IsStockName(int id, string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}
		string text = name.Trim();
		return id switch
		{
			0 => text.Equals("White", StringComparison.OrdinalIgnoreCase) || text == "白", 
			1 => text.Equals("Red", StringComparison.OrdinalIgnoreCase) || text == "红", 
			2 => text.Equals("Green", StringComparison.OrdinalIgnoreCase) || text == "绿", 
			3 => text.Equals("Blue", StringComparison.OrdinalIgnoreCase) || text == "蓝", 
			_ => false, 
		};
	}

	public static string CleanLabel(string? value, string fallback)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return fallback;
		}
		char[] array = (from c in value.Trim()
			where !char.IsControl(c)
			select c).ToArray();
		if (array.Length == 0)
		{
			return fallback;
		}
		string text = new string(array);
		if (text.Length > 24)
		{
			text = text.Substring(0, 24);
		}
		return text;
	}

	public static bool TryNormalizeBitrate(int? value, out int? bps)
	{
		if ((!value.HasValue || value.GetValueOrDefault() == 0) ? true : false)
		{
			bps = null;
			return true;
		}
		if (value < 0 || value > 1000000000)
		{
			bps = null;
			return false;
		}
		bps = value;
		return true;
	}
}
