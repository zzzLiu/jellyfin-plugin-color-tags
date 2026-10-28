# Assignment migration (1.4.0.0)

1.3.x stored one color per item, per Jellyfin user:

```json
"users": {
  "<user guid without dashes>": {
    "<item guid without dashes>": 2
  }
}
```

1.4.0.0 stores every color on an item in one shared list:

```json
"items": {
  "<item guid without dashes>": [2, 6]
}
```

`version` becomes `3`. Label id `0` (white) stays the empty default and is never stored in `items`. Traces stay where they are.

## Which user's colors are kept

When more than one user has assignments:

1. Prefer a live administrator. If several are administrators, the user named `zzzliu` comes first.
2. Otherwise prefer the saved user `dec84115884943b3bb091446124ba36d` when that key is present.
3. Otherwise use the only user, or the user with the most assignments.

That user's color is kept when they tagged the item. Colors from other users are added only for items that user did not tag. Duplicate ids are dropped. Order follows the label list.

## Rollback

On the first save of the new file, the plugin copies the old file to `assignments.v2.json` next to it (it does not overwrite a copy that is already there). The `users` object inside the live file is also left as it was at migration time.

1.3.2 still reads `users` and ignores `items`. To go back:

1. Stop Jellyfin.
2. Replace `assignments.json` with `assignments.v2.json` if you want the pre-1.4 colors, including edits that 1.4 has not kept in `users`.
3. Install plugin 1.3.2.0 and start Jellyfin.

Colors changed after migration are only in `items`. The frozen `users` snapshot does not include them. Item tags written by 1.4 are not removed by rolling the plugin back.

## Item tags

The plugin writes the label's exact name onto the item (for example `看过`). It does not add a prefix, and it does not write the white label. Other tags on the item are left in place. After migration, the plugin writes names for items that already have colors. That pass runs once (`migration.tagsWritten`). If some items fail, it tries again on the next start.

Renaming or deleting a color updates those tags. A tag edited in the metadata editor is not copied back into Color Tags.
