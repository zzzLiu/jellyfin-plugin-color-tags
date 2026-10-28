using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ColorDots.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ColorDots.Controllers;

[ApiController]
[Authorize]
[Route("ColorDots")]
public sealed class ColorDotsController : ControllerBase
{
	private const int MaxIds = 300;

	private static readonly Lazy<string> ClientScript = new Lazy<string>(ReadClientScript);

	private readonly ColorStore _store;

	private readonly ILibraryManager _library;

	private readonly OfficialTagSync _tags;

	private readonly ILogger<ColorDotsController> _logger;

	public ColorDotsController(ColorStore store, ILibraryManager library, OfficialTagSync tags, ILogger<ColorDotsController> logger)
	{
		_store = store;
		_library = library;
		_tags = tags;
		_logger = logger;
	}

	[AllowAnonymous]
	[HttpGet("client.js")]
	public ContentResult ClientJs()
	{
		Response.Headers.CacheControl = "no-cache, must-revalidate";
		return Content(ClientScript.Value, "application/javascript; charset=utf-8");
	}

	[HttpGet("labels")]
	public ActionResult<LabelsResponse> Labels()
	{
		if (!TryGetUserId(out var _))
		{
			return Unauthorized();
		}
		Response.Headers.CacheControl = "no-store";
		return Ok(new LabelsResponse((from label in _store.GetLabels()
			select LabelDto.From(label, _store.CountItems(label.Id))).ToList()));
	}

	[Authorize(Policy = "RequiresElevation")]
	[HttpPost("labels")]
	public ActionResult<LabelDto> AddLabel([FromBody] LabelWriteRequest? request)
	{
		try
		{
			return Ok(LabelDto.From(_store.AddLabel(request?.Name, request?.Color, request?.ForceTranscode ?? false, request?.MaxBitrateBps)));
		}
		catch (ArgumentException)
		{
			return BadRequest();
		}
	}

	[Authorize(Policy = "RequiresElevation")]
	[HttpPut("labels/{id:int}")]
	public async Task<ActionResult<LabelDto>> UpdateLabel(int id, [FromBody] LabelWriteRequest? request, CancellationToken cancellationToken)
	{
		DotLabel? before = _store.GetLabels().FirstOrDefault((DotLabel label) => label.Id == id);
		IReadOnlyList<string> itemIds = (id == 0) ? Array.Empty<string>() : _store.ItemIds(id);
		DotLabel updated;
		try
		{
			updated = _store.UpdateLabel(id, request?.Name, request?.Color, request?.ForceTranscode ?? false, request?.MaxBitrateBps);
		}
		catch (ArgumentException)
		{
			return BadRequest();
		}
		if (before != null && id != 0 && !string.Equals(before.Name, updated.Name, StringComparison.Ordinal))
		{
			await _tags.SyncManyAsync(ParseItemIds(itemIds), new string[1] { before.Name }, cancellationToken).ConfigureAwait(false);
		}
		return Ok(LabelDto.From(updated));
	}

	[Authorize(Policy = "RequiresElevation")]
	[HttpPut("labels/order")]
	public ActionResult<LabelsResponse> Reorder([FromBody] OrderRequest? request)
	{
		if (request?.Ids == null)
		{
			return BadRequest();
		}
		try
		{
			_store.Reorder(request.Ids);
		}
		catch (ArgumentException)
		{
			return BadRequest();
		}
		return Ok(new LabelsResponse(_store.GetLabels().Select(LabelDto.From).ToList()));
	}

	[Authorize(Policy = "RequiresElevation")]
	[HttpGet("labels/{id:int}/usage")]
	public ActionResult<UsageResponse> Usage(int id)
	{
		if (_store.GetLabels().All((DotLabel label) => label.Id != id))
		{
			return NotFound();
		}
		return Ok(new UsageResponse(_store.CountItems(id)));
	}

