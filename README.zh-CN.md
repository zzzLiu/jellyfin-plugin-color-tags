# Jellyfin 颜色标签

给 Jellyfin 网页加色点。海报悬停时，圆点在 ⋮ 上方；播放时在收藏按钮旁边。可为每种颜色起名、拖动排序，也可为该色设置码率上限或强制转码。

界面默认英文。显示语言以 `zh` 开头（`zh-CN`、`zh-TW`、`zh-HK`、`zh-Hans`）时用简体中文。`auto` 或留空则跟浏览器语言。

## 兼容性

- Jellyfin **12**（含上报 ABI `12.0.0.0` 的 nyanmisaka 构建）
- `targetAbi`：`12.0.0.0`
- 插件 id：`a7e4c1d2-6b58-4f0a-9c33-81d5e2b47a60`
- 程序集：`Jellyfin.Plugin.ColorDots.dll`（与早期版本相同）
- 标记数据：`/config/colordots/assignments.json`（按 Jellyfin 用户隔离）

## 用插件仓库安装

1. Jellyfin：**控制台 → 插件 → 仓库 → 添加**
2. 名称随意，地址填：

   `https://raw.githubusercontent.com/zzzLiu/jellyfin-plugin-color-tags/main/manifest.json`

3. 在目录里安装 **Jellyfin Color Tags**，然后重启。

安装前请删掉旧的 `ColorDots_*` 目录。Jellyfin 不会自动清旧目录，同 id 的两个目录可能一起被加载。

## 手动安装

从 [Releases](https://github.com/zzzLiu/jellyfin-plugin-color-tags/releases) 下载 `jellyfin-color-tags_1.3.2.0.zip`。

```bash
rm -rf /config/plugins/ColorDots_* /config/plugins/ColorTags_*
mkdir -p /config/plugins/ColorTags_1.3.2.0
unzip jellyfin-color-tags_1.3.2.0.zip -d /config/plugins/ColorTags_1.3.2.0
```

重启 Jellyfin。目录里需要有 `Jellyfin.Plugin.ColorDots.dll` 和 `meta.json`（zip 根目录直接是这两个文件）。

## 使用

在控制台服务器菜单打开 **Jellyfin Color Tags**（中文：**Jellyfin 颜色标签**）。

- 添加颜色和名字，拖动排序。
- 白色是默认占位，不出现在已标记列表，也不能删除。
- 海报菜单从上到下（序号小的在上）；播放器里序号最小的在菜单最下。
- 每种颜色可设码率上限和/或强制转码。同一条目有多个颜色时，取最严的一条。网页播放器可继续用「自动」。直接打开文件的客户端（如 Infuse）不受影响。
- 已标记文件显示名称、媒体库和路径，可导出 CSV。

默认名字跟界面语言（White / 白、Red / 红、Green / 绿、Blue / 蓝）。你改过的名字保持原样。

## 许可证

MIT，见 [LICENSE](LICENSE)。

## 更新说明

见 [RELEASE_NOTES.md](RELEASE_NOTES.md)。
