using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ColorDots.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
	public string RedLabel { get; set; } = "Red";

	public string GreenLabel { get; set; } = "Green";

	public string BlueLabel { get; set; } = "Blue";

	public string WhiteLabel { get; set; } = "White";
}
