function clean(value, fallback) {
    const text = String(value || "").replace(/[\u0000-\u001F\u007F]/g, "").trim();
    if (!text) {
        return fallback;
    }

    return text.slice(0, 24);
}

function hexOf(value, fallback) {
    const text = String(value || "").trim();
    return /^#[0-9a-fA-F]{6}$/.test(text) ? text.toLowerCase() : fallback;
}

function headers() {
    const api = window.ApiClient;
    const token = api && api.accessToken ? api.accessToken() : "";
    const device = (api && api.deviceId && api.deviceId()) || "colordots";
    return {
        Accept: "application/json",
        "Content-Type": "application/json",
        "X-Emby-Token": token,
        Authorization: 'MediaBrowser Token="' + token + '", Client="ColorDots", Device="Browser", DeviceId="' + String(device).replace(/["\r\n]/g, "").slice(0, 80) + '", Version="1.4.0"'
    };
}

const COPY = {
    en: {
        title: "Jellyfin Color Tags",
        lead: "Add custom colors and names. Drag to reorder. The dot appears above the ⋮ button on posters and next to the favorite button in the player.",
        labels: "Labels",
        note: "White is the default and is not counted.",
        playNote: "A color can cap the bitrate or always transcode. Leave the number empty for no limit. Several colors on one item: any Force transcode applies, and the lowest bitrate cap is used. The name is saved on the item. White is not. The web player can stay on Auto. Apps that open the file themselves, such as Infuse, skip this.",
        force: "Force transcode",
        bitrate: "Max bitrate",
        mbps: "Mbps",
        kbps: "kbps",
        bitrateHint: "Empty means no limit",
        whitePlay: "White is not assigned to items",
        tagged: "Tagged items",
        add: "Add label",
        save: "Save",
        up: "Up",
        down: "Down",
        remove: "Delete",
        drag: "Drag to reorder",
        color: "Color",
        name: "Name",
        library: "Library",
        path: "Path",
        copy: "Copy",
        copied: "Copied.",
        copyFailed: "Could not copy.",
        export: "Export all (CSV)",
        clearInvalid: "Clear lost",
        recover: "Relink",
        deleteAll: "Delete all",
        cleared: "Cleared {count}.",
        relinked: "Relinked {count}.",
        deletedAll: "Deleted all {count}.",
        nothingToDelete: "Nothing to delete.",
        confirmDeleteAll: "Delete all items with this label?",
        copyAll: "Copy all paths",
        relinkedSkipped: "Relinked {count}. Skipped {skipped}.",
        noneMatched: "No match. Skipped {skipped}.",
        nothingToRelink: "Nothing to relink.",
        maintainFailed: "Could not update.",
        confirmItem: "Remove this tag?",
        removedOne: "Removed.",
        notRemoved: "Could not remove.",
        exported: "Exported.",
        exportEmpty: "Nothing to export.",
        csvLabel: "Label",
        csvName: "Name",
        csvType: "Type",
        csvYear: "Year",
        csvLibrary: "Library",
        csvPath: "Path",
        loading: "Loading…",
        empty: "No tagged items yet.",
        none: "No items",
        missing: "Not in the library",
        linkLost: "Item link lost",
        saved: "Saved.",
        notSaved: "Could not save.",
        orderSaved: "Order saved.",
        orderFailed: "Could not save the order.",
        added: "Added.",
        notAdded: "Could not add.",
        deleted: "Deleted. Those items return to White.",
        notDeleted: "Could not delete.",
        labelsFailed: "Could not load labels.",
        reportFailed: "Could not load tagged items.",
        usageFailed: "Could not read how many items use this label.",
        confirm: "{count} items use this label. They return to White. Delete it?",
        white: "White",
        red: "Red",
        green: "Green",
        blue: "Blue",
        fresh: "New label"
    },
    "zh-CN": {
        title: "Jellyfin 颜色标签",
        lead: "可以自定义颜色和名字，拖动即可排序。圆点出现在海报 ⋮ 按钮上方，以及播放器收藏按钮旁边。",
        labels: "标签",
        note: "白色是默认项，不计入已标记文件。",
        playNote: "颜色可以限制码率，或总是转码。数字留空表示不限制。一个片子有多个颜色时，只要有一个强制转码就会转码，码率取最低的上限。颜色名字会写到影片标签里，白色不会。网页播放器可以保持自动。自己打开文件的应用（例如 Infuse）不会受影响。",
        force: "强制转码",
        bitrate: "码率上限",
        mbps: "Mbps",
        kbps: "kbps",
        bitrateHint: "留空表示不限制",
        whitePlay: "白色不会标到文件上",
        tagged: "已标记文件",
        add: "添加标签",
        save: "保存",
        up: "上移",
        down: "下移",
        remove: "删除",
        drag: "拖动排序",
        color: "颜色",
        name: "名称",
        library: "媒体库",
        path: "路径",
        copy: "复制",
        copied: "已复制。",
        copyFailed: "没有复制。",
        export: "导出全部(CSV)",
        clearInvalid: "清除失效",
        recover: "重新关联",
        deleteAll: "删除全部",
        cleared: "已清除 {count} 条。",
        relinked: "已重新关联 {count} 条。",
        deletedAll: "已删除全部 {count} 条。",
        nothingToDelete: "没有可删除的条目。",
        confirmDeleteAll: "删除这个标签下的全部条目？",
        copyAll: "复制全部路径",
        relinkedSkipped: "已重新关联 {count} 条，跳过 {skipped} 条。",
        noneMatched: "没有对上，跳过 {skipped} 条。",
        nothingToRelink: "没有需要重新关联的条目。",
        maintainFailed: "没有更新。",
        confirmItem: "删除这条标记？",
        removedOne: "已删除。",
        notRemoved: "没有删除。",
        exported: "已导出。",
        exportEmpty: "没有可导出的文件。",
        csvLabel: "标签名",
        csvName: "名称",
        csvType: "类型",
        csvYear: "年份",
        csvLibrary: "媒体库",
        csvPath: "路径",
        loading: "正在加载…",
        empty: "还没有已标记文件。",
        none: "没有文件",
        missing: "不在资料库中",
        linkLost: "此项连接已丢失",
        saved: "已保存。",
        notSaved: "没有保存。",
        orderSaved: "顺序已保存。",
        orderFailed: "顺序没有保存。",
        added: "已添加。",
        notAdded: "没有添加。",
        deleted: "已删除，这些条目回到白色。",
        notDeleted: "没有删除。",
        labelsFailed: "标签没有加载。",
        reportFailed: "已标记文件没有加载。",
        usageFailed: "没有读到数量。",
        confirm: "有 {count} 个条目使用这个标签。删除后会回到白色。删除吗？",
        white: "白",
        red: "红",
        green: "绿",
        blue: "蓝",
        fresh: "新标签"
    }
};

const STOCK = { 0: ["white", "白"], 1: ["red", "红"], 2: ["green", "绿"], 3: ["blue", "蓝"] };

let locale = "en";
let userDisplayLanguage = "";

function isAuto(value) {
    const text = String(value || "").trim().toLowerCase();
    return !text || text === "auto";
}

function mapLocale(value) {
    const culture = String(value || "").trim().toLowerCase().replace(/_/g, "-");
    if (culture === "zh" || culture.indexOf("zh-") === 0) {
        return "zh-CN";
    }
    return "en";
}

function currentUserIdRaw() {
    const api = window.ApiClient;
    if (api && typeof api.getCurrentUserId === "function") {
        try {
            const id = api.getCurrentUserId();
            if (id) {
                return String(id);
            }
        } catch (err) {
            /* fall through to stored credentials */
        }
    }
    try {
        const raw = localStorage.getItem("jellyfin_credentials") || localStorage.getItem("servercredentials") || "";
        const creds = raw ? JSON.parse(raw) : null;
        const servers = creds && (creds.Servers || creds.servers);
        if (servers && servers.length && (servers[0].UserId || servers[0].userId)) {
            return String(servers[0].UserId || servers[0].userId);
        }
    } catch (err) {
        return "";
    }
    return "";
}

function languageKeys(userId) {
    const raw = String(userId || "").trim().toLowerCase();
    const compact = raw.replace(/-/g, "");
    const keys = [];
    if (raw) {
        keys.push(raw + "-language");
    }
    if (compact && compact !== raw) {
        keys.push(compact + "-language");
    }
    if (/^[0-9a-f]{32}$/.test(compact)) {
        keys.push(compact.slice(0, 8) + "-" + compact.slice(8, 12) + "-" + compact.slice(12, 16) + "-" + compact.slice(16, 20) + "-" + compact.slice(20) + "-language");
    }
    return keys;
}

function languageFromStorage(userId) {
    const compact = String(userId || "").replace(/-/g, "").toLowerCase();
    let sawOwn = false;
    try {
        const keys = languageKeys(userId);
        for (let i = 0; i < keys.length; i++) {
            const value = localStorage.getItem(keys[i]);
            if (value === null) {
                continue;
            }
            sawOwn = true;
            if (!isAuto(value)) {
                return value;
            }
        }
        if (sawOwn || !compact) {
            return "";
        }
        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i) || "";
            if (!/language/i.test(key)) {
                continue;
            }
            if (key.replace(/-/g, "").toLowerCase().indexOf(compact) === -1) {
                continue;
            }
            const stored = localStorage.getItem(key);
            if (stored === null || isAuto(stored)) {
                return "";
            }
            return stored;
        }
    } catch (err) {
        return "";
    }
    return "";
}

