# Jellyfin 颜色标签

[English](README.md)

在 Jellyfin 网页里给影片加一个颜色小点，用来自己做标记。鼠标停在海报上时，色点出现在 ⋮ 按钮上方；播放页面里，色点在收藏按钮旁边。每种颜色可以起名字、拖动调整顺序，也可以单独限制播放码率，或强制转码。

界面默认英文。显示语言以 `zh` 开头（`zh-CN`、`zh-TW`、`zh-HK`、`zh-Hans`）时用简体中文。`auto` 或留空则跟浏览器语言。

## 效果图

电影库海报网格上的绿色颜色标签，标签菜单已打开。

![电影库海报网格，带绿色颜色标签和打开的标签菜单](docs/screenshots/color-tags-movies-grid.png)

剧集库海报网格上的颜色标签，标签菜单已打开。

![剧集库海报网格，带颜色标签和打开的标签菜单](docs/screenshots/color-tags-series-grid.png)

## 兼容性

- Jellyfin **12**（含上报 ABI `12.0.0.0` 的 nyanmisaka 构建）
- `targetAbi`：`12.0.0.0`
- 插件 id：`a7e4c1d2-6b58-4f0a-9c33-81d5e2b47a60`
- 程序集：`Jellyfin.Plugin.ColorDots.dll`（与早期版本相同）
- 标记数据：`/config/colordots/assignments.json`（所有用户共用）

## 用插件仓库安装

1. Jellyfin：**控制台 → 插件 → 仓库 → 添加**
2. 名称随意，地址填：

   `https://raw.githubusercontent.com/zzzLiu/jellyfin-plugin-color-tags/main/manifest.json`

3. 在目录里安装 **Jellyfin Color Tags**，然后重启。

安装前请删掉旧的 `ColorDots_*` 目录。Jellyfin 不会自动清旧目录，同 id 的两个目录可能一起被加载。

## 手动安装

从 [Releases](https://github.com/zzzLiu/jellyfin-plugin-color-tags/releases) 下载 `jellyfin-color-tags_1.4.0.0.zip`。

```bash
rm -rf /config/plugins/ColorDots_* /config/plugins/ColorTags_*
mkdir -p /config/plugins/ColorTags_1.4.0.0
unzip jellyfin-color-tags_1.4.0.0.zip -d /config/plugins/ColorTags_1.4.0.0
```

重启 Jellyfin。目录里需要有 `Jellyfin.Plugin.ColorDots.dll` 和 `meta.json`（zip 根目录直接是这两个文件）。

## 使用

在控制台服务器菜单打开 **Jellyfin Color Tags**（中文：**Jellyfin 颜色标签**）。

- 添加颜色和名字，拖动排序。
- 白色是默认占位，不出现在已标记列表，也不能删除，也不会写进影片标签。
- 一个片子可以有多个颜色。一个颜色是实心圆点，多个颜色分成同样大小的扇形，没有颜色时是白点。
- 点圆点打开两列：左边是已经点上的颜色，右边是还没点的。点一下就加上或去掉，面板不会自动关掉。
- 颜色是共用的，大家看到的是同一套。点亮后，颜色的名字会写到影片标签里；关掉就去掉这个名字。别的标签不动。改名或删除颜色时，影片标签一起改。在元数据里改的标签不会同步回来。
- 每种颜色可设码率上限和/或强制转码。同一条目有多个颜色时，只要有一个强制转码就会转码，码率取最低的上限。网页播放器可继续用「自动」。直接打开文件的客户端（如 Infuse）不受影响。
- 已标记文件显示名称、媒体库和路径，可导出 CSV。

默认名字跟界面语言（White / 白、Red / 红、Green / 绿、Blue / 蓝）。你改过的名字保持原样。

## 支持 / 捐赠

[Ko-fi](https://ko-fi.com/zzzliu)

[![Buy me a coffee](docs/donate/ko-fi-button.png)](https://ko-fi.com/zzzliu)

国内用户可以扫描下方微信赞赏码：

<img src="docs/donate/wechat-appreciate.jpg" alt="微信赞赏码" width="220" />

## 许可证

MIT，见 [LICENSE](LICENSE)。

## 更新说明

见 [RELEASE_NOTES.md](RELEASE_NOTES.md)。
