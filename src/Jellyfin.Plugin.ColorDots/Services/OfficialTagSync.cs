using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ColorDots.Services;

public sealed class OfficialTagSync
{
	private readonly ColorStore _store;

	private readonly ILibraryManager _library;

	private readonly ILogger<OfficialTagSync> _logger;

	public OfficialTagSync(ColorStore store, ILibraryManager library, ILogger<OfficialTagSync> logger)
	{
		_store = store;
		_library = library;
		_logger = logger;
	}

	public Task SyncAsync(Guid itemId, CancellationToken cancellationToken)
	{
		return SyncAsync(itemId, Array.Empty<string>(), cancellationToken);
	}

	public async Task SyncAsync(Guid itemId, IReadOnlyCollection<string> extraManagedNames, CancellationToken cancellationToken)
	{
		if (itemId == Guid.Empty)
		{
			return;
		}
		BaseItem? item = Find(itemId);
		if (item == null)
		{
			throw new InvalidOperationException("Item was not found.");
		}
		string[] next = TagEdit.Apply(item.Tags, DesiredNames(itemId), ManagedNames(extraManagedNames));
		if (TagEdit.Same(item.Tags, next))
		{
			return;
		}
		item.Tags = next;
		BaseItem parent = item.GetParent() ?? item;
		await _library.UpdateItemAsync(item, parent, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
	}

	public async Task SyncManyAsync(IEnumerable<Guid> itemIds, IReadOnlyCollection<string> extraManagedNames, CancellationToken cancellationToken)
	{
		if (itemIds == null)
		{
			return;
		}
		foreach (Guid itemId in itemIds.Distinct())
		{
			try
			{
				await SyncAsync(itemId, extraManagedNames, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Jellyfin Color Tags could not update tags for {ItemId}.", itemId);
			}
		}
	}

	private BaseItem? Find(Guid itemId)
	{
		try
		{
			return _library.GetItemById(itemId);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Jellyfin Color Tags could not look up item {ItemId} to update tags.", itemId);
			throw;
		}
	}

	private List<string> DesiredNames(Guid itemId)
	{
		List<string> names = new List<string>();
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (DotLabel label in LabelsFor(_store.GetItemLabels(itemId)))
		{
			if (label.Id == 0 || string.IsNullOrWhiteSpace(label.Name))
			{
				continue;
			}
			if (seen.Add(label.Name))
			{
				names.Add(label.Name);
			}
		}
		return names;
	}

	private List<string> ManagedNames(IReadOnlyCollection<string>? extra)
	{
		List<string> names = new List<string>();
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (DotLabel label in _store.GetLabels())
		{
			if (label.Id == 0 || string.IsNullOrWhiteSpace(label.Name))
			{
				continue;
			}
			if (seen.Add(label.Name))
			{
				names.Add(label.Name);
			}
		}
		if (extra != null)
		{
			foreach (string name in extra)
			{
				if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
				{
					names.Add(name);
				}
			}
		}
		return names;
	}

	private IEnumerable<DotLabel> LabelsFor(IReadOnlyList<int> ids)
	{
		foreach (int id in ids)
		{
			foreach (DotLabel label in _store.GetLabels())
			{
				if (label.Id == id)
				{
					yield return label;
					break;
				}
			}
		}
	}
}