function uiLocale() {
    if (!isAuto(userDisplayLanguage)) {
        return mapLocale(userDisplayLanguage);
    }
    const stored = languageFromStorage(currentUserIdRaw());
    if (!isAuto(stored)) {
        return mapLocale(stored);
    }
    const nav = (navigator.languages && navigator.languages[0]) || navigator.language || "";
    if (!isAuto(nav)) {
        return mapLocale(nav);
    }
    const attr = document.documentElement.getAttribute("lang") || "";
    if (!isAuto(attr)) {
        return mapLocale(attr);
    }
    return "en";
}

function t(key) {
    const pack = COPY[locale] || COPY.en;
    return pack[key] || COPY.en[key] || "";
}

function isStockName(id, name) {
    const list = STOCK[id];
    if (!list) {
        return false;
    }
    return list.indexOf(String(name || "").trim().toLowerCase()) !== -1;
}

function displayName(id, stored) {
    if (!isStockName(id, stored)) {
        return stored || t("fresh");
    }
    if (id === 0) {
        return t("white");
    }
    if (id === 1) {
        return t("red");
    }
    if (id === 2) {
        return t("green");
    }
    return t("blue");
}

function itemTitle(name) {
    if (name === "Not in the library" || name === "未在资料库中" || name === "不在资料库中") {
        return t("missing");
    }
    return name;
}

