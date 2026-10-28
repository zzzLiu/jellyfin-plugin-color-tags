using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.ColorDots.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ColorDots;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
	public const string PluginGuid = "a7e4c1d2-6b58-4f0a-9c33-81d5e2b47a60";

	public static Plugin? Instance { get; private set; }

	public override string Name => "Jellyfin Color Tags";

	public override string Description => "Mark items with a colored dot on posters and in the web player.";

	public override Guid Id => Guid.Parse("a7e4c1d2-6b58-4f0a-9c33-81d5e2b47a60");

	public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
		: base(applicationPaths, xmlSerializer)
	{
		Instance = this;
	}

	public IEnumerable<PluginPageInfo> GetPages()
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Expected Obj, but got Unknown
		string arg = GetType().Namespace;
		return new PluginPageInfo[2]
		{
			new PluginPageInfo
			{
				Name = Name,
				DisplayName = "Jellyfin Color Tags",
				EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", arg),
				EnableInMainMenu = true,
				MenuSection = "server",
				MenuIcon = "palette"
			},
			new PluginPageInfo
			{
				Name = "ColorDotsConfig.js",
				EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.js", arg)
			}
		};
	}
}
