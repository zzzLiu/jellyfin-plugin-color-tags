using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Jellyfin.Plugin.ColorDots.Services;

public static class PlaybackRequestEditor
{
	public static bool TryReadItemId(string? path, out Guid itemId)
	{
		itemId = Guid.Empty;
		if (string.IsNullOrEmpty(path))
		{
			return false;
		}
		int num = path.LastIndexOf("/Items/", StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return false;
		}
		num += "/Items/".Length;
		int num2 = path.IndexOf("/PlaybackInfo", num, StringComparison.OrdinalIgnoreCase);
		if (num2 <= num)
		{
			return false;
		}
		string text = path.Substring(num2 + "/PlaybackInfo".Length);
		if (text.Length != 0 && text != "/")
		{
			return false;
		}
		int num3 = num;
		if (Guid.TryParse(path.Substring(num3, num2 - num3), out itemId))
		{
			return itemId != Guid.Empty;
		}
		return false;
	}

	public static (string Body, string Query) Rewrite(string? body, string? query, PlaybackRule rule)
	{
		List<KeyValuePair<string, string>> pairs = ReadQuery(query);
		JsonObject jsonObject = ParseObject(body);
		if (!rule.Applies)
		{
			return (Body: body ?? string.Empty, Query: FormatQuery(pairs));
		}
		if (rule.ForceTranscode)
		{
			SetQuery(pairs, "enableDirectPlay", "false");
			SetQuery(pairs, "enableDirectStream", "false");
			SetQuery(pairs, "allowVideoStreamCopy", "false");
			SetQuery(pairs, "enableTranscoding", "true");
			if (jsonObject != null)
			{
				SetBool(jsonObject, "enableDirectPlay", value: false);
				SetBool(jsonObject, "enableDirectStream", value: false);
				SetBool(jsonObject, "allowVideoStreamCopy", value: false);
				SetBool(jsonObject, "enableTranscoding", value: true);
				JsonObject jsonObject2 = FindObject(jsonObject, "deviceProfile");
				if (jsonObject2 != null)
				{
					SetNode(jsonObject2, "directPlayProfiles", new JsonArray());
				}
			}
		}
		int? maxBitrateBps = rule.MaxBitrateBps;
		int num2;
		if (maxBitrateBps.HasValue)
		{
			int valueOrDefault = maxBitrateBps.GetValueOrDefault();
			if (valueOrDefault > 0)
			{
				int? num = ReadQueryInt(pairs, "maxStreamingBitrate") ?? ReadNodeInt(jsonObject, "maxStreamingBitrate");
				if (num.HasValue)
				{
					int valueOrDefault2 = num.GetValueOrDefault();
					if (valueOrDefault2 > 0)
					{
						num2 = Math.Min(valueOrDefault2, valueOrDefault);
						goto IL_013f;
					}
				}
				num2 = valueOrDefault;
				goto IL_013f;
			}
		}
		goto IL_0168;
		IL_0168:
		string item = ((jsonObject == null) ? (body ?? string.Empty) : jsonObject.ToJsonString());
		return (Body: item, Query: FormatQuery(pairs));
		IL_013f:
		int value = num2;
		SetQuery(pairs, "maxStreamingBitrate", value.ToString(CultureInfo.InvariantCulture));
		if (jsonObject != null)
		{
			SetInt(jsonObject, "maxStreamingBitrate", value);
		}
		goto IL_0168;
	}