function isMissingItem(item) {
    if (!item) {
        return false;
    }
    if (item.missing === true || item.Missing === true) {
        return true;
    }
    const name = item.name || item.Name || "";
    return name === "Not in the library" || name === "未在资料库中" || name === "不在资料库中";
}

function pathLeaf(path) {
    const text = String(path || "").replace(/\\/g, "/").replace(/\/+$/, "");
    if (!text) {
        return "";
    }
    const slash = text.lastIndexOf("/");
    return (slash >= 0 ? text.slice(slash + 1) : text).trim();
}

function missingFileName(item) {
    const explicit = String(item.fileName || item.FileName || "").trim();
    if (explicit) {
        return explicit;
    }
    return pathLeaf(item.path || item.Path || "");
}

function formatSize(bytes) {
    const units = ["B", "KB", "MB", "GB", "TB"];
    let value = Number(bytes);
    if (!Number.isFinite(value) || value < 0) {
        return "";
    }
    let unit = 0;
    while (value >= 1024 && unit < units.length - 1) {
        value /= 1024;
        unit += 1;
    }
    const text = unit === 0 ? String(Math.round(value)) : String(Math.round(value * 10) / 10);
    return text + " " + units[unit];
}

function formatDuration(ticks) {
    const total = Math.round(Number(ticks) / 10000000);
    if (!Number.isFinite(total) || total < 0) {
        return "";
    }
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const seconds = total % 60;
    const mm = String(minutes).padStart(2, "0");
    const ss = String(seconds).padStart(2, "0");
    if (hours > 0) {
        return String(hours).padStart(2, "0") + ":" + mm + ":" + ss;
    }
    return mm + ":" + ss;
}

function positiveNumber(value) {
    if (value == null || value === "") {
        return null;
    }
    const number = Number(value);
    return Number.isFinite(number) && number > 0 ? number : null;
}

function mediaFact(item) {
    const size = positiveNumber(item.size != null ? item.size : item.Size);
    const duration = positiveNumber(item.duration != null ? item.duration : item.Duration);
    const parts = [];
    if (size != null) {
        const text = formatSize(size);
        if (text) {
            parts.push(text);
        }
    }
    if (duration != null) {
        const text = formatDuration(duration);
        if (text) {
            parts.push(text);
        }
    }
    return parts.join(" · ");
}

function missingLines(item) {
    const lines = [t("linkLost")];
    const title = String(item.name || item.Name || "").trim();
    if (title && title !== "Not in the library" && title !== "未在资料库中" && title !== "不在资料库中") {
        lines.push(title);
    }
    const facts = mediaFact(item);
    if (facts) {
        lines.push(facts);
    }
    const fileName = missingFileName(item);
    const path = String(item.path || item.Path || "").trim();
    if (fileName) {
        lines.push(fileName);
    }
    if (path) {
        lines.push(path);
    }
    return lines;
}

function api(method, path, body) {
    const url = window.ApiClient.getUrl(path);
    return fetch(url, {
        method: method,
        headers: headers(),
        body: body == null ? undefined : JSON.stringify(body),
        credentials: "same-origin",
        cache: "no-store"
    }).then(function (response) {
        if (!response.ok) {
            throw new Error(String(response.status));
        }
        if (response.status === 204) {
            return null;
        }
        return response.json();
    });
}

function bitrateFields(bps) {
    const value = Number(bps);
    if (!Number.isFinite(value) || value <= 0) {
        return { value: "", unit: "mbps" };
    }
    if (value % 1000 !== 0 || value < 1000000) {
        return { value: String(value / 1000), unit: "kbps" };
    }
    const mbps = Math.round((value / 1000000) * 1000) / 1000;
    return { value: String(mbps), unit: "mbps" };
}

function bitrateToBps(text, unit) {
    const raw = String(text == null ? "" : text).trim();
    if (!raw) {
        return null;
    }
    const number = Number(raw);
    if (!Number.isFinite(number) || number <= 0) {
        return null;
    }
    const factor = unit === "kbps" ? 1000 : 1000000;
    return Math.round(number * factor);
}

