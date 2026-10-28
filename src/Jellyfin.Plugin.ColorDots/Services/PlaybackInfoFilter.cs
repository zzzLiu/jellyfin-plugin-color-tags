using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ColorDots.Services;

public sealed class PlaybackInfoFilter : IAsyncActionFilter, IFilterMetadata, IAsyncResultFilter
{
	private readonly ColorStore _store;

	private readonly ILogger<PlaybackInfoFilter> _logger;

	public PlaybackInfoFilter(ColorStore store, ILogger<PlaybackInfoFilter> logger)
	{
		_store = store;
		_logger = logger;
	}

	public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
	{
		try
		{
			if (TryPrepare(context.HttpContext.Request, context.ActionArguments, out var itemId, out var rule))
			{
				int? clientBitrate = ReadClientBitrate(context.ActionArguments);
				int? num = PlaybackRules.CappedRequestBitrate(clientBitrate, rule);
				if (num.HasValue)
				{
					ApplyBitrate(context.ActionArguments, num.Value);
				}
				if (rule.ForceTranscode)
				{
					ApplyForceTranscode(context.ActionArguments);
				}
				_logger.LogInformation("Color Tags will limit playback of {ItemId}. Bitrate cap {CapBps} bps, force transcode {ForceTranscode}.", itemId, rule.MaxBitrateBps, rule.ForceTranscode);
			}
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Color Tags left this playback request unchanged.");
		}
		await next().ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
	{
		try
		{
			if (IsPlaybackInfo(context.HttpContext.Request) && TryItemId(context.RouteData.Values, context.HttpContext.Request, out var itemId))
			{
				PlaybackRule rule = PlaybackRules.Combine(_store.GetPlaybackRules(itemId));
				PlaybackInfoResponse val = Unwrap(context.Result);
				if (rule.Applies && val != null)
				{
					ApplyToResponse(val, rule);
				}
			}
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Color Tags left this playback response unchanged.");
		}
		await next().ConfigureAwait(continueOnCapturedContext: false);
	}

	internal static bool IsPlaybackInfo(HttpRequest request)
	{
		if (!HttpMethods.IsPost(request.Method))
		{
			return false;
		}
		string text = request.Path.Value ?? string.Empty;
		if (text.Contains("/Items/", StringComparison.OrdinalIgnoreCase))
		{
			return text.EndsWith("/PlaybackInfo", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	internal static bool TryItemId(IDictionary<string, object?> arguments, HttpRequest request, out Guid itemId)
	{
		itemId = Guid.Empty;
		if (arguments.TryGetValue("itemId", out object value) && value is Guid guid && guid != Guid.Empty)
		{
			itemId = guid;
			return true;
		}
		if (request.RouteValues.TryGetValue("itemId", out object value2) && Guid.TryParse(value2?.ToString(), out var result) && result != Guid.Empty)
		{
			itemId = result;
			return true;
		}
		return false;
	}

	internal static int? ReadClientBitrate(IDictionary<string, object?> arguments)
	{
		int? queryBitrate = (arguments.TryGetValue("maxStreamingBitrate", out object value) ? AsPositiveInt(value) : ((int?)null));
		object target = (arguments.TryGetValue("playbackInfoDto", out object value2) ? value2 : null);
		int? bodyBitrate = ReadPropertyInt(target, "MaxStreamingBitrate");
		int? profileBitrate = ReadPropertyInt(ReadProperty(target, "DeviceProfile"), "MaxStreamingBitrate");
		return PlaybackRules.ClientBitrate(queryBitrate, bodyBitrate, profileBitrate);
	}

	internal static void ApplyBitrate(IDictionary<string, object?> arguments, int bitrate)
	{
		arguments["maxStreamingBitrate"] = bitrate;
		if (!arguments.TryGetValue("playbackInfoDto", out object value) || value == null)
		{
			return;
		}
		SetProperty(value, "MaxStreamingBitrate", bitrate);
		object obj = ReadProperty(value, "DeviceProfile");
		if (obj != null)
		{
			int? num = ReadPropertyInt(obj, "MaxStreamingBitrate");
			if (!num.HasValue || num.Value > bitrate)
			{
				SetProperty(obj, "MaxStreamingBitrate", bitrate);
			}
		}
	}

	internal static void ApplyForceTranscode(IDictionary<string, object?> arguments)
	{
		arguments["enableDirectPlay"] = false;
		arguments["enableDirectStream"] = false;
		arguments["allowVideoStreamCopy"] = false;
		arguments["enableTranscoding"] = true;
		if (arguments.TryGetValue("playbackInfoDto", out object value) && value != null)
		{
			SetProperty(value, "EnableDirectPlay", false);
			SetProperty(value, "EnableDirectStream", false);
			SetProperty(value, "AllowVideoStreamCopy", false);
			SetProperty(value, "EnableTranscoding", true);
			object profile = ReadProperty(value, "DeviceProfile");
			ClearDirectPlayProfiles(profile);
		}
	}

	private static PlaybackInfoResponse? Unwrap(IActionResult result)
	{
		if (result is ObjectResult { Value: var value })
		{
			PlaybackInfoResponse val = (PlaybackInfoResponse)((value is PlaybackInfoResponse) ? value : null);
			if (val != null)
			{
				return val;
			}
		}
		object obj = result.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(result);
		return (PlaybackInfoResponse?)((obj is PlaybackInfoResponse) ? obj : null);
	}

	internal static void ApplyToResponse(PlaybackInfoResponse info, PlaybackRule rule)
	{
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		if (info.MediaSources == null || info.MediaSources.Count == 0)
		{
			return;
		}
		List<MediaSourceInfo> list = CloneSources(info.MediaSources);
		foreach (MediaSourceInfo item in list)
		{
			if (PlaybackRules.BlocksDirectPlay(item.Bitrate, rule) && !string.IsNullOrWhiteSpace(item.TranscodingUrl))
			{
				item.SupportsDirectPlay = false;
				item.SupportsDirectStream = false;
				if (!item.SupportsTranscoding)
				{
					item.SupportsTranscoding = true;
				}
				if (PlaybackRules.BitrateExceedsCap(item.Bitrate, rule))
				{
					item.TranscodeReasons |= TranscodeReason.ContainerBitrateExceedsLimit;
				}
				else if (item.TranscodeReasons == 0)
				{
					item.TranscodeReasons = TranscodeReason.ContainerNotSupported;
				}
			}
		}
		info.MediaSources = list;
	}

	private bool TryPrepare(HttpRequest request, IDictionary<string, object?> arguments, out Guid itemId, out PlaybackRule rule)
	{
		rule = PlaybackRule.None;
		if (!IsPlaybackInfo(request) || !TryItemId(arguments, request, out itemId))
		{
			itemId = Guid.Empty;
			return false;
		}
		rule = PlaybackRules.Combine(_store.GetPlaybackRules(itemId));
		return rule.Applies;
	}

	private static List<MediaSourceInfo> CloneSources(IReadOnlyList<MediaSourceInfo> sources)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			byte[] array = JsonSerializer.SerializeToUtf8Bytes(sources);
			List<MediaSourceInfo> list = JsonSerializer.Deserialize<List<MediaSourceInfo>>(array);
			if (list != null && list.Count == sources.Count)
			{
				for (int i = 0; i < list.Count; i++)
				{
					list[i].TranscodeReasons = sources[i].TranscodeReasons;
				}
				return list;
			}
		}
		catch (JsonException)
		{
		}
		return new List<MediaSourceInfo>(sources);
	}

	private static void ClearDirectPlayProfiles(object? profile)
	{
		if (profile == null)
		{
			return;
		}
		PropertyInfo property = profile.GetType().GetProperty("DirectPlayProfiles", BindingFlags.Instance | BindingFlags.Public);
		if (property == null || !property.CanWrite || property.GetIndexParameters().Length != 0)
		{
			return;
		}
		Type propertyType = property.PropertyType;
		if (propertyType.IsArray)
		{
			Type elementType = propertyType.GetElementType();
			if (elementType != null)
			{
				property.SetValue(profile, Array.CreateInstance(elementType, 0));
			}
		}
		else if (propertyType.IsGenericType && propertyType.GetConstructor(Type.EmptyTypes) != null)
		{
			property.SetValue(profile, Activator.CreateInstance(propertyType));
		}
	}

	private static object? ReadProperty(object? target, string name)
	{
		return target?.GetType().GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public)?.GetValue(target);
	}

	private static int? ReadPropertyInt(object? target, string name)
	{
		return AsPositiveInt(ReadProperty(target, name));
	}

	private static void SetProperty(object target, string name, object value)
	{
		PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
		if (!(property == null) && property.CanWrite && property.GetIndexParameters().Length == 0)
		{
			Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
			object value2 = value;
			if (value != null && !type.IsInstanceOfType(value))
			{
				value2 = Convert.ChangeType(value, type);
			}
			property.SetValue(target, value2);
		}
	}

	private static int? AsPositiveInt(object? value)
	{
		if (!(value is int num))
		{
			if (!(value is long num2))
			{
				if (value is string text)
				{
					string s = text;
					if (int.TryParse(s, out var result) && result > 0)
					{
						return result;
					}
				}
			}
			else
			{
				long num3 = num2;
				if (num3 > 0 && num3 <= int.MaxValue)
				{
					return (int)num3;
				}
			}
		}
		else
		{
			int num4 = num;
			if (num4 > 0)
			{
				return num4;
			}
		}
		return null;
	}
}