	public static string PatchResponse(string json, PlaybackRule rule)
	{
		if (!rule.ForceTranscode || string.IsNullOrWhiteSpace(json))
		{
			return json;
		}
		JsonNode jsonNode;
		try
		{
			jsonNode = JsonNode.Parse(json);
		}
		catch (JsonException)
		{
			return json;
		}
		if (!(jsonNode is JsonObject jsonObject))
		{
			return json;
		}
		if (!(FindNode(jsonObject, "mediaSources") is JsonArray jsonArray))
		{
			return json;
		}
		bool flag = false;
		foreach (JsonNode item in jsonArray)
		{
			if (item is JsonObject obj)
			{
				string value = ReadNodeString(obj, "transcodingUrl");
				if (!string.IsNullOrWhiteSpace(value))
				{
					SetBool(obj, "supportsDirectPlay", value: false);
					SetBool(obj, "supportsDirectStream", value: false);
					SetBool(obj, "supportsTranscoding", value: true);
					flag = true;
				}
			}
		}
		if (!flag)
		{
			return json;
		}
		return jsonObject.ToJsonString();
	}

	private static List<KeyValuePair<string, string>> ReadQuery(string? query)
	{
		string text = query ?? string.Empty;
		if (text.StartsWith('?'))
		{
			text = text.Substring(1);
		}
		List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
		foreach (KeyValuePair<string, StringValues> item in QueryHelpers.ParseQuery(text))
		{
			list.Add(new KeyValuePair<string, string>(item.Key, item.Value.ToString()));
		}
		return list;
	}

	private static void SetQuery(List<KeyValuePair<string, string>> pairs, string name, string value)
	{
		for (int i = 0; i < pairs.Count; i++)
		{
			if (string.Equals(pairs[i].Key, name, StringComparison.OrdinalIgnoreCase))
			{
				pairs[i] = new KeyValuePair<string, string>(pairs[i].Key, value);
				return;
			}
		}
		pairs.Add(new KeyValuePair<string, string>(name, value));
	}

	private static int? ReadQueryInt(List<KeyValuePair<string, string>> pairs, string name)
	{
		foreach (KeyValuePair<string, string> pair in pairs)
		{
			if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase) && int.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result > 0)
			{
				return result;
			}
		}
		return null;
	}

	private static string FormatQuery(List<KeyValuePair<string, string>> pairs)
	{
		if (pairs.Count == 0)
		{
			return string.Empty;
		}
		return QueryString.Create(pairs).Value ?? string.Empty;
	}

	private static JsonObject? ParseObject(string? body)
	{
		if (string.IsNullOrWhiteSpace(body))
		{
			return null;
		}
		try
		{
			return JsonNode.Parse(body) as JsonObject;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static JsonObject? FindObject(JsonObject obj, string name)
	{
		return FindNode(obj, name) as JsonObject;
	}

	private static JsonNode? FindNode(JsonObject obj, string name)
	{
		foreach (KeyValuePair<string, JsonNode> item in obj)
		{
			if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase))
			{
				return item.Value;
			}
		}
		return null;
	}

	private static void SetBool(JsonObject obj, string name, bool value)
	{
		SetNode(obj, name, JsonValue.Create(value));
	}

	private static void SetInt(JsonObject obj, string name, int value)
	{
		SetNode(obj, name, JsonValue.Create(value));
	}

	private static void SetNode(JsonObject obj, string name, JsonNode? value)
	{
		string text = null;
		foreach (KeyValuePair<string, JsonNode> item in obj)
		{
			if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase))
			{
				text = item.Key;
				break;
			}
		}
		obj[text ?? name] = value;
	}

	private static int? ReadNodeInt(JsonObject? obj, string name)
	{
		if (obj == null || !(FindNode(obj, name) is JsonValue jsonValue))
		{
			return null;
		}
		if (jsonValue.TryGetValue<int>(out var value) && value > 0)
		{
			return value;
		}
		if (jsonValue.TryGetValue<string>(out string value2) && int.TryParse(value2, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result > 0)
		{
			return result;
		}
		return null;
	}

	private static string? ReadNodeString(JsonObject obj, string name)
	{
		if (!(FindNode(obj, name) is JsonValue jsonValue))
		{
			return null;
		}
		if (!jsonValue.TryGetValue<string>(out string value))
		{
			return null;
		}
		return value;
	}
}