function readLabels(data) {
    const list = (data && (data.labels || data.Labels)) || data || [];
    const rows = [];
    list.forEach(function (row) {
        const id = Number(row.id != null ? row.id : row.Id);
        if (!Number.isFinite(id)) {
            return;
        }
        const bps = row.maxBitrateBps != null ? row.maxBitrateBps : row.MaxBitrateBps;
        const force = row.forceTranscode != null ? row.forceTranscode : row.ForceTranscode;
        rows.push({
            id: id,
            name: clean(row.name || row.Name, id === 0 ? "White" : "New label"),
            color: hexOf(row.color || row.Color, id === 0 ? "#f4f4f5" : "#888888"),
            fixed: id === 0,
            forceTranscode: id !== 0 && !!force,
            maxBitrateBps: id === 0 ? null : (Number(bps) > 0 ? Math.round(Number(bps)) : null)
        });
    });
    return rows;
}

export default function (view) {
    const form = view.querySelector("#ColorDotsConfigForm");
    const host = view.querySelector("#jfcd-labels");
    const report = view.querySelector("#jfcd-report");
    const status = view.querySelector("#jfcd-status");
    const add = view.querySelector("#jfcd-add");
    if (!form || !host || form.dataset.jfcdBound === "1") {
        return;
    }

    form.dataset.jfcdBound = "1";
    let labels = [];
    let reportData = null;

    function rememberUser(user) {
        const config = (user && (user.Configuration || user.configuration)) || {};
        userDisplayLanguage = config.DisplayLanguage || config.displayLanguage || (user && (user.DisplayLanguage || user.displayLanguage)) || "";
    }

    function paint() {
        locale = uiLocale();
        applyCopy();
        if (host.querySelector(".jfcd-row")) {
            collect();
        }
        if (labels.length) {
            renderLabels();
        }
        if (reportData) {
            renderReport(reportData);
        }
    }

    function refreshLocale() {
        const next = uiLocale();
        if (next === locale) {
            return;
        }
        paint();
    }

    function watchLanguage() {
        [0, 50, 250, 800].forEach(function (delay) {
            window.setTimeout(refreshLocale, delay);
        });
        const api = window.ApiClient;
        if (!api || typeof api.getCurrentUser !== "function") {
            return;
        }
        let current;
        try {
            current = api.getCurrentUser();
        } catch (err) {
            return;
        }
        if (!current) {
            return;
        }
        if (typeof current.then === "function") {
            current.then(function (user) {
                rememberUser(user);
                refreshLocale();
            }).catch(function () { /* the display language stays on localStorage */ });
            return;
        }
        rememberUser(current);
        refreshLocale();
    }

    function setStatus(text) {
        status.textContent = text || "";
    }

    function applyCopy() {
        const title = t("title");
        view.setAttribute("data-title", title);
        const pageTitle = view.querySelector("#jfcd-page-title");
        const lead = view.querySelector("#jfcd-lead");
        const labelsTitle = view.querySelector("#jfcd-labels-title");
        const note = view.querySelector("#jfcd-note");
        const playNote = view.querySelector("#jfcd-play-note");
        const reportTitle = view.querySelector("#jfcd-report-title");
        const addLabel = view.querySelector("#jfcd-add span");
        const saveLabel = view.querySelector("button[type='submit'] span");
        const exportLabel = view.querySelector("#jfcd-export span");
        if (pageTitle) {
            pageTitle.textContent = title;
        }
        if (lead) {
            lead.textContent = t("lead");
        }
        if (labelsTitle) {
            labelsTitle.textContent = t("labels");
        }
        if (note) {
            note.textContent = t("note");
        }
        if (playNote) {
            playNote.textContent = t("playNote");
        }
        if (reportTitle) {
            reportTitle.textContent = t("tagged");
        }
        if (addLabel) {
            addLabel.textContent = t("add");
        }
        if (saveLabel) {
            saveLabel.textContent = t("save");
        }
        if (exportLabel) {
            exportLabel.textContent = t("export");
        }
    }

    function loading(on) {
        if (!window.Dashboard) {
            return;
        }
        if (on && window.Dashboard.showLoadingMsg) {
            window.Dashboard.showLoadingMsg();
        }
        if (!on && window.Dashboard.hideLoadingMsg) {
            window.Dashboard.hideLoadingMsg();
        }
    }

    function collect() {
        host.querySelectorAll(".jfcd-row").forEach(function (row) {
            const id = Number(row.dataset.id);
            const label = labels.find(function (item) { return item.id === id; });
            if (!label) {
                return;
            }
            const name = row.querySelector(".jfcd-name");
            const picker = row.querySelector("input[type='color']");
            const typed = clean(name && name.value, label.id === 0 ? "White" : "New label");
            label.name = isStockName(label.id, typed) ? label.name : typed;
            label.color = hexOf(picker && picker.value, label.color);
            const force = row.querySelector(".jfcd-force-input");
            const bitrate = row.querySelector(".jfcd-bitrate-input");
            const unit = row.querySelector(".jfcd-unit");
            if (label.id === 0) {
                label.forceTranscode = false;
                label.maxBitrateBps = null;
            } else {
                label.forceTranscode = !!(force && force.checked);
                let bitrateText = bitrate && bitrate.value;
                if (bitrate && bitrate.dataset.edited !== "1" && !String(bitrateText || "").trim() && bitrate.dataset.bps) {
                    bitrateText = bitrate.dataset.bps;
                }
                label.maxBitrateBps = bitrateToBps(bitrateText, unit && unit.value);
            }
        });
    }

    function clearDrop() {
        host.querySelectorAll(".jfcd-row").forEach(function (row) {
            row.classList.remove("is-drop-before", "is-drop-after");
        });
    }

    function rowFromPoint(x, y, skip) {
        const nodes = document.elementsFromPoint(x, y);
        for (let i = 0; i < nodes.length; i++) {
            const row = nodes[i].closest ? nodes[i].closest(".jfcd-row") : null;
            if (row && row !== skip && host.contains(row)) {
                return row;
            }
        }
        return null;
    }

    function bindDrag(row, handle) {
        handle.addEventListener("pointerdown", function (event) {
            if (event.button != null && event.button !== 0) {
                return;
            }
            event.preventDefault();
            const pointerId = event.pointerId;
            handle.setPointerCapture(pointerId);
            const startY = event.clientY;
            let dragging = false;

            function move(ev) {
                if (ev.pointerId !== pointerId) {
                    return;
                }
                if (!dragging && Math.abs(ev.clientY - startY) < 4) {
                    return;
                }
                dragging = true;
                row.classList.add("is-dragging");
                clearDrop();
                const target = rowFromPoint(ev.clientX, ev.clientY, row);
                if (!target) {
                    return;
                }
                const box = target.getBoundingClientRect();
                target.classList.add(ev.clientY < box.top + box.height / 2 ? "is-drop-before" : "is-drop-after");
            }

            function end(ev) {
                handle.removeEventListener("pointermove", move);
                handle.removeEventListener("pointerup", end);
                handle.removeEventListener("pointercancel", end);
                const target = dragging ? rowFromPoint(ev.clientX, ev.clientY, row) : null;
                const before = target ? ev.clientY < target.getBoundingClientRect().top + target.getBoundingClientRect().height / 2 : false;
                clearDrop();
                row.classList.remove("is-dragging");
                if (!dragging || !target) {
                    return;
                }
                const fromId = Number(row.dataset.id);
                const toId = Number(target.dataset.id);
                const ids = labels.map(function (label) { return label.id; }).filter(function (id) { return id !== fromId; });
                let index = ids.indexOf(toId);
                if (index < 0) {
                    return;
                }
                if (!before) {
                    index += 1;
                }
                ids.splice(index, 0, fromId);
                persistOrder(ids);
            }

            handle.addEventListener("pointermove", move);
            handle.addEventListener("pointerup", end);
            handle.addEventListener("pointercancel", end);
        });
    }

    function renderLabels() {
        host.textContent = "";
        labels.forEach(function (label, index) {
            const line = document.createElement("div");
            line.className = "jfcd-line";
            line.dataset.id = String(label.id);

            const num = document.createElement("span");
            num.className = "jfcd-num";
            num.textContent = String(index + 1);
            line.appendChild(num);

            const row = document.createElement("div");
            row.className = "jfcd-row";
            row.dataset.id = String(label.id);

            const handle = document.createElement("button");
            handle.type = "button";
            handle.className = "jfcd-handle";
            handle.textContent = "⠿";
            handle.setAttribute("aria-label", t("drag"));
            row.appendChild(handle);
            bindDrag(row, handle);

            const picker = document.createElement("input");
            picker.type = "color";
            picker.className = "jfcd-swatch";
            picker.value = label.color;
            picker.setAttribute("aria-label", t("color"));
            row.appendChild(picker);

            const name = document.createElement("input");
            name.type = "text";
            name.className = "jfcd-name";
            name.maxLength = 24;
            name.value = displayName(label.id, label.name);
            name.setAttribute("aria-label", t("name"));
            name.autocomplete = "off";
            row.appendChild(name);

            const play = document.createElement("div");
            play.className = "jfcd-play" + (label.id === 0 ? " is-locked" : "");
            const shown = bitrateFields(label.maxBitrateBps);
            const forceLabel = document.createElement("label");
            forceLabel.className = "jfcd-force";
            const force = document.createElement("input");
            force.type = "checkbox";
            force.className = "jfcd-force-input";
            force.checked = !!label.forceTranscode;
            force.disabled = label.id === 0;
            force.title = label.id === 0 ? t("whitePlay") : t("force");
            const forceText = document.createElement("span");
            forceText.textContent = t("force");
            forceLabel.appendChild(force);
            forceLabel.appendChild(forceText);
            const rateLabel = document.createElement("label");
            rateLabel.className = "jfcd-bitrate";
            const rateText = document.createElement("span");
            rateText.textContent = t("bitrate");
            const rate = document.createElement("input");
            rate.type = "text";
            rate.className = "jfcd-bitrate-input";
            rate.inputMode = "decimal";
            rate.autocomplete = "off";
            rate.placeholder = "—";
            rate.dataset.bps = shown.value;
            rate.defaultValue = shown.value;
            rate.value = shown.value;
            rate.disabled = label.id === 0;
            rate.addEventListener("input", function () {
                rate.dataset.edited = "1";
            });
            rate.title = label.id === 0 ? t("whitePlay") : t("bitrateHint");
            rate.setAttribute("aria-label", t("bitrate"));
            const unit = document.createElement("select");
            unit.className = "jfcd-unit";
            unit.disabled = label.id === 0;
            ["mbps", "kbps"].forEach(function (key) {
                const option = document.createElement("option");
                option.value = key;
                option.textContent = t(key);
                unit.appendChild(option);
            });
            unit.value = shown.unit;
            rateLabel.appendChild(rateText);
            rateLabel.appendChild(rate);
            rateLabel.appendChild(unit);
            play.appendChild(forceLabel);
            play.appendChild(rateLabel);
            row.appendChild(play);

            function slot(button) {
                const cell = document.createElement("div");
                cell.className = "jfcd-slot";
                if (button) {
                    cell.appendChild(button);
                }
                return cell;
            }

            function iconButton(glyph, labelText, action, disabled) {
                const button = document.createElement("button");
                button.type = "button";
                button.className = "jfcd-iconbtn" + (glyph === "trash" ? " is-delete" : "");
                button.title = labelText;
                button.setAttribute("aria-label", labelText);
                button.disabled = !!disabled;
                button.textContent = glyph === "up" ? "↑" : (glyph === "down" ? "↓" : "⌫");
                if (glyph === "trash") {
                    button.textContent = "";
                    button.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 3h6l1 2h4v2H4V5h4l1-2zm1 6h2v8h-2V9zm4 0h2v8h-2V9zM7 9h2v8H7V9z"/></svg>';
                }
                if (action) {
                    button.addEventListener("click", action);
                }
                return button;
            }

            const actions = document.createElement("div");
            actions.className = "jfcd-actions";
            actions.appendChild(slot(iconButton("up", t("up"), function () { move(label.id, -1); }, index === 0)));
            actions.appendChild(slot(iconButton("down", t("down"), function () { move(label.id, 1); }, index === labels.length - 1)));
            if (label.id === 0) {
                actions.appendChild(slot(null));
            } else {
                actions.appendChild(slot(iconButton("trash", t("remove"), function () { removeLabel(label.id); }, false)));
            }
            line.appendChild(row);
            line.appendChild(actions);
            host.appendChild(line);
            rate.value = shown.value;
            rate.defaultValue = shown.value;
        });
    }

    function absolutePath(item) {
        return String((item && (item.path || item.Path)) || "").trim();
    }

    function groupPaths(items) {
        const lines = [];
        (items || []).forEach(function (item) {
            const path = absolutePath(item);
            if (path) {
                lines.push(path);
            }
        });
        return lines.join("\n");
    }

    function copyPath(value, button) {
        const text = String(value == null ? "" : value).trim();
        if (!text) {
            setStatus(t("copyFailed"));
            return;
        }
        const keepLine = button.classList.contains("jfcd-file") || button.classList.contains("jfcd-group-name");
        const done = function () {
            if (keepLine) {
                setStatus(t("copied"));
                return;
            }
            button.textContent = t("copied");
            window.setTimeout(function () {
                button.textContent = t("copy");
            }, 1200);
        };
        const fallback = function () {
            const area = document.createElement("textarea");
            area.value = text;
            area.setAttribute("readonly", "readonly");
            document.body.appendChild(area);
            area.select();
            try {
                document.execCommand("copy");
                done();
            } catch (err) {
                setStatus(t("copyFailed"));
            }
            area.remove();
        };
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(done).catch(fallback);
            return;
        }
        fallback();
    }

    function renderReport(data) {
        const groups = (data && (data.groups || data.Groups)) || [];
        report.textContent = "";
        if (!groups.length) {
            const empty = document.createElement("p");
            empty.className = "jfcd-empty";
            empty.textContent = t("empty");
            report.appendChild(empty);
            return;
        }

        groups.forEach(function (group, index) {
            const box = document.createElement("section");
            box.className = "jfcd-group";
            const head = document.createElement("div");
            head.className = "jfcd-group-head";
            const title = document.createElement("h3");
            const dot = document.createElement("span");
            dot.className = "jfcd-swatch";
            dot.style.background = group.color || group.Color || "#888888";
            const name = document.createElement("button");
            const items = group.items || group.Items || [];
            const count = group.count != null ? group.count : (group.Count != null ? group.Count : items.length);
            const number = group.number != null ? group.number : (group.Number != null ? group.Number : index + 1);
            const storedName = group.name || group.Name || "";
            const groupId = group.id != null ? group.id : group.Id;
            name.type = "button";
            name.className = "jfcd-group-name";
            name.textContent = displayName(Number(groupId), storedName) + " · " + count;
            name.title = t("copyAll");
            name.addEventListener("click", function () { copyPath(groupPaths(items), name); });
            const num = document.createElement("span");
            num.className = "jfcd-num";
            num.textContent = String(number);
            const actions = document.createElement("div");
            actions.className = "jfcd-maintain";
            actions.appendChild(maintainButton("clear", t("clearInvalid"), function () { maintain("clear", groupId); }));
            actions.appendChild(maintainButton("relink", t("recover"), function () { maintain("relink", groupId); }));
            actions.appendChild(maintainButton("delete-all", t("deleteAll"), function () { deleteAll(groupId, items.length); }));
            title.appendChild(num);
            title.appendChild(dot);
            title.appendChild(name);
            head.appendChild(title);
            head.appendChild(actions);
            box.appendChild(head);
            box.dataset.id = String(groupId);

            if (!items.length) {
                const none = document.createElement("p");
                none.className = "jfcd-empty";
                none.textContent = t("none");
                box.appendChild(none);
            } else {
                const list = document.createElement("ul");
                list.className = "jfcd-items";
                items.forEach(function (item) {
                    const li = document.createElement("li");
                    const year = item.year != null ? item.year : item.Year;
                    const place = item.place || item.Place || item.library || item.Library || "";
                    const path = absolutePath(item);
                    const row = document.createElement("div");
                    row.className = "jfcd-file-row";
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className = "jfcd-file";
                    button.title = path;
                    if (isMissingItem(item)) {
                        button.classList.add("is-missing");
                        missingLines(item).forEach(function (lineText) {
                            const lineNode = document.createElement("span");
                            lineNode.className = "jfcd-file-text";
                            lineNode.textContent = lineText;
                            button.appendChild(lineNode);
                        });
                    } else {
                        const bits = [itemTitle(item.name || item.Name || "")];
                        if (year) {
                            bits.push(String(year));
                        }
                        const facts = mediaFact(item);
                        if (facts) {
                            bits.push(facts);
                        }
                        if (place) {
                            bits.push(place);
                        }
                        const line = bits.join(" · ");
                        const text = document.createElement("span");
                        text.className = "jfcd-file-text";
                        text.textContent = line;
                        button.appendChild(text);
                    }
                    button.addEventListener("click", function () { copyPath(path, button); });
                    const remove = document.createElement("button");
                    remove.type = "button";
                    remove.className = "jfcd-btn jfcd-item-delete";
                    remove.textContent = t("remove");
                    remove.addEventListener("click", function () { removeItem(groupId, item.id || item.Id); });
                    row.appendChild(button);
                    row.appendChild(remove);
                    li.appendChild(row);
                    list.appendChild(li);
                });
                box.appendChild(list);
            }
            report.appendChild(box);
        });
    }

    function maintainButton(kind, labelText, action) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "jfcd-btn jfcd-" + kind;
        button.textContent = labelText;
        button.addEventListener("click", action);
        return button;
    }

    function deleteAll(id, count) {
        if (!count) {
            setStatus(t("nothingToDelete"));
            return;
        }
        if (!window.confirm(t("confirmDeleteAll"))) {
            return;
        }
        maintain("deleteAll", id);
    }

    async function maintain(kind, id) {
        loading(true);
        try {
            const path = kind === "clear"
                ? "ColorDots/labels/" + id + "/invalid"
                : (kind === "deleteAll" ? "ColorDots/labels/" + id + "/items" : "ColorDots/labels/" + id + "/relink");
            const result = kind === "deleteAll" ? await api("DELETE", path) : await api("POST", path, {});
            await loadReport();
            const cleared = result && (result.cleared != null ? result.cleared : result.Cleared);
            const relinked = result && (result.relinked != null ? result.relinked : result.Relinked);
            const skipped = result && (result.skipped != null ? result.skipped : result.Skipped);
            if (kind === "deleteAll") {
                setStatus(cleared ? t("deletedAll").replace("{count}", String(cleared)) : t("nothingToDelete"));
            } else if (kind === "clear") {
                setStatus(t("cleared").replace("{count}", String(cleared || 0)));
            } else if (!relinked && !skipped) {
                setStatus(t("nothingToRelink"));
            } else if (!relinked) {
                setStatus(t("noneMatched").replace("{skipped}", String(skipped || 0)));
            } else if (skipped) {
                setStatus(t("relinkedSkipped").replace("{count}", String(relinked || 0)).replace("{skipped}", String(skipped)));
            } else {
                setStatus(t("relinked").replace("{count}", String(relinked || 0)));
            }
        } catch (err) {
            setStatus(t("maintainFailed"));
        } finally {
            loading(false);
        }
    }

    function csvCell(value) {
        const text = value == null ? "" : String(value);
        if (/[",\n\r]/.test(text)) {
            return '"' + text.replace(/"/g, '""') + '"';
        }
        return text;
    }

    function exportCsv() {
        locale = uiLocale();
        const groups = (reportData && (reportData.groups || reportData.Groups)) || [];
        const lines = [[t("csvLabel"), t("csvName"), t("csvType"), t("csvYear"), t("csvLibrary"), t("csvPath")].join(",")];
        groups.forEach(function (group) {
            const items = group.items || group.Items || [];
            const storedName = group.name || group.Name || "";
            const groupId = group.id != null ? group.id : group.Id;
            const labelName = displayName(Number(groupId), storedName);
            items.forEach(function (item) {
                const year = item.year != null ? item.year : item.Year;
                const missing = isMissingItem(item);
                const storedPath = String(item.path || item.Path || "").trim();
                const shownName = missing
                    ? missingLines(item).filter(function (line) { return line !== storedPath; }).join(" · ")
                    : itemTitle(item.name || item.Name || "");
                lines.push([
                    labelName,
                    shownName,
                    item.type || item.Type || "",
                    year == null ? "" : year,
                    item.library || item.Library || "",
                    item.path || item.Path || ""
                ].map(csvCell).join(","));
            });
        });
        if (lines.length === 1) {
            setStatus(t("exportEmpty"));
            return;
        }
        const blob = new Blob(["\uFEFF" + lines.join("\r\n")], { type: "text/csv;charset=utf-8" });
        const link = document.createElement("a");
        link.href = URL.createObjectURL(blob);
        link.download = "jellyfin-color-tags.csv";
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.setTimeout(function () { URL.revokeObjectURL(link.href); }, 1000);
        setStatus(t("exported"));
    }

    function settled() {
        view.dispatchEvent(new CustomEvent("jfcd-settled"));
    }

    async function loadReport() {
        try {
            reportData = await api("GET", "ColorDots/Report");
            locale = uiLocale();
            renderReport(reportData);
        } catch (err) {
            reportData = null;
            report.textContent = "";
            const failed = document.createElement("p");
            failed.className = "jfcd-empty";
            failed.textContent = t("reportFailed");
            report.appendChild(failed);
        }
    }

    async function load() {
        loading(true);
        try {
            locale = uiLocale();
            applyCopy();
            labels = readLabels(await api("GET", "ColorDots/labels"));
            locale = uiLocale();
            applyCopy();
            renderLabels();
            setStatus("");
            await loadReport();
        } catch (err) {
            setStatus(t("labelsFailed"));
        } finally {
            loading(false);
            settled();
        }
    }

    async function saveAll() {
        collect();
        for (let i = 0; i < labels.length; i++) {
            const label = labels[i];
            await api("PUT", "ColorDots/labels/" + label.id, {
                name: label.name,
                color: label.color,
                forceTranscode: !!label.forceTranscode,
                maxBitrateBps: label.maxBitrateBps || null
            });
        }
    }

    async function persistOrder(ids) {
        loading(true);
        try {
            await saveAll();
            labels = readLabels(await api("PUT", "ColorDots/labels/order", { ids: ids }));
            renderLabels();
            setStatus(t("orderSaved"));
            await loadReport();
        } catch (err) {
            setStatus(t("orderFailed"));
            await load();
        } finally {
            loading(false);
        }
    }

    function move(id, delta) {
        const ids = labels.map(function (label) { return label.id; });
        const index = ids.indexOf(id);
        const next = index + delta;
        if (index < 0 || next < 0 || next >= ids.length) {
            return;
        }
        const swap = ids[index];
        ids[index] = ids[next];
        ids[next] = swap;
        persistOrder(ids);
    }

    async function removeItem(labelId, itemId) {
        if (!itemId || !window.confirm(t("confirmItem"))) {
            return;
        }
        loading(true);
        try {
            await api("DELETE", "ColorDots/labels/" + labelId + "/items/" + itemId);
            await loadReport();
            setStatus(t("removedOne"));
        } catch (err) {
            setStatus(t("notRemoved"));
        } finally {
            loading(false);
        }
    }

    async function removeLabel(id) {
        let count = 0;
        try {
            const usage = await api("GET", "ColorDots/labels/" + id + "/usage");
            count = usage.count != null ? usage.count : usage.Count || 0;
        } catch (err) {
            setStatus(t("usageFailed"));
            return;
        }
        if (!window.confirm(t("confirm").replace("{count}", String(count)))) {
            return;
        }
        loading(true);
        try {
            await api("DELETE", "ColorDots/labels/" + id);
            await load();
            setStatus(t("deleted"));
        } catch (err) {
            setStatus(t("notDeleted"));
            loading(false);
        }
    }

    form.addEventListener("submit", function (event) {
        event.preventDefault();
        loading(true);
        saveAll().then(function () {
            renderLabels();
            setStatus(t("saved"));
            return loadReport();
        }).catch(function () {
            setStatus(t("notSaved"));
        }).finally(function () {
            loading(false);
        });
    });

    add.addEventListener("click", function () {
        loading(true);
        saveAll().then(function () {
            return api("POST", "ColorDots/labels", { name: t("fresh"), color: "#a855f7" });
        }).then(function () {
            return load();
        }).then(function () {
            setStatus(t("added"));
        }).catch(function () {
            setStatus(t("notAdded"));
            loading(false);
        });
    });

    const exportButton = view.querySelector("#jfcd-export");
    if (exportButton) {
        exportButton.addEventListener("click", exportCsv);
    }

    function showPage() {
        locale = uiLocale();
        applyCopy();
        load();
        watchLanguage();
    }

    view.addEventListener("viewshow", showPage);
    view.addEventListener("pageshow", showPage);
    window.addEventListener("pageshow", refreshLocale);
    window.addEventListener("storage", refreshLocale);

    load();
    watchLanguage();
}
