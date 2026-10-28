# Release notes — Jellyfin Color Tags

## 1.4.0.0

- An item can have more than one color. The dot is solid for one color, equal slices for several, and white when none are on.
- The picker has two columns and stays open while you turn colors on or off.
- Colors are shared by every user. Older per-user assignments are migrated; see [MIGRATION.md](MIGRATION.md).
- The color name is written to the item's tags. White is not. Renaming or deleting a color updates those tags. Metadata edits are not copied back.
- If several colors are on, any Force transcode applies, and the lowest bitrate cap is used.

## 1.3.2.0

- Keeps the color dot in the player.
- Stops poster color buttons from covering **Playback Info**.
- On the video detail page, poster overlay buttons are removed so Playback Info (and its close control) stays clickable.

## 1.3.1.0

- Puts **Force transcode** and **Max bitrate** on the right of each color row.
- Shows a saved bitrate after reload.
- Force transcode routes Jellyfin Web through the transcoder.
- Apps that open the file directly (for example Infuse) still skip this.

## 1.3.0.0

- Optional **bitrate cap** and **Force transcode** per color.
- When an item has more than one color, the strictest rule wins.
- Jellyfin Web can stay on Auto quality.
- Direct-file clients (for example Infuse) are not affected.

## Earlier

See older release assets / git history for 1.2.x and before (labels, CSV export, zh language detection, etc.).
