using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ColorDots.Services;

public sealed class PlaybackRewriteStartupFilter : IStartupFilter
{
	private readonly ColorStore _store;

	private readonly ILogger<PlaybackRewriteStartupFilter> _logger;

	public PlaybackRewriteStartupFilter(ColorStore store, ILogger<PlaybackRewriteStartupFilter> logger)
	{
		_store = store;
		_logger = logger;
	}

	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
	{
		return (IApplicationBuilder app) =>
		{
			app.Use(InvokeAsync);
			next(app);
		};
	}

	private async Task InvokeAsync(HttpContext context, Func<Task> next)
	{
		HttpRequest request = context.Request;
		if (!HttpMethods.IsPost(request.Method) || !PlaybackRequestEditor.TryReadItemId(request.Path.Value, out var itemId))
		{
			await next().ConfigureAwait(continueOnCapturedContext: false);
			return;
		}
		PlaybackRule rule;
		try
		{
			rule = PlaybackRules.Combine(_store.GetPlaybackRules(itemId));
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Color Tags left playback of {ItemId} unchanged.", itemId);
			await next().ConfigureAwait(continueOnCapturedContext: false);
			return;
		}
		if (!rule.Applies)
		{
			await next().ConfigureAwait(continueOnCapturedContext: false);
			return;
		}
		string text = await ReadBody(request).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			(string Body, string Query) tuple = PlaybackRequestEditor.Rewrite(text, request.QueryString.Value, rule);
			string item = tuple.Body;
			string item2 = tuple.Query;
			byte[] bytes = Encoding.UTF8.GetBytes(item);
			request.Body = new MemoryStream(bytes);
			request.ContentLength = bytes.Length;
			request.QueryString = (string.IsNullOrEmpty(item2) ? QueryString.Empty : new QueryString(item2));
			_logger.LogInformation("Color Tags will limit playback of {ItemId}. Bitrate cap {CapBps} bps, force transcode {ForceTranscode}.", itemId, rule.MaxBitrateBps, rule.ForceTranscode);
		}
		catch (Exception exception2)
		{
			_logger.LogWarning(exception2, "Color Tags left the playback request for {ItemId} unchanged.", itemId);
			request.Body = new MemoryStream(Encoding.UTF8.GetBytes(text));
			request.ContentLength = Encoding.UTF8.GetByteCount(text);
		}
		if (rule.ForceTranscode)
		{
			request.Headers.Remove("Accept-Encoding");
			Stream originalResponse = context.Response.Body;
			using MemoryStream buffer = new MemoryStream();
			context.Response.Body = buffer;
			try
			{
				await next().ConfigureAwait(continueOnCapturedContext: false);
			}
			catch
			{
				context.Response.Body = originalResponse;
				throw;
			}
			context.Response.Body = originalResponse;
			string text2 = context.Response.ContentType ?? string.Empty;
			if (context.Response.StatusCode != 200 || (text2.Length != 0 && !text2.Contains("json", StringComparison.OrdinalIgnoreCase)))
			{
				buffer.Position = 0L;
				await buffer.CopyToAsync(originalResponse).ConfigureAwait(continueOnCapturedContext: false);
				return;
			}
			string text3;
			using (StreamReader reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true))
			{
				buffer.Position = 0L;
				text3 = await reader.ReadToEndAsync().ConfigureAwait(continueOnCapturedContext: false);
			}
			try
			{
				text3 = PlaybackRequestEditor.PatchResponse(text3, rule);
			}
			catch (Exception exception3)
			{
				_logger.LogWarning(exception3, "Color Tags left the playback response for {ItemId} unchanged.", itemId);
			}
			byte[] bytes2 = Encoding.UTF8.GetBytes(text3);
			context.Response.ContentLength = bytes2.Length;
			context.Response.Headers.Remove("Content-Encoding");
			context.Response.Headers.Remove("ETag");
			await originalResponse.WriteAsync(bytes2).ConfigureAwait(continueOnCapturedContext: false);
			return;
		}
		await next().ConfigureAwait(continueOnCapturedContext: false);
	}

	private static async Task<string> ReadBody(HttpRequest request)
	{
		request.EnableBuffering();
		using StreamReader reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, 1024, leaveOpen: true);
		string result = await reader.ReadToEndAsync().ConfigureAwait(continueOnCapturedContext: false);
		request.Body.Position = 0L;
		return result;
	}
}
