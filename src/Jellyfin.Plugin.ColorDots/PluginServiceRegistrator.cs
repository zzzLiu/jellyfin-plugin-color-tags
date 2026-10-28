using System;
using System.Collections.Generic;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.ColorDots.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ColorDots;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
	public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
	{
		serviceCollection.AddSingleton((IServiceProvider services) =>
		{
			IApplicationPaths paths = services.GetRequiredService<IApplicationPaths>();
			ILogger<ColorStore> logger = services.GetRequiredService<ILogger<ColorStore>>();
			IUserManager users = services.GetRequiredService<IUserManager>();
			return new ColorStore(ColorStore.StorePath(paths.DataPath), logger, PreferredAdminKeys(users, logger));
		});
		serviceCollection.AddSingleton<OfficialTagSync>();
		serviceCollection.AddHostedService<InitialTagSyncService>();
		serviceCollection.AddSingleton<PlaybackInfoFilter>();
		serviceCollection.AddTransient<IStartupFilter, PlaybackRewriteStartupFilter>();
		serviceCollection.AddTransient<IStartupFilter, ScriptInjectionStartupFilter>();
		serviceCollection.PostConfigure((MvcOptions options) =>
		{
			options.Filters.AddService<PlaybackInfoFilter>();
		});
	}

	internal static IReadOnlyList<string> PreferredAdminKeys(IUserManager users, ILogger logger)
	{
		try
		{
			List<(string Key, string Name)> found = new List<(string, string)>();
			foreach (Jellyfin.Database.Implementations.Entities.User user in users.GetUsers())
			{
				if (user == null || user.Id == Guid.Empty || !IsAdmin(user))
				{
					continue;
				}
				found.Add((user.Id.ToString("N"), user.Username ?? string.Empty));
			}
			found.Sort((left, right) =>
			{
				int leftRank = string.Equals(left.Name, "zzzliu", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
				int rightRank = string.Equals(right.Name, "zzzliu", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
				int rank = leftRank.CompareTo(rightRank);
				return (rank != 0) ? rank : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
			});
			List<string> keys = new List<string>(found.Count);
			foreach ((string Key, string Name) item in found)
			{
				keys.Add(item.Key);
			}
			return keys;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Jellyfin Color Tags could not list administrators. Existing assignments still migrate from the saved file.");
			return Array.Empty<string>();
		}
	}

	private static bool IsAdmin(Jellyfin.Database.Implementations.Entities.User user)
	{
		if (user.Permissions == null)
		{
			return false;
		}
		foreach (Jellyfin.Database.Implementations.Entities.Permission permission in user.Permissions)
		{
			if (permission != null && permission.Kind == PermissionKind.IsAdministrator && permission.Value)
			{
				return true;
			}
		}
		return false;
	}
}