	[Authorize(Policy = "RequiresElevation")]
	[HttpDelete("labels/{id:int}")]
	public async Task<ActionResult<ClearedResponse>> DeleteLabel(int id, CancellationToken cancellationToken)
	{
		DotLabel? before = _store.GetLabels().FirstOrDefault((DotLabel label) => label.Id == id);
		IReadOnlyList<string> itemIds = (before == null || id == 0) ? Array.Empty<string>() : _store.ItemIds(id);
		int cleared;
		try
		{
			cleared = _store.DeleteLabel(id);
		}
		catch (ArgumentException)
		{
			return BadRequest();
		}
		if (before != null && id != 0)
		{
			await _tags.SyncManyAsync(ParseItemIds(itemIds), new string[1] { before.Name }, cancellationToken).ConfigureAwait(false);
		}
		return Ok(new ClearedResponse(cleared));
	}

	[Authorize(Policy = "RequiresElevation")]
	[HttpDelete("labels/{id:int}/items/{itemId:guid}")]
	public async Task<ActionResult<ClearedResponse>> RemoveAssignment(int id, Guid itemId, CancellationToken cancellationToken)
	{
		if (!KnownLabel(id, out ActionResult? error))
		{
			return error!;
		}
		if (itemId == Guid.Empty)
		{
			return BadRequest();
		}
		try
		{
			IReadOnlyList<string> removed = _store.RemoveItems(id, new[] { itemId.ToString("N") });
			await _tags.SyncManyAsync(ParseItemIds(removed), Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
			return Ok(new ClearedResponse(removed.Count));
		}
		catch (ArgumentException)
		{
			return BadRequest();
		}
	}

	[Authorize(Policy = "RequiresElevation")]
	[HttpDelete("labels/{id:int}/items")]
	public async Task<ActionResult<ClearedResponse>> RemoveAllAssignments(int id, CancellationToken cancellationToken)
	{
		if (!KnownLabel(id, out ActionResult? error))
		{
			return error!;
		}
		try
		{
			IReadOnlyList<string> removed = _store.RemoveItems(id, _store.ItemIds(id));
			await _tags.SyncManyAsync(ParseItemIds(removed), Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
			return Ok(new ClearedResponse(removed.Count));
		}
		catch (ArgumentException)
		{
			return BadRequest();
		}
	}

	[Authorize(Policy = "RequiresElevation")]
	[HttpPost("labels/{id:int}/invalid")]
	public async Task<ActionResult<ClearInvalidResponse>> ClearInvalid(int id, CancellationToken cancellationToken)
	{
		if (!KnownLabel(id, out ActionResult? error))
		{
			return error!;
		}
		List<string> list = new List<string>();
		foreach (string item in _store.ItemIds(id))
		{
			if (!Guid.TryParseExact(item, "N", out Guid result) || !IsLive(TryGetItem(result)))
			{
				list.Add(item);
			}
		}
		try
		{
			IReadOnlyList<string> removed = _store.RemoveItems(id, list);
			await _tags.SyncManyAsync(ParseItemIds(removed), Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
			return Ok(new ClearInvalidResponse(removed.Count));
		}
		catch (ArgumentException)
		{
			return BadRequest();
		}
	}

	[Authorize(Policy = "RequiresElevation")]
	[HttpPost("labels/{id:int}/relink")]
	public async Task<ActionResult<RelinkResponse>> Relink(int id)
	{
		if (!KnownLabel(id, out ActionResult error))
		{
			return error;
		}
		int num = 0;
		int num2 = 0;
		IReadOnlyList<BaseItem> library = MediaInLibrary();
		foreach (string item in _store.ItemIds(id))
		{
			if (!Guid.TryParseExact(item, "N", out var result))
			{
				num2++;
				continue;
			}
			BaseItem val = TryGetItem(result);
			if (IsLive(val))
			{
				continue;
			}
			ItemTrace trace = _store.GetTrace(result);
			string text = trace?.Path;
			if (string.IsNullOrWhiteSpace(text))
			{
				text = ((val != null) ? val.Path : null);
			}
			if (string.IsNullOrWhiteSpace(text))
			{
				num2++;
				continue;
			}
			(BaseItem, bool) tuple = MatchReplacement(result, text, library, trace?.Size, trace?.Duration);
			if (tuple.Item2 || tuple.Item1 == null)
			{
				num2++;
				continue;
			}
			try
			{
				if (_store.MoveItem(id, result, tuple.Item1.Id).Moved == 0)
				{
					num2++;
					continue;
				}
				num++;
				if (!string.IsNullOrWhiteSpace(tuple.Item1.Path))
				{
					(long?, long?) tuple2 = MediaFacts(tuple.Item1);
					_store.RememberTrace(tuple.Item1.Id, tuple.Item1.Path, tuple.Item1.Name, tuple2.Item1, tuple2.Item2);
				}
				await _tags.SyncManyAsync(new Guid[2] { result, tuple.Item1.Id }, Array.Empty<string>(), CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentException)
			{
				num2++;
			}
		}
		return Ok(new RelinkResponse(num, num2));
	}

	[Authorize(Policy = "RequiresElevation")]
	[HttpGet("Report")]
	public ActionResult<ReportResponse> Report()
	{
		Response.Headers.CacheControl = "no-store";
		List<ReportGroup> list = new List<ReportGroup>();
		List<(Guid, string, string, long?, long?)> list2 = new List<(Guid, string, string, long?, long?)>();
		int num = 0;
		foreach (DotLabel label in _store.GetLabels())
		{
			num++;
			if (label.Id == 0)
			{
				continue;
			}
			List<ReportItem> list3 = new List<ReportItem>();
			foreach (string item in _store.ItemIds(label.Id))
			{
				if (Guid.TryParseExact(item, "N", out var result))
				{
					BaseItem val = TryGetItem(result);
					if (val != null && !string.IsNullOrWhiteSpace(val.Path))
					{
						(long?, long?) tuple = MediaFacts(val);
						list2.Add((val.Id, val.Path, val.Name, tuple.Item1, tuple.Item2));
					}
					list3.Add(Describe(result));
				}
			}
			list3.Sort((ReportItem a, ReportItem b) =>
			{
				int num2 = string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
				return (num2 != 0) ? num2 : string.CompareOrdinal(a.Id, b.Id);
			});
			list.Add(new ReportGroup(label.Id, label.Name, label.Color, list3)
			{
				Number = num
			});
		}
		_store.RememberTraces(list2);
		return Ok(new ReportResponse(list));
	}

	[HttpPost("colors")]
	public ActionResult<ColorsResponse> Colors([FromBody] ColorsRequest? request)
	{
		Response.Headers.CacheControl = "no-store";
		if (!TryGetUserId(out var userId))
		{
			return Unauthorized();
		}
		if (request?.Ids == null || request.Ids.Count > 300)
		{
			return BadRequest();
		}
		List<Guid> list = new List<Guid>(request.Ids.Count);
		foreach (string id in request.Ids)
		{
			if (Guid.TryParse(id, out var result) && result != Guid.Empty)
			{
				list.Add(result);
			}
		}
		return Ok(new ColorsResponse(_store.GetItemLabels(list)));
	}

	[HttpPut("items/{itemId:guid}")]
	public async Task<ActionResult<SetLabelResponse>> SetLabel(Guid itemId, [FromBody] SetLabelRequest? request, CancellationToken cancellationToken)
	{
		if (!TryGetUserId(out Guid _))
		{
			return Unauthorized();
		}
		if (itemId == Guid.Empty || !(request?.Label).HasValue)
		{
			return BadRequest();
		}
		BaseItem? itemById;
		try
		{
			itemById = _library.GetItemById(itemId);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Jellyfin Color Tags could not look up item {ItemId}.", itemId);
			return StatusCode(500);
		}
		if (itemById == null)
		{
			return NotFound();
		}
		if (!IsSupported(itemById))
		{
			return BadRequest();
		}
		IReadOnlyList<int> before = _store.GetItemLabels(itemId);
		IReadOnlyList<int> after;
		try
		{
			after = _store.ToggleLabel(itemId, request.Label.Value);
			if (after.Count > 0)
			{
				(long?, long?) tuple = MediaFacts(itemById);
				_store.RememberTrace(itemById.Id, itemById.Path, itemById.Name, tuple.Item1, tuple.Item2);
			}
		}
		catch (ArgumentException)
		{
			return BadRequest();
		}
		try
		{
			await _tags.SyncAsync(itemId, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "Jellyfin Color Tags could not update tags for {ItemId}. The color change was reverted.", itemId);
			try
			{
				_store.SetItemLabels(itemId, before);
			}
			catch (Exception revertException)
			{
				_logger.LogError(revertException, "Jellyfin Color Tags could not revert labels for {ItemId}.", itemId);
			}
			return StatusCode(500);
		}
		return Ok(new SetLabelResponse(after.ToArray()));
	}

	private static IEnumerable<Guid> ParseItemIds(IEnumerable<string> keys)
	{
		foreach (string key in keys)
		{
			if (Guid.TryParseExact(key, "N", out Guid itemId))
			{
				yield return itemId;
			}
		}
	}

	private ReportItem Describe(Guid itemId)
	{
		BaseItem val = null;
		try
		{
			val = _library.GetItemById(itemId);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Jellyfin Color Tags could not name item {ItemId}.", itemId);
		}
		if (val == null)
		{
			ItemTrace trace = _store.GetTrace(itemId);
			string path = null;
			string fileName = null;
			string name = string.Empty;
			long? size = null;
			long? duration = null;
			if ((object)trace != null)
			{
				if (!string.IsNullOrWhiteSpace(trace.Path))
				{
					path = trace.Path;
					fileName = (string.IsNullOrWhiteSpace(trace.FileName) ? TagRelink.FileName(trace.Path) : trace.FileName);
				}
				if (!string.IsNullOrWhiteSpace(trace.Name))
				{
					name = trace.Name.Trim();
				}
				size = trace.Size;
				duration = trace.Duration;
			}
			return new ReportItem(itemId.ToString("D"), name, string.Empty, null, path, null, null)
			{
				Missing = true,
				FileName = fileName,
				Size = size,
				Duration = duration
			};
		}
		string name2 = ((object)val).GetType().Name;
		int? productionYear = val.ProductionYear;
		int? year = ((productionYear.HasValue && productionYear.GetValueOrDefault() > 0) ? val.ProductionYear : ((int?)null));
		string path2 = (string.IsNullOrWhiteSpace(val.Path) ? null : val.Path);
		(string, string) tuple = Locate(val);
		(long?, long?) tuple2 = MediaFacts(val);
		ReportItem reportItem = new ReportItem(itemId.ToString("D"), DisplayName(val, name2), name2, year, path2, tuple.Item1, tuple.Item2);
		(reportItem.Size, reportItem.Duration) = tuple2;
		return reportItem;
	}

	private (string? Library, string? Place) Locate(BaseItem item)
	{
		string text = null;
		string libraryRoot = null;
		try
		{
			foreach (Folder collectionFolder in _library.GetCollectionFolders(item))
			{
				string text2 = ((collectionFolder != null) ? ((BaseItem)collectionFolder).Name : null);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					text = text2.Trim();
					string text3 = ((collectionFolder != null) ? ((BaseItem)collectionFolder).Path : null);
					libraryRoot = (string.IsNullOrWhiteSpace(text3) ? null : text3);
					break;
				}
			}
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Jellyfin Color Tags could not read collection folders for {ItemId}.", item.Id);
		}
		string text4 = LibraryMatcher.DescribePlace(text, item.Path, libraryRoot);
		if (string.IsNullOrEmpty(text4) || text4 == text)
		{
			try
			{
				LibraryMatcher.LibraryHit? libraryHit = LibraryMatcher.Find(item.Path, VirtualLocations());
				if (libraryHit.HasValue)
				{
					if (text == null)
					{
						text = libraryHit.Value.Name;
					}
					string text5 = LibraryMatcher.DescribePlace(text, item.Path, libraryHit.Value.Root);
					if (!string.IsNullOrEmpty(text5))
					{
						text4 = text5;
					}
				}
			}
			catch (Exception exception2)
			{
				_logger.LogDebug(exception2, "Jellyfin Color Tags could not match a library for {ItemId}.", item.Id);
			}
		}
		return (Library: text, Place: text4);
	}

	private List<LibraryMatcher.LibraryLocation> VirtualLocations()
	{
		List<LibraryMatcher.LibraryLocation> list = new List<LibraryMatcher.LibraryLocation>();
		foreach (VirtualFolderInfo virtualFolder in _library.GetVirtualFolders())
		{
			if (!string.IsNullOrWhiteSpace(virtualFolder.Name))
			{
				list.Add(new LibraryMatcher.LibraryLocation(virtualFolder.Name, virtualFolder.Locations));
			}
		}
		return list;
	}

	private static string DisplayName(BaseItem item, string type)
	{
		if (type != "Episode")
		{
			if (!string.IsNullOrWhiteSpace(item.Name))
			{
				return item.Name;
			}
			return "Not in the library";
		}
		string text = ((object)item).GetType().GetProperty("SeriesName")?.GetValue(item) as string;
		string text2 = (string.IsNullOrWhiteSpace(text) ? item.Name : text.Trim());
		if (string.IsNullOrWhiteSpace(text2))
		{
			text2 = "Not in the library";
		}
		if (!item.ParentIndexNumber.HasValue && !item.IndexNumber.HasValue)
		{
			return text2;
		}
		IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
		DefaultInterpolatedStringHandler handler = new DefaultInterpolatedStringHandler(3, 3, invariantCulture);
		handler.AppendFormatted(text2);
		handler.AppendLiteral(" S");
		handler.AppendFormatted(item.ParentIndexNumber.GetValueOrDefault(), "D2");
		handler.AppendLiteral("E");
		handler.AppendFormatted(item.IndexNumber.GetValueOrDefault(), "D2");
		return string.Create(invariantCulture, ref handler);
	}

	private bool KnownLabel(int id, out ActionResult? error)
	{
		error = null;
		if (id == 0)
		{
			error = BadRequest();
			return false;
		}
		if (_store.GetLabels().All((DotLabel label) => label.Id != id))
		{
			error = NotFound();
			return false;
		}
		return true;
	}

	private BaseItem? TryGetItem(Guid itemId)
	{
		try
		{
			return _library.GetItemById(itemId);
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Jellyfin Color Tags could not look up item {ItemId}.", itemId);
			return null;
		}
	}

	private static bool IsLive(BaseItem? item)
	{
		if (item != null)
		{
			return TagRelink.Exists(item.Path);
		}
		return false;
	}

	private (BaseItem? Item, bool Ambiguous) MatchReplacement(Guid oldId, string hint, IReadOnlyList<BaseItem> library, long? size, long? durationTicks)
	{
		Dictionary<Guid, BaseItem> found = new Dictionary<Guid, BaseItem>();
		foreach (string item in TagRelink.CandidatePaths(hint, LibraryRoots()))
		{
			Consider(FindByPath(item));
			string directoryName = Path.GetDirectoryName(item);
			if (!string.IsNullOrWhiteSpace(directoryName))
			{
				Consider(FindByPath(directoryName));
			}
		}
		foreach (BaseItem item2 in library)
		{
			if (TagRelink.Same(hint, item2.Path))
			{
				Consider(item2);
			}
		}
		List<BaseItem> list = new List<BaseItem>();
		foreach (BaseItem value in found.Values)
		{
			if (TagRelink.Exists(value.Path))
			{
				list.Add(value);
			}
		}
		List<BaseItem> list2 = ((list.Count > 0) ? list : found.Values.ToList());
		if (list2.Count == 1)
		{
			return (Item: list2[0], Ambiguous: false);
		}
		return UniqueByFileName(oldId, hint, library, size, durationTicks);
		void Consider(BaseItem? item)
		{
			if (item != null && !(item.Id == Guid.Empty) && !(item.Id == oldId) && IsSupported(item) && !string.IsNullOrWhiteSpace(item.Path))
			{
				found[item.Id] = item;
			}
		}
	}

	private static (BaseItem? Item, bool Ambiguous) UniqueByFileName(Guid oldId, string hint, IReadOnlyList<BaseItem> library, long? size, long? durationTicks)
	{
		if (TagRelink.FileName(hint) == null)
		{
			return (Item: null, Ambiguous: false);
		}
		Dictionary<Guid, BaseItem> dictionary = new Dictionary<Guid, BaseItem>();
		foreach (BaseItem item in library)
		{
			if (!(item.Id == Guid.Empty) && !(item.Id == oldId) && IsSupported(item) && !string.IsNullOrWhiteSpace(item.Path) && TagRelink.SameFileName(hint, item.Path))
			{
				dictionary[item.Id] = item;
			}
		}
		List<BaseItem> list = new List<BaseItem>();
		foreach (BaseItem value in dictionary.Values)
		{
			if (TagRelink.Exists(value.Path))
			{
				list.Add(value);
			}
		}
		List<BaseItem> list2 = ((list.Count > 0) ? list : dictionary.Values.ToList());
		if (list2.Count == 1)
		{
			return (Item: list2[0], Ambiguous: false);
		}
		if (list2.Count == 0)
		{
			return (Item: null, Ambiguous: false);
		}
		List<BaseItem> list3 = new List<BaseItem>();
		foreach (BaseItem item2 in list2)
		{
			if (TagRelink.SameSizeAndDuration(size, durationTicks, item2.Size, item2.RunTimeTicks))
			{
				list3.Add(item2);
			}
		}
		if (list3.Count == 1)
		{
			return (Item: list3[0], Ambiguous: false);
		}
		return (Item: null, Ambiguous: true);
	}

	private BaseItem? FindByPath(string path)
	{
		try
		{
			return _library.FindByPath(path, (bool?)null);
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Jellyfin Color Tags could not resolve {Path}.", path);
			return null;
		}
	}

	private IReadOnlyList<BaseItem> MediaInLibrary()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected Obj, but got Unknown
		try
		{
			IReadOnlyList<BaseItem> itemList = _library.GetItemList(new InternalItemsQuery
			{
				Recursive = true
			});
			if (itemList == null)
			{
				return Array.Empty<BaseItem>();
			}
			return itemList.Where(IsSupported).ToList();
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Jellyfin Color Tags could not list the library.");
			return Array.Empty<BaseItem>();
		}
	}

	private IEnumerable<string> LibraryRoots()
	{
		List<VirtualFolderInfo> virtualFolders;
		try
		{
			virtualFolders = _library.GetVirtualFolders();
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Jellyfin Color Tags could not list libraries.");
			yield break;
		}
		foreach (VirtualFolderInfo item in virtualFolders)
		{
			if (item.Locations == null)
			{
				continue;
			}
			string[] locations = item.Locations;
			string[] array = locations;
			foreach (string text in array)
			{
				if (!string.IsNullOrWhiteSpace(text))
				{
					yield return text;
				}
			}
		}
	}

	private bool TryGetUserId(out Guid userId)
	{
		userId = Guid.Empty;
		string text = User.FindFirst("Jellyfin-UserId")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
		if (!string.IsNullOrEmpty(text) && Guid.TryParse(text, out userId))
		{
			return userId != Guid.Empty;
		}
		return false;
	}

	private static bool IsSupported(BaseItem item)
	{
		switch (((object)item).GetType().Name)
		{
		case "Movie":
		case "Series":
		case "Season":
		case "Episode":
			return true;
		default:
			return false;
		}
	}

	private (long? Size, long? DurationTicks) MediaFacts(BaseItem item)
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected Obj, but got Unknown
		long? size = item.Size;
		long? size2 = ((size.HasValue && size.GetValueOrDefault() > 0) ? item.Size : ((long?)null));
		size = item.RunTimeTicks;
		long? duration = ((size.HasValue && size.GetValueOrDefault() > 0) ? item.RunTimeTicks : ((long?)null));
		if (!size2.HasValue || !duration.HasValue)
		{
			ReadPrimarySource(item, ref size2, ref duration);
		}
		if (!size2.HasValue || !duration.HasValue)
		{
			Folder val = (Folder)((item is Folder) ? item : null);
			if (val != null)
			{
				SumChildren(val, ref size2, ref duration);
			}
		}
		if (!size2.HasValue)
		{
			size2 = FileLength(item.Path);
		}
		return (Size: size2, DurationTicks: duration);
	}

	private void ReadPrimarySource(BaseItem item, ref long? size, ref long? duration)
	{
		IReadOnlyList<MediaSourceInfo> mediaSources;
		try
		{
			mediaSources = item.GetMediaSources(false);
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Jellyfin Color Tags could not read media sources for {ItemId}.", item.Id);
			return;
		}
		string b = item.Id.ToString("N");
		MediaSourceInfo val = null;
		foreach (MediaSourceInfo item2 in mediaSources)
		{
			if (item2 != null)
			{
				if (val == null || string.Equals(item2.Id, b, StringComparison.OrdinalIgnoreCase))
				{
					val = item2;
				}
				if (string.Equals(item2.Id, b, StringComparison.OrdinalIgnoreCase))
				{
					break;
				}
			}
		}
		if (val == null)
		{
			return;
		}
		long? num = size;
		if (!num.HasValue)
		{
			num = val.Size;
			if (num.HasValue && num.GetValueOrDefault() > 0)
			{
				size = val.Size;
			}
		}
		num = duration;
		if (!num.HasValue)
		{
			num = val.RunTimeTicks;
			if (num.HasValue && num.GetValueOrDefault() > 0)
			{
				duration = val.RunTimeTicks;
			}
		}
	}

	private void SumChildren(Folder folder, ref long? size, ref long? duration)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected Obj, but got Unknown
		IEnumerable recursiveChildren;
		try
		{
			recursiveChildren = folder.GetRecursiveChildren();
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Jellyfin Color Tags could not list children of {ItemId}.", ((BaseItem)folder).Id);
			return;
		}
		long num = 0L;
		long num2 = 0L;
		bool flag = false;
		bool flag2 = false;
		int num3 = 0;
		long? size2;
		foreach (BaseItem item in recursiveChildren)
		{
			BaseItem val = item;
			BaseItem val2 = val;
			if (val2 == null || val2.Id == ((BaseItem)folder).Id || !IsLeaf(val2))
			{
				continue;
			}
			num3++;
			if (num3 > 4000)
			{
				break;
			}
			size2 = val2.Size;
			if (size2.HasValue)
			{
				long valueOrDefault = size2.GetValueOrDefault();
				if (valueOrDefault > 0)
				{
					num += valueOrDefault;
					flag = true;
				}
			}
			size2 = val2.RunTimeTicks;
			if (size2.HasValue)
			{
				long valueOrDefault2 = size2.GetValueOrDefault();
				if (valueOrDefault2 > 0)
				{
					num2 += valueOrDefault2;
					flag2 = true;
				}
			}
		}
		size2 = size;
		if (!size2.HasValue & flag)
		{
			size = num;
		}
		size2 = duration;
		if (!size2.HasValue & flag2)
		{
			duration = num2;
		}
	}

	private static long? FileLength(string? path)
	{
		if (path == null)
		{
			return null;
		}
		string text = path.Trim();
		if (text.Length == 0 || !System.IO.File.Exists(text))
		{
			return null;
		}
		try
		{
			long length = new FileInfo(text).Length;
			return (length > 0) ? new long?(length) : ((long?)null);
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return null;
		}
	}

	private static bool IsLeaf(BaseItem item)
	{
		string name = ((object)item).GetType().Name;
		if (name == "Movie" || name == "Episode")
		{
			return true;
		}
		return false;
	}

	private static string ReadClientScript()
	{
		Assembly assembly = typeof(ColorDotsController).Assembly;
		string text = Array.Find(assembly.GetManifestResourceNames(), (string resource) => resource.EndsWith("Web.client.js", StringComparison.Ordinal));
		if (text == null)
		{
			return "/* Jellyfin Color Tags client script is missing from the plugin assembly. */";
		}
		using Stream stream = assembly.GetManifestResourceStream(text);
		if (stream == null)
		{
			return "/* Jellyfin Color Tags client script is missing from the plugin assembly. */";
		}
		using StreamReader streamReader = new StreamReader(stream);
		return streamReader.ReadToEnd();
	}
}
