using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Jellyfin.Plugin.ColorDots.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ColorDots.Services;

public sealed class ColorStore
{
	private sealed class MigrationRecord
	{
		public int FromVersion { get; set; }

		public string PreferredUser { get; set; } = string.Empty;

		public string UsersSnapshot { get; set; } = "frozen";

		public bool TagsWritten { get; set; }
	}

	private sealed class StoreFile
	{
		public int Version { get; set; } = 3;

		public int NextId { get; set; } = 4;

		public List<LabelRecord> Labels { get; set; } = new List<LabelRecord>();

		public Dictionary<string, List<int>> Items { get; set; } = new Dictionary<string, List<int>>(StringComparer.Ordinal);

		public Dictionary<string, Dictionary<string, int>> Users { get; set; } = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

		public Dictionary<string, TraceRecord> Traces { get; set; } = new Dictionary<string, TraceRecord>(StringComparer.Ordinal);

		public MigrationRecord? Migration { get; set; }
	}

	private sealed class TraceRecord
	{
		public string Path { get; set; } = string.Empty;

		public string Name { get; set; } = string.Empty;

		public string FileName { get; set; } = string.Empty;

		public long? Size { get; set; }

		public long? Duration { get; set; }
	}

	private sealed class LabelRecord
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public string Color { get; set; } = "#f4f4f5";

		public bool ForceTranscode { get; set; }

