using Jellyfin.Plugin.ColorDots.Services;
using Xunit;

namespace Jellyfin.Plugin.ColorDots.Tests;

public class TagEditTests
{
	[Fact]
	public void Adds_exact_label_names_and_leaves_other_tags()
	{
		string[] next = TagEdit.Apply(new[] { "Action", "看过" }, new[] { "看过", "要下" }, new[] { "看过", "要下", "删" });

		Assert.Equal(new[] { "Action", "看过", "要下" }, next);
	}

	[Fact]
	public void Removes_a_label_name_without_touching_white()
	{
		string[] next = TagEdit.Apply(new[] { "看过", "默认", "Note" }, new[] { "要下" }, new[] { "看过", "要下" });

		Assert.Equal(new[] { "默认", "Note", "要下" }, next);
	}

	[Fact]
	public void Rename_replaces_the_old_name_once()
	{
		string[] next = TagEdit.Apply(new[] { "看过", "Note" }, new[] { "已看" }, new[] { "已看", "看过" });

		Assert.Equal(new[] { "Note", "已看" }, next);
		Assert.True(TagEdit.Same(next, TagEdit.Apply(next, new[] { "已看" }, new[] { "已看", "看过" })));
	}

	[Fact]
	public void Does_not_write_an_empty_desired_list_over_unrelated_tags()
	{
		string[] next = TagEdit.Apply(new[] { "默认", "Favorite" }, Array.Empty<string>(), new[] { "看过" });

		Assert.Equal(new[] { "默认", "Favorite" }, next);
	}
}
