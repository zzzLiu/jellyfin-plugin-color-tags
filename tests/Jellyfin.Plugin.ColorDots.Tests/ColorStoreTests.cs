using System.Text.Json;
using Jellyfin.Plugin.ColorDots.Services;
using Xunit;

namespace Jellyfin.Plugin.ColorDots.Tests;

public class ColorStoreTests
{
	private const string Admin = "dec84115884943b3bb091446124ba36d";

	private const string Other = "0652672233044b3bb4670fbe1aba2e20";

	[Fact]
	public void Migrates_single_user_map_into_global_items_and_keeps_users()
	{
		string dir = TempDir();
		string path = Path.Combine(dir, "assignments.json");
		File.WriteAllText(path, """
		{
		  "version": 2,
		  "nextId": 4,
		  "labels": [
		    { "id": 0, "name": "White", "color": "#f4f4f5" },
		    { "id": 2, "name": "看过", "color": "#3cba54" }
		  ],
		  "users": {
		    "dec84115884943b3bb091446124ba36d": {
		      "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa": 2,
		      "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb": 0
		    }
		  },
		  "traces": {
		    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa": { "path": "/media/a.mkv", "name": "A" }
		  }
		}
		""");

		ColorStore store = new ColorStore(path, null, new[] { Admin });
		Assert.Equal(new[] { 2 }, store.GetItemLabels(Guid.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")));
		Assert.Empty(store.GetItemLabels(Guid.Parse("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")));

		using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(path));
		Assert.Equal(3, saved.RootElement.GetProperty("version").GetInt32());
		Assert.Equal(2, saved.RootElement.GetProperty("items").GetProperty("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")[0].GetInt32());
		Assert.True(saved.RootElement.GetProperty("users").TryGetProperty(Admin, out _));
		Assert.Equal("/media/a.mkv", saved.RootElement.GetProperty("traces").GetProperty("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa").GetProperty("path").GetString());
		Assert.Equal(Admin, saved.RootElement.GetProperty("migration").GetProperty("preferredUser").GetString());
		Assert.False(saved.RootElement.GetProperty("migration").GetProperty("tagsWritten").GetBoolean());
		Assert.True(store.NeedsInitialTagWrite());
		store.MarkTagsWritten();
		Assert.False(store.NeedsInitialTagWrite());
		Assert.True(File.Exists(Path.Combine(dir, "assignments.v2.json")));
	}

	[Fact]
	public void Prefers_admin_bucket_and_merges_only_items_the_admin_did_not_tag()
	{
		var users = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal)
		{
			[Admin] = new Dictionary<string, int>(StringComparer.Ordinal)
			{
				["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"] = 2,
				["bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"] = 3
			},
			[Other] = new Dictionary<string, int>(StringComparer.Ordinal)
			{
				["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"] = 5,
				["cccccccccccccccccccccccccccccccc"] = 6,
				["dddddddddddddddddddddddddddddddd"] = 8
			},
			["11111111111111111111111111111111"] = new Dictionary<string, int>(StringComparer.Ordinal)
			{
				["cccccccccccccccccccccccccccccccc"] = 6,
				["dddddddddddddddddddddddddddddddd"] = 7
			}
		};

		(Dictionary<string, List<int>> Items, string? PreferredUser) migrated = ColorStore.MigrateAssignments(users, new[] { Admin }, new[] { 0, 2, 3, 5, 6, 7, 8 });

		Assert.Equal(Admin, migrated.PreferredUser);
		Assert.Equal(new[] { 2 }, migrated.Items["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"]);
		Assert.Equal(new[] { 3 }, migrated.Items["bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"]);
		Assert.Equal(new[] { 6 }, migrated.Items["cccccccccccccccccccccccccccccccc"]);
		Assert.Equal(new[] { 7, 8 }, migrated.Items["dddddddddddddddddddddddddddddddd"]);
	}

	[Fact]
	public void Falls_back_to_known_admin_key_when_no_live_admin_is_passed()
	{
		var users = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal)
		{
			[Other] = new Dictionary<string, int> { ["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"] = 1 },
			[Admin] = new Dictionary<string, int> { ["bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"] = 2 }
		};