		public int? MaxBitrateBps { get; set; }
	}

	internal const string FallbackAdminKey = "dec84115884943b3bb091446124ba36d";

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};

	private readonly string _path;

	private readonly ILogger? _logger;

	private readonly IReadOnlyList<string> _preferredUserKeys;

	private readonly object _gate = new object();

	private StoreFile _data = NewStore();

	private bool _loaded;

	public ColorStore(string storeFilePath)
		: this(storeFilePath, null, null)
	{
	}

	public ColorStore(string storeFilePath, ILogger? logger, IReadOnlyList<string>? preferredUserKeys)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(storeFilePath, "storeFilePath");
		_path = storeFilePath;
		_logger = logger;
		_preferredUserKeys = preferredUserKeys ?? System.Array.Empty<string>();
	}

	public ColorStore(IApplicationPaths paths, ILogger<ColorStore> logger)
		: this(StorePath(paths.DataPath), logger, null)
	{
	}

	public static string StorePath(string dataPath)
	{
		return Path.Combine(dataPath, "colordots", "assignments.json");
	}

	public IReadOnlyList<DotLabel> GetLabels()
	{
		lock (_gate)
		{
			EnsureLoaded();
			return ExportLabels();
		}
	}

	public bool NeedsInitialTagWrite()
	{
		lock (_gate)
		{
			EnsureLoaded();
			return _data.Migration != null && !_data.Migration.TagsWritten && _data.Items.Count > 0;
		}
	}

	public void MarkTagsWritten()
	{
		lock (_gate)
		{
			EnsureLoaded();
			if (_data.Migration == null)
			{
				_data.Migration = new MigrationRecord();
			}
			if (_data.Migration.TagsWritten)
			{
				return;
			}
			_data.Migration.TagsWritten = true;
			Save();
		}
	}

	public IReadOnlyList<Guid> AssignedItemIds()
	{
		lock (_gate)
		{
			EnsureLoaded();
			List<Guid> ids = new List<Guid>(_data.Items.Count);
			foreach (string key in _data.Items.Keys)
			{
				if (Guid.TryParseExact(key, "N", out Guid itemId))
				{
					ids.Add(itemId);
				}
			}
			return ids;
		}
	}

	public IReadOnlyList<int> GetItemLabels(Guid itemId)
	{
		lock (_gate)
		{
			EnsureLoaded();
			return ReadItemLabels(itemId);
		}
	}

	public Dictionary<string, int[]> GetItemLabels(IReadOnlyCollection<Guid> itemIds)
	{
		Dictionary<string, int[]> dictionary = new Dictionary<string, int[]>(itemIds.Count, StringComparer.Ordinal);
		lock (_gate)
		{
			EnsureLoaded();
			foreach (Guid itemId in itemIds)
			{
				dictionary[Key(itemId)] = ReadItemLabels(itemId).ToArray();
			}
			return dictionary;
		}
	}

	public IReadOnlyList<int> ToggleLabel(Guid itemId, int labelId)
	{
		lock (_gate)
		{
			EnsureLoaded();
			string key = Key(itemId);
			if (labelId == 0)
			{
				if (_data.Items.Remove(key))
				{
					Save();
				}
				return System.Array.Empty<int>();
			}
			if (FindLabel(labelId) == null)
			{
				throw new ArgumentException("Unknown label.", "labelId");
			}
			List<int> list = _data.Items.TryGetValue(key, out List<int>? existing) ? new List<int>(existing) : new List<int>();
			if (!list.Remove(labelId))
			{
				list.Add(labelId);
			}
			list = OrderIds(list);
			if (list.Count == 0)
			{
				_data.Items.Remove(key);
			}
			else
			{
				_data.Items[key] = list;
			}
			Save();
			return list;
		}
	}

	public void SetItemLabels(Guid itemId, IReadOnlyList<int> labelIds)
	{
		lock (_gate)
		{
			EnsureLoaded();
			string key = Key(itemId);
			List<int> list = new List<int>();
			if (labelIds != null)
			{
				foreach (int labelId in labelIds)
				{
					if (labelId == 0 || list.Contains(labelId))
					{
						continue;
					}
					if (FindLabel(labelId) == null)
					{
						throw new ArgumentException("Unknown label.", "labelIds");
					}
					list.Add(labelId);
				}
			}
			list = OrderIds(list);
			if (list.Count == 0)
			{
				if (!_data.Items.Remove(key))
				{
					return;
				}
			}
			else
			{
				_data.Items[key] = list;
			}
			Save();
		}
	}

	public DotLabel AddLabel(string? name, string? color, bool forceTranscode = false, int? maxBitrateBps = null)
	{
		if (!DotPalette.TryNormalizeHex(color, out string hex))
		{
			throw new ArgumentException("Color must be a 6-digit hex value.", "color");
		}
		if (!DotPalette.TryNormalizeBitrate(maxBitrateBps, out var bps))
		{
			throw new ArgumentException("Bitrate must be a positive number of bits per second.", "maxBitrateBps");
		}
		lock (_gate)
		{
			EnsureLoaded();
			if (_data.Labels.Count >= 24)
			{
				throw new ArgumentException("Too many labels.", "name");
			}
			while (FindLabel(_data.NextId) != null)
			{
				_data.NextId++;
			}
			int nextId = _data.NextId;
			_data.NextId = nextId + 1;
			_data.Labels.Add(new LabelRecord
			{
				Id = nextId,
				Name = DotPalette.CleanLabel(name, "New label"),
				Color = hex,
				ForceTranscode = forceTranscode,
				MaxBitrateBps = bps
			});
			Save();
			return ToDot(FindLabel(nextId));
		}
	}

	public DotLabel UpdateLabel(int labelId, string? name, string? color, bool forceTranscode = false, int? maxBitrateBps = null)
	{
		if (!DotPalette.TryNormalizeBitrate(maxBitrateBps, out var bps))
		{
			throw new ArgumentException("Bitrate must be a positive number of bits per second.", "maxBitrateBps");
		}
		lock (_gate)
		{
			EnsureLoaded();
			LabelRecord labelRecord = FindLabel(labelId) ?? throw new ArgumentException("Unknown label.", "labelId");
			labelRecord.Name = DotPalette.CleanLabel(name, (labelId == 0) ? "White" : "New label");
			if (!DotPalette.TryNormalizeHex(color, out string hex))
			{
				throw new ArgumentException("Color must be a 6-digit hex value.", "color");
			}
			labelRecord.Color = hex;
			labelRecord.ForceTranscode = (labelId != 0) & forceTranscode;
			labelRecord.MaxBitrateBps = ((labelId == 0) ? ((int?)null) : bps);
			Save();
			return ToDot(labelRecord);
		}
	}

	public void Reorder(IReadOnlyList<int> orderedIds)
	{
		lock (_gate)
		{
			EnsureLoaded();
			List<int> current = _data.Labels.Select((LabelRecord label) => label.Id).ToList();
			if (orderedIds.Count != current.Count || orderedIds.Distinct().Count() != current.Count || orderedIds.Any((int id) => !current.Contains(id)))
			{
				throw new ArgumentException("Order must list each label once.", "orderedIds");
			}
			List<LabelRecord> list = new List<LabelRecord>(orderedIds.Count);
			foreach (int orderedId in orderedIds)
			{
				list.Add(FindLabel(orderedId));
			}
			_data.Labels = list;
			Save();
		}
	}

	public int CountItems(int labelId)
	{
		lock (_gate)
		{
			EnsureLoaded();
			return DistinctItems(labelId).Count;
		}
	}

	public int DeleteLabel(int labelId)
	{
		if (labelId == 0)
		{
			throw new ArgumentException("The white default cannot be deleted.", "labelId");
		}
		lock (_gate)
		{
			EnsureLoaded();
			if (FindLabel(labelId) == null)
			{
				throw new ArgumentException("Unknown label.", "labelId");
			}
			int count = DistinctItems(labelId).Count;
			_data.Labels.RemoveAll((LabelRecord label) => label.Id == labelId);
			foreach (string key in _data.Items.Keys.ToList())
			{
				List<int> labels = _data.Items[key];
				if (!labels.Remove(labelId))
				{
					continue;
				}
				if (labels.Count == 0)
				{
					_data.Items.Remove(key);
				}
			}
			Save();
			return count;
		}
	}

	public IReadOnlyList<string> ItemIds(int labelId)
	{
		lock (_gate)
		{
			EnsureLoaded();
			return DistinctItems(labelId);
		}
	}

	public IReadOnlyList<PlaybackRule> GetPlaybackRules(Guid itemId)
	{
		lock (_gate)
		{
			EnsureLoaded();
			if (itemId == Guid.Empty)
			{
				return System.Array.Empty<PlaybackRule>();
			}
			List<PlaybackRule> list = new List<PlaybackRule>();
			foreach (int labelId in ReadItemLabels(itemId))
			{
				LabelRecord? labelRecord = FindLabel(labelId);
				if (labelRecord == null || labelRecord.Id == 0)
				{
					continue;
				}
				PlaybackRule item = new PlaybackRule(PositiveBitrate(labelRecord.MaxBitrateBps), labelRecord.ForceTranscode);
				if (item.Applies)
				{
					list.Add(item);
				}
			}
			return list;
		}
	}

	private static int? PositiveBitrate(int? value)
	{
		if (!DotPalette.TryNormalizeBitrate(value, out var bps))
		{
			return null;
		}
		return bps;
	}

	public ItemTrace? GetTrace(Guid itemId)
	{
		lock (_gate)
		{
			EnsureLoaded();
			return ReadTrace(Key(itemId));
		}
	}

	public void RememberTrace(Guid itemId, string? path, string? name, long? size = null, long? durationTicks = null)
	{
		RememberTraces(new[] { (itemId, path, name, size, durationTicks) });
	}

	public void RememberTraces(IEnumerable<(Guid Id, string? Path, string? Name, long? Size, long? DurationTicks)> traces)
	{
		lock (_gate)
		{
			EnsureLoaded();
			bool flag = false;
			foreach (var trace in traces)
			{
				flag |= TryRemember(trace.Id, trace.Path, trace.Name, trace.Size, trace.DurationTicks);
			}
			if (flag)
			{
				Save();
			}
		}
	}

	public IReadOnlyList<string> RemoveItems(int labelId, IReadOnlyCollection<string> itemKeys)
	{
		if (labelId == 0)
		{
			throw new ArgumentException("The white default has no assignments.", "labelId");
		}
		lock (_gate)
		{
			EnsureLoaded();
			if (FindLabel(labelId) == null)
			{
				throw new ArgumentException("Unknown label.", "labelId");
			}
			HashSet<string> wanted = new HashSet<string>(StringComparer.Ordinal);
			foreach (string itemKey in itemKeys)
			{
				string text = NormalizeGuidKey(itemKey);
				if (text.Length > 0)
				{
					wanted.Add(text);
				}
			}
			List<string> removed = new List<string>();
			foreach (string item in wanted)
			{
				if (!_data.Items.TryGetValue(item, out List<int>? labels) || !labels.Remove(labelId))
				{
					continue;
				}
				removed.Add(item);
				if (labels.Count == 0)
				{
					_data.Items.Remove(item);
				}
			}
			foreach (string item2 in removed)
			{
				if (!Referenced(item2))
				{
					_data.Traces.Remove(item2);
				}
			}
			if (removed.Count > 0)
			{
				Save();
			}
			return removed;
		}
	}

	public (int Moved, int Blocked) MoveItem(int labelId, Guid oldItemId, Guid newItemId)
	{
		if (labelId == 0)
		{
			throw new ArgumentException("The white default has no assignments.", "labelId");
		}
		if (oldItemId == Guid.Empty || newItemId == Guid.Empty || oldItemId == newItemId)
		{
			throw new ArgumentException("The item ids must be two different items.", "newItemId");
		}
		lock (_gate)
		{
			EnsureLoaded();
			if (FindLabel(labelId) == null)
			{
				throw new ArgumentException("Unknown label.", "labelId");
			}
			string key = Key(oldItemId);
			string key2 = Key(newItemId);
			if (!_data.Items.TryGetValue(key, out List<int>? oldLabels) || !oldLabels.Remove(labelId))
			{
				return (Moved: 0, Blocked: 0);
			}
			if (oldLabels.Count == 0)
			{
				_data.Items.Remove(key);
			}
			List<int> nextLabels = _data.Items.TryGetValue(key2, out List<int>? existing) ? new List<int>(existing) : new List<int>();
			if (!nextLabels.Contains(labelId))
			{
				nextLabels.Add(labelId);
			}
			_data.Items[key2] = OrderIds(nextLabels);
			if (_data.Traces.TryGetValue(key, out TraceRecord? value3) && !_data.Traces.ContainsKey(key2))
			{
				_data.Traces[key2] = new TraceRecord
				{
					Path = value3.Path,
					Name = value3.Name,
					FileName = value3.FileName,
					Size = value3.Size,
					Duration = value3.Duration
				};
			}
			if (!Referenced(key))
			{
				_data.Traces.Remove(key);
			}
			Save();
			return (Moved: 1, Blocked: 0);
		}
	}

	private IReadOnlyList<int> ReadItemLabels(Guid itemId)
	{
		if (itemId == Guid.Empty || !_data.Items.TryGetValue(Key(itemId), out List<int>? list))
		{
			return System.Array.Empty<int>();
		}
		return OrderIds(list);
	}

	private List<int> OrderIds(IEnumerable<int> ids)
	{
		HashSet<int> set = new HashSet<int>(ids);
		List<int> list = new List<int>();
		foreach (LabelRecord label in _data.Labels)
		{
			if (label.Id != 0 && set.Contains(label.Id))
			{
				list.Add(label.Id);
			}
		}
		return list;
	}

	private List<string> DistinctItems(int labelId)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		if (labelId == 0)
		{
			return new List<string>();
		}
		foreach (KeyValuePair<string, List<int>> item in _data.Items)
		{
			if (item.Value.Contains(labelId))
			{
				hashSet.Add(item.Key);
			}
		}
		return hashSet.Order(StringComparer.Ordinal).ToList();
	}

	private ItemTrace? ReadTrace(string key)
	{
		if (!_data.Traces.TryGetValue(key, out TraceRecord value) || string.IsNullOrWhiteSpace(value.Path))
		{
			return null;
		}
		string fileName = (string.IsNullOrWhiteSpace(value.FileName) ? TagRelink.FileName(value.Path) : value.FileName);
		return new ItemTrace(value.Path, value.Name)
		{
			FileName = fileName,
			Size = Positive(value.Size),
			Duration = Positive(value.Duration)
		};
	}

	private bool TryRemember(Guid itemId, string? path, string? name, long? size, long? durationTicks)
	{
		string text = (string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/').TrimEnd('/'));
		string text2 = (string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim());
		if (text.Length == 0)
		{
			return false;
		}
		string text3 = TagRelink.FileName(text) ?? string.Empty;
		long? num = Positive(size);
		long? num2 = Positive(durationTicks);
		string key = Key(itemId);
		if (!_data.Traces.TryGetValue(key, out TraceRecord trace))
		{
			_data.Traces[key] = new TraceRecord
			{
				Path = text,
				Name = text2,
				FileName = text3,
				Size = num,
				Duration = num2
			};
			return true;
		}
		return (byte)((int)(0u | (Assign(trace.Path, text, (string value) =>
		{
			trace.Path = value;
		}) ? 1u : 0u) | (Assign(trace.Name, text2, (string value) =>
		{
			trace.Name = value;
		}) ? 1u : 0u) | (Assign(trace.FileName, text3, (string value) =>
		{
			trace.FileName = value;
		}) ? 1u : 0u) | (KeepOrReplace(trace.Size, num, (long? value) =>
		{
			trace.Size = value;
		}) ? 1u : 0u)) | (KeepOrReplace(trace.Duration, num2, (long? value) =>
		{
			trace.Duration = value;
		}) ? 1 : 0)) != 0;
	}

	private static long? Positive(long? value)
	{
		if (!value.HasValue || value.GetValueOrDefault() <= 0)
		{
			return null;
		}
		return value;
	}

	private static bool KeepOrReplace(long? current, long? next, Action<long?> write)
	{
		if (next.HasValue)
		{
			if (current == next)
			{
				return false;
			}
			write(next);
			return true;
		}
		if (!current.HasValue || ((current.GetValueOrDefault() > 0) ? true : false))
		{
			return false;
		}
		write(null);
		return true;
	}

	private static bool Assign(string current, string next, Action<string> write)
	{
		if (string.Equals(current, next, StringComparison.Ordinal))
		{
			return false;
		}
		write(next);
		return true;
	}

	private bool Referenced(string itemKey)
	{
		return _data.Items.TryGetValue(itemKey, out List<int>? labels) && labels.Count > 0;
	}

	private LabelRecord? FindLabel(int id)
	{
		return _data.Labels.Find((LabelRecord label) => label.Id == id);
	}

	private List<DotLabel> ExportLabels()
	{
		return _data.Labels.Select(ToDot).ToList();
	}

	private static DotLabel ToDot(LabelRecord label)
	{
		bool flag = label.Id == 0;
		int? bps;
		return new DotLabel(label.Id, label.Name, label.Color, flag)
		{
			ForceTranscode = (!flag && label.ForceTranscode),
			MaxBitrateBps = (flag ? ((int?)null) : (DotPalette.TryNormalizeBitrate(label.MaxBitrateBps, out bps) ? bps : ((int?)null)))
		};
	}

	private void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}
		_loaded = true;
		if (!File.Exists(_path))
		{
			_data = NewStore();
			ApplyConfiguredNames(_data);
			return;
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(_path));
			JsonElement rootElement = jsonDocument.RootElement;
			int version = ReadInt(rootElement, "version", "Version");
			if (version >= 2 && HasProperty(rootElement, "labels", "Labels"))
			{
				_data = ReadCurrent(rootElement);
				NormalizeCurrent();
				if (version >= 3 && HasProperty(rootElement, "items", "Items"))
				{
					return;
				}
				MigrateFromUsers(version);
				Save();
				_logger?.LogInformation("Jellyfin Color Tags migrated {Path} to shared multi-label assignments. Preferred user {User}.", _path, _data.Migration?.PreferredUser);
				return;
			}
			_data = ReadLegacy(rootElement);
			MigrateFromUsers(version);
			Save();
			_logger?.LogInformation("Jellyfin Color Tags migrated {Path} to shared multi-label assignments.", _path);
		}
		catch (Exception ex) when (((ex is JsonException || ex is IOException || ex is FormatException) ? 1 : 0) != 0)
		{
			_logger?.LogError(ex, "Jellyfin Color Tags could not read {Path}. Starting with an empty assignment file.", _path);
			Quarantine();
			_data = NewStore();
		}
	}

	private static StoreFile ReadCurrent(JsonElement root)
	{
		int version = ReadInt(root, "version", "Version");
		StoreFile storeFile = new StoreFile
		{
			Version = (version >= 3) ? version : 2,
			NextId = Math.Max(1, ReadInt(root, "nextId", "NextId"))
		};
		foreach (JsonElement item in Array(root, "labels", "Labels"))
		{
			int id = ReadInt(item, "id", "Id");
			string value = ReadString(item, "name", "Name");
			if (!DotPalette.TryNormalizeHex(ReadString(item, "color", "Color"), out string hex))
			{
				hex = ((id == 0) ? "#f4f4f5" : "#888888");
			}
			if (!storeFile.Labels.Exists((LabelRecord label) => label.Id == id))
			{
				int? maxBitrateBps = null;
				if (id != 0 && DotPalette.TryNormalizeBitrate(ReadNullableInt(item, "maxBitrateBps", "MaxBitrateBps"), out var bps))
				{
					maxBitrateBps = bps;
				}
				storeFile.Labels.Add(new LabelRecord
				{
					Id = id,
					Name = DotPalette.CleanLabel(value, (id == 0) ? "White" : "New label"),
					Color = hex,
					ForceTranscode = (id != 0 && ReadBool(item, "forceTranscode", "ForceTranscode")),
					MaxBitrateBps = maxBitrateBps
				});
			}
		}
		ReadUsers(root, storeFile, legacyNames: false);
		ReadItems(root, storeFile);
		ReadTraces(root, storeFile);
		ReadMigration(root, storeFile);
		return storeFile;
	}

	private static void ReadItems(JsonElement root, StoreFile store)
	{
		if (!TryGet(root, "items", "Items", out JsonElement value) || value.ValueKind != JsonValueKind.Object)
		{
			return;
		}
		foreach (JsonProperty item in value.EnumerateObject())
		{
			string key = NormalizeGuidKey(item.Name);
			if (key.Length == 0)
			{
				continue;
			}
			List<int> ids = new List<int>();
			if (item.Value.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement element in item.Value.EnumerateArray())
				{
					if (TryReadAssignment(element, legacyNames: false, out int labelId) && labelId != 0 && store.Labels.Exists((LabelRecord label) => label.Id == labelId) && !ids.Contains(labelId))
					{
						ids.Add(labelId);
					}
				}
			}
			else if (TryReadAssignment(item.Value, legacyNames: false, out int one) && one != 0 && store.Labels.Exists((LabelRecord label) => label.Id == one))
			{
				ids.Add(one);
			}
			if (ids.Count == 0)
			{
				continue;
			}
			ids.Sort((int left, int right) => store.Labels.FindIndex((LabelRecord label) => label.Id == left).CompareTo(store.Labels.FindIndex((LabelRecord label) => label.Id == right)));
			store.Items[key] = ids;
		}
	}

	private static void ReadMigration(JsonElement root, StoreFile store)
	{
		if (!TryGet(root, "migration", "Migration", out JsonElement value) || value.ValueKind != JsonValueKind.Object)
		{
			return;
		}
		string snapshot = ReadString(value, "usersSnapshot", "UsersSnapshot");
		store.Migration = new MigrationRecord
		{
			FromVersion = ReadInt(value, "fromVersion", "FromVersion"),
			PreferredUser = ReadString(value, "preferredUser", "PreferredUser"),
			UsersSnapshot = string.IsNullOrWhiteSpace(snapshot) ? "frozen" : snapshot,
			TagsWritten = ReadBool(value, "tagsWritten", "TagsWritten")
		};
	}

	private void MigrateFromUsers(int fromVersion)
	{
		List<int> order = _data.Labels.Select((LabelRecord label) => label.Id).ToList();
		(Dictionary<string, List<int>> items, string? preferred) migrated = MigrateAssignments(_data.Users, _preferredUserKeys, order);
		_data.Items = migrated.items;
		_data.Version = 3;
		_data.Migration = new MigrationRecord
		{
			FromVersion = fromVersion,
			PreferredUser = migrated.preferred ?? string.Empty,
			UsersSnapshot = "frozen"
		};
		PreserveRollbackCopy();
	}

	private void PreserveRollbackCopy()
	{
		try
		{
			string? directoryName = Path.GetDirectoryName(_path);
			if (string.IsNullOrEmpty(directoryName) || !File.Exists(_path))
			{
				return;
			}
			string backup = Path.Combine(directoryName, "assignments.v2.json");
			if (!File.Exists(backup))
			{
				File.Copy(_path, backup);
			}
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
		{
			_logger?.LogWarning(ex, "Jellyfin Color Tags could not keep a rollback copy of {Path}.", _path);
		}
	}

	internal static (Dictionary<string, List<int>> Items, string? PreferredUser) MigrateAssignments(IReadOnlyDictionary<string, Dictionary<string, int>> users, IReadOnlyList<string>? preferredKeys, IReadOnlyList<int> labelOrder)
	{
		Dictionary<string, List<int>> items = new Dictionary<string, List<int>>(StringComparer.Ordinal);
		if (users == null || users.Count == 0)
		{
			return (items, null);
		}
		string? preferred = ChoosePreferredUser(users, preferredKeys);
		HashSet<int> known = new HashSet<int>(labelOrder);
		known.Remove(0);
		SortedSet<string> itemIds = new SortedSet<string>(StringComparer.Ordinal);
		foreach (Dictionary<string, int> map in users.Values)
		{
			if (map == null)
			{
				continue;
			}
			foreach (string id in map.Keys)
			{
				itemIds.Add(id);
			}
		}
		foreach (string itemId in itemIds)
		{
			List<int> chosen = new List<int>();
			if (preferred != null && users.TryGetValue(preferred, out Dictionary<string, int>? preferredMap) && preferredMap != null && preferredMap.TryGetValue(itemId, out int preferredLabel) && known.Contains(preferredLabel))
			{
				chosen.Add(preferredLabel);
			}
			else
			{
				foreach (KeyValuePair<string, Dictionary<string, int>> pair in users.OrderBy((KeyValuePair<string, Dictionary<string, int>> pair) => pair.Key, StringComparer.Ordinal))
				{
					if (pair.Key == preferred || pair.Value == null)
					{
						continue;
					}
					if (pair.Value.TryGetValue(itemId, out int label) && known.Contains(label) && !chosen.Contains(label))
					{
						chosen.Add(label);
					}
				}
			}
			if (chosen.Count == 0)
			{
				continue;
			}
			List<int> ordered = new List<int>();
			foreach (int id in labelOrder)
			{
				if (chosen.Contains(id))
				{
					ordered.Add(id);
				}
			}
			items[itemId] = ordered;
		}
		return (items, preferred);
	}

	internal static string? ChoosePreferredUser(IReadOnlyDictionary<string, Dictionary<string, int>> users, IReadOnlyList<string>? preferredKeys)
	{
		if (users == null || users.Count == 0)
		{
			return null;
		}
		if (preferredKeys != null)
		{
			foreach (string key in preferredKeys)
			{
				string normalized = NormalizeGuidKey(key ?? string.Empty);
				if (normalized.Length > 0 && users.ContainsKey(normalized))
				{
					return normalized;
				}
			}
		}
		if (users.ContainsKey(FallbackAdminKey))
		{
			return FallbackAdminKey;
		}
		if (users.Count == 1)
		{
			foreach (string only in users.Keys)
			{
				return only;
			}
		}
		string? best = null;
		int bestCount = -1;
		foreach (KeyValuePair<string, Dictionary<string, int>> pair in users.OrderBy((KeyValuePair<string, Dictionary<string, int>> pair) => pair.Key, StringComparer.Ordinal))
		{
			int count = pair.Value?.Count ?? 0;
			if (count > bestCount)
			{
				bestCount = count;
				best = pair.Key;
			}
		}
		return best;
	}

	private static void ReadTraces(JsonElement root, StoreFile store)
	{
		if (!TryGet(root, "traces", "Traces", out var value) || value.ValueKind != JsonValueKind.Object)
		{
			return;
		}
		foreach (JsonProperty item in value.EnumerateObject())
		{
			string text = NormalizeGuidKey(item.Name);
			if (text.Length == 0 || item.Value.ValueKind != JsonValueKind.Object)
			{
				continue;
			}
			string text2 = ReadString(item.Value, "path", "Path").Trim().Replace('\\', '/').TrimEnd('/');
			if (text2.Length != 0)
			{
				string text3 = ReadString(item.Value, "fileName", "FileName").Trim();
				if (text3.Length == 0)
				{
					text3 = TagRelink.FileName(text2) ?? string.Empty;
				}
				store.Traces[text] = new TraceRecord
				{
					Path = text2,
					Name = ReadString(item.Value, "name", "Name").Trim(),
					FileName = text3,
					Size = ReadLong(item.Value, "size", "Size"),
					Duration = ReadLong(item.Value, "duration", "Duration")
				};
			}
		}
	}

	private static StoreFile ReadLegacy(JsonElement root)
	{
		StoreFile storeFile = NewStore();
		ApplyConfiguredNames(storeFile);
		ReadUsers(root, storeFile, legacyNames: true);
		return storeFile;
	}

	private static void ReadUsers(JsonElement root, StoreFile store, bool legacyNames)
	{
		if (!TryGet(root, "users", "Users", out var value) || value.ValueKind != JsonValueKind.Object)
		{
			return;
		}
		foreach (JsonProperty item in value.EnumerateObject())
		{
			if (item.Value.ValueKind != JsonValueKind.Object)
			{
				continue;
			}
			string text = NormalizeGuidKey(item.Name);
			if (text.Length == 0)
			{
				continue;
			}
			Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (JsonProperty item2 in item.Value.EnumerateObject())
			{
				string text2 = NormalizeGuidKey(item2.Name);
				if (text2.Length != 0 && TryReadAssignment(item2.Value, legacyNames, out var labelId) && labelId != 0 && (legacyNames || store.Labels.Exists((LabelRecord label) => label.Id == labelId)))
				{
					dictionary[text2] = labelId;
				}
			}
			if (dictionary.Count > 0)
			{
				store.Users[text] = dictionary;
			}
		}
	}

	private static bool TryReadAssignment(JsonElement value, bool legacyNames, out int labelId)
	{
		labelId = 0;
		if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out labelId))
		{
			return labelId >= 0;
		}
		if (value.ValueKind == JsonValueKind.String)
		{
			string text = value.GetString();
			if (legacyNames && DotPalette.TryMapLegacy(text, out labelId))
			{
				return true;
			}
			if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out labelId))
			{
				return labelId >= 0;
			}
			return false;
		}
		return false;
	}

	private void NormalizeCurrent()
	{
		if (FindLabel(0) == null)
		{
			_data.Labels.Insert(0, new LabelRecord
			{
				Id = 0,
				Name = "White",
				Color = "#f4f4f5"
			});
		}
		int num = ((_data.Labels.Count != 0) ? _data.Labels.Max((LabelRecord label) => label.Id) : 0);
		if (_data.NextId <= num)
		{
			_data.NextId = num + 1;
		}
	}

	private static void ApplyConfiguredNames(StoreFile store)
	{
		if (AppDomain.CurrentDomain.GetAssemblies().Any((Assembly assembly) => assembly.GetName().Name == "MediaBrowser.Model"))
		{
			ApplyConfiguredNamesFromPlugin(store);
		}
	}

	private static void ApplyConfiguredNamesFromPlugin(StoreFile store)
	{
		PluginConfiguration pluginConfiguration = ((BasePlugin<PluginConfiguration>)(object)Plugin.Instance)?.Configuration;
		Rename(store, 0, pluginConfiguration?.WhiteLabel, "White");
		Rename(store, 1, pluginConfiguration?.RedLabel, "Red");
		Rename(store, 2, pluginConfiguration?.GreenLabel, "Green");
		Rename(store, 3, pluginConfiguration?.BlueLabel, "Blue");
	}

	private static void Rename(StoreFile store, int id, string? configured, string fallback)
	{
		LabelRecord labelRecord = store.Labels.Find((LabelRecord item) => item.Id == id);
		if (labelRecord != null)
		{
			string name = DotPalette.CleanLabel(configured, fallback);
			if (!DotPalette.IsStockName(id, name) && DotPalette.IsStockName(id, labelRecord.Name))
			{
				labelRecord.Name = name;
			}
		}
	}

	private static StoreFile NewStore()
	{
		StoreFile storeFile = new StoreFile
		{
			Version = 3,
			NextId = 4
		};
		int num = 4;
		List<LabelRecord> list = new List<LabelRecord>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<LabelRecord> span = CollectionsMarshal.AsSpan(list);
		span[0] = new LabelRecord
		{
			Id = 0,
			Name = "White",
			Color = "#f4f4f5"
		};
		span[1] = new LabelRecord
		{
			Id = 1,
			Name = "Red",
			Color = "#e23b3b"
		};
		span[2] = new LabelRecord
		{
			Id = 2,
			Name = "Green",
			Color = "#3cba54"
		};
		span[3] = new LabelRecord
		{
			Id = 3,
			Name = "Blue",
			Color = "#3b82f6"
		};
		storeFile.Labels = list;
		return storeFile;
	}

	private void Quarantine()
	{
		try
		{
			string directoryName = Path.GetDirectoryName(_path);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
				string destFileName = Path.Combine(directoryName, "assignments.bad-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".json");
				File.Move(_path, destFileName, overwrite: false);
			}
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			_logger?.LogError(ex, "Jellyfin Color Tags could not quarantine a corrupt assignment file.");
		}
	}

	private void Save()
	{
		string directoryName = Path.GetDirectoryName(_path);
		if (string.IsNullOrEmpty(directoryName))
		{
			throw new InvalidOperationException("Jellyfin Color Tags store path has no directory.");
		}
		Directory.CreateDirectory(directoryName);
		string text = _path + ".tmp";
		string contents = JsonSerializer.Serialize(_data, JsonOptions);
		File.WriteAllText(text, contents);
		File.Move(text, _path, overwrite: true);
	}

	private static string Key(Guid id)
	{
		return id.ToString("N");
	}

	private static string NormalizeGuidKey(string raw)
	{
		string text = raw.Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
		if (text.Length != 32 || !text.All((char c) =>
		{
			switch (c)
			{
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
			case 'a':
			case 'b':
			case 'c':
			case 'd':
			case 'e':
			case 'f':
				return true;
			default:
				return false;
			}
		}))
		{
			return string.Empty;
		}
		return text;
	}

	private static bool HasProperty(JsonElement element, string camel, string pascal)
	{
		if (element.ValueKind == JsonValueKind.Object)
		{
			if (!element.TryGetProperty(camel, out var value))
			{
				return element.TryGetProperty(pascal, out value);
			}
			return true;
		}
		return false;
	}

	private static bool TryGet(JsonElement element, string camel, string pascal, out JsonElement value)
	{
		if (element.ValueKind == JsonValueKind.Object && (element.TryGetProperty(camel, out value) || element.TryGetProperty(pascal, out value)))
		{
			return true;
		}
		value = default;
		return false;
	}

	private static bool ReadBool(JsonElement element, string camel, string pascal)
	{
		if (!TryGet(element, camel, pascal, out var value))
		{
			return false;
		}
		if (value.ValueKind == JsonValueKind.True)
		{
			return true;
		}
		if (value.ValueKind == JsonValueKind.False || value.ValueKind == JsonValueKind.Null)
		{
			return false;
		}
		if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var value2))
		{
			return value2 != 0;
		}
		if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var result))
		{
			return result;
		}
		return false;
	}

	private static int? ReadNullableInt(JsonElement element, string camel, string pascal)
	{
		if (!TryGet(element, camel, pascal, out var value) || value.ValueKind == JsonValueKind.Null)
		{
			return null;
		}
		if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var value2))
		{
			return value2;
		}
		if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value2))
		{
			return value2;
		}
		return null;
	}

	private static int ReadInt(JsonElement element, string camel, string pascal)
	{
		if (!TryGet(element, camel, pascal, out var value))
		{
			return 0;
		}
		if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var value2))
		{
			return value2;
		}
		if (value.ValueKind != JsonValueKind.String || !int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value2))
		{
			return 0;
		}
		return value2;
	}

	private static long? ReadLong(JsonElement element, string camel, string pascal)
	{
		if (!TryGet(element, camel, pascal, out var value))
		{
			return null;
		}
		if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var value2))
		{
			return value2;
		}
		if (value.ValueKind != JsonValueKind.String || !long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value2))
		{
			return null;
		}
		return value2;
	}

	private static string ReadString(JsonElement element, string camel, string pascal)
	{
		if (!TryGet(element, camel, pascal, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return string.Empty;
		}
		return value.GetString() ?? string.Empty;
	}

	private static IEnumerable<JsonElement> Array(JsonElement element, string camel, string pascal)
	{
		if (TryGet(element, camel, pascal, out var value) && value.ValueKind == JsonValueKind.Array)
		{
			return value.EnumerateArray().ToArray();
		}
		return System.Array.Empty<JsonElement>();
	}
}
