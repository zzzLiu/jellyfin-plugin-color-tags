using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ColorDots.Services;

public sealed class InitialTagSyncService : IHostedService
{
	private readonly ColorStore _store;

	private readonly OfficialTagSync _tags;

	private readonly ILogger<InitialTagSyncService> _logger;

	public InitialTagSyncService(ColorStore store, OfficialTagSync tags, ILogger<InitialTagSyncService> logger)
	{
		_store = store;
		_tags = tags;
		_logger = logger;
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (!_store.NeedsInitialTagWrite())
		{
			return Task.CompletedTask;
		}
		_ = WriteExistingAsync(cancellationToken);
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	private async Task WriteExistingAsync(CancellationToken cancellationToken)
	{
		try
		{
			int failed = 0;
			int seen = 0;
			foreach (Guid itemId in _store.AssignedItemIds())
			{
				cancellationToken.ThrowIfCancellationRequested();
				seen++;
				try
				{
					await _tags.SyncAsync(itemId, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					failed++;
					_logger.LogWarning(ex, "Jellyfin Color Tags could not write tags for {ItemId}.", itemId);
				}
			}
			if (failed == 0)
			{
				_store.MarkTagsWritten();
				_logger.LogInformation("Jellyfin Color Tags wrote label names onto {Count} items.", seen);
			}
			else
			{
				_logger.LogWarning("Jellyfin Color Tags left tag sync unfinished. {Failed} of {Count} items failed and will be tried again next start.", failed, seen);
			}
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("Jellyfin Color Tags stopped writing existing label names.");
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Jellyfin Color Tags could not write existing label names. It will try again next start.");
		}
	}
}