		string? preferred = ColorStore.ChoosePreferredUser(users, Array.Empty<string>());

		Assert.Equal(Admin, preferred);
	}

	[Fact]
	public void Toggle_adds_and_removes_without_writing_white()
	{
		ColorStore store = new ColorStore(Path.Combine(TempDir(), "assignments.json"));
		Guid item = Guid.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

		Assert.Equal(new[] { 2 }, store.ToggleLabel(item, 2));
		Assert.Equal(new[] { 1, 2 }, store.ToggleLabel(item, 1));
		Assert.Equal(new[] { 2 }, store.ToggleLabel(item, 1));
		Assert.Empty(store.ToggleLabel(item, 0));
		Assert.Empty(store.GetItemLabels(item));
	}

	[Fact]
	public void Playback_uses_any_force_transcode_and_the_lowest_positive_cap()
	{
		ColorStore store = new ColorStore(Path.Combine(TempDir(), "assignments.json"));
		store.UpdateLabel(1, "Red", "#e23b3b", forceTranscode: true, maxBitrateBps: 8_000_000);
		store.UpdateLabel(2, "Green", "#3cba54", forceTranscode: false, maxBitrateBps: 3_000_000);
		store.UpdateLabel(3, "Blue", "#3b82f6", forceTranscode: false, maxBitrateBps: null);
		Guid item = Guid.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
		store.ToggleLabel(item, 1);
		store.ToggleLabel(item, 2);
		store.ToggleLabel(item, 3);

		PlaybackRule rule = PlaybackRules.Combine(store.GetPlaybackRules(item));

		Assert.True(rule.ForceTranscode);
		Assert.Equal(3_000_000, rule.MaxBitrateBps);
	}

	[Fact]
	public void Rename_and_delete_keep_other_labels()
	{
		ColorStore store = new ColorStore(Path.Combine(TempDir(), "assignments.json"));
		Guid item = Guid.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
		store.ToggleLabel(item, 1);
		store.ToggleLabel(item, 2);
		store.UpdateLabel(2, "看过", "#3cba54", false, null);
		Assert.Equal("看过", store.GetLabels().Single(label => label.Id == 2).Name);
		Assert.Equal(new[] { 1, 2 }, store.GetItemLabels(item));

		Assert.Equal(1, store.DeleteLabel(1));
		Assert.Equal(new[] { 2 }, store.GetItemLabels(item));
	}

	[Fact]
	public void Migrates_fixture_file_when_env_points_at_one()
	{
		string? fixture = Environment.GetEnvironmentVariable("COLOR_TAGS_FIXTURE");
		if (string.IsNullOrWhiteSpace(fixture) || !File.Exists(fixture))
		{
			return;
		}

		string dir = TempDir();
		string path = Path.Combine(dir, "assignments.json");
		File.Copy(fixture, path);
		using JsonDocument before = JsonDocument.Parse(File.ReadAllText(path));
		int traces = before.RootElement.GetProperty("traces").EnumerateObject().Count();
		int userItems = before.RootElement.GetProperty("users").GetProperty(Admin).EnumerateObject().Count(pair => pair.Value.GetInt32() != 0);

		ColorStore store = new ColorStore(path, null, new[] { Admin });
		Assert.NotEmpty(store.GetLabels());
		using JsonDocument after = JsonDocument.Parse(File.ReadAllText(path));
		Assert.Equal(3, after.RootElement.GetProperty("version").GetInt32());
		Assert.Equal(userItems, after.RootElement.GetProperty("items").EnumerateObject().Count());
		Assert.Equal(traces, after.RootElement.GetProperty("traces").EnumerateObject().Count());
		Assert.True(after.RootElement.GetProperty("users").TryGetProperty(Admin, out _));
		Assert.All(after.RootElement.GetProperty("items").EnumerateObject(), item =>
		{
			Assert.DoesNotContain(0, item.Value.EnumerateArray().Select(value => value.GetInt32()));
		});
	}

	private static string TempDir()
	{
		string dir = Path.Combine(Path.GetTempPath(), "colortags-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		return dir;
	}
}
