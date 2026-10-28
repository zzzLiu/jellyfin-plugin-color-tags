(function () {
    "use strict";

    var WHITE = { id: 0, name: "White", color: "#f4f4f5", fixed: true, count: 0 };
    var COPY = {
        en: { menu: "Jellyfin Color Tags", notSaved: "Not saved", white: "White", red: "Red", green: "Green", blue: "Blue", fresh: "New label", on: "On", off: "Off", none: "None" },
        "zh-CN": { menu: "Jellyfin 颜色标签", notSaved: "没有保存", white: "白", red: "红", green: "绿", blue: "蓝", fresh: "新标签", on: "已选", off: "未选", none: "无" }
    };
    var STOCK = { 0: ["white", "白"], 1: ["red", "红"], 2: ["green", "绿"], 3: ["blue", "蓝"] };

    var script = document.currentScript || document.querySelector("script[data-plugin='ColorDots']");
    var apiRoot = "/ColorDots";
    if (script && script.src) {
        apiRoot = new URL(script.src, window.location.href).href.replace(/\/client\.js(?:\?.*)?$/, "");
    }

    var labels = [WHITE];
    var labelsReady = false;
    var labelsAt = 0;
    var cache = Object.create(null);
    var pending = Object.create(null);
    var pendingCount = 0;
    var flushTimer = 0;
    var panel = null;
    var openHit = null;
    var toastTimer = 0;
    var viewItemId = "";
    var viewItemAt = 0;
    var sessionItemId = "";
    var sessionItemAt = 0;
    var mediaItemId = "";
    var mediaItemAt = 0;
    var playerItemId = "";
    var playerItemAt = 0;
    var sessionAt = 0;
    var sessionFlight = false;
    var cardPairs = [];
    var syncQueued = false;

    function normId(id) {
        return String(id || "").replace(/-/g, "").toLowerCase();
    }

    function labelById(id) {
        var i;
        for (i = 0; i < labels.length; i++) {
            if (labels[i].id === id) {
                return labels[i];
            }
        }
        return null;
    }

    function normLabel(value) {
        var id = typeof value === "number" ? value : parseInt(value, 10);
        if (!isFinite(id) || id < 0) {
            return 0;
        }
        if (id === 0) {
            return 0;
        }
        if (labelsReady && !labelById(id)) {
            return 0;
        }
        return id;
    }

    function labelIndex(id) {
        var i;
        for (i = 0; i < labels.length; i++) {
            if (labels[i].id === id) {
                return i;
            }
        }
        return 1000 + id;
    }

    function normLabels(value) {
        var raw = [];
        if (Array.isArray(value)) {
            raw = value;
        } else if (typeof value === "string" && value.indexOf(",") !== -1) {
            raw = value.split(",");
        } else if (value != null && value !== "") {
            raw = [value];
        }
        var out = [];
        var seen = Object.create(null);
        var i;
        for (i = 0; i < raw.length; i++) {
            var id = typeof raw[i] === "number" ? raw[i] : parseInt(raw[i], 10);
            if (!isFinite(id) || id <= 0) {
                continue;
            }
            if (labelsReady && !labelById(id)) {
                continue;
            }
            if (seen[id]) {
                continue;
            }
            seen[id] = 1;
            out.push(id);
        }
        out.sort(function (a, b) {
            return labelIndex(a) - labelIndex(b);
        });
        return out;
    }

    function toggled(current, labelId) {
        var next = current.slice();
        var at = next.indexOf(labelId);
        if (at >= 0) {
            next.splice(at, 1);
        } else {
            next.push(labelId);
        }
        return normLabels(next);
    }

    function idsKey(ids) {
        return ids.length ? ids.join(",") : "0";
    }

    function dotFill(ids) {
        var colors = [];
        var i;
        for (i = 0; i < ids.length; i++) {
            var label = labelById(ids[i]);
            if (label && label.id !== 0) {
                colors.push(label.color);
            }
        }
        if (colors.length === 0) {
            return WHITE.color;
        }
        if (colors.length === 1) {
            return colors[0];
        }
        var stops = [];
        for (i = 0; i < colors.length; i++) {
            var start = (i * 100) / colors.length;
            var end = ((i + 1) * 100) / colors.length;
            stops.push(colors[i] + " " + start + "% " + end + "%");
        }
        return "conic-gradient(" + stops.join(", ") + ")";
    }

    function namesFor(ids) {
        if (!ids.length) {
            return text("white");
        }
        var parts = [];
        var i;
        for (i = 0; i < ids.length; i++) {
            parts.push(nameFor(ids[i]));
        }
        return parts.join(", ");
    }

    var userDisplayLanguage = "";

    function isAuto(value) {
        var text = String(value || "").trim().toLowerCase();
        return !text || text === "auto";
    }

    function mapLocale(value) {
        var culture = String(value || "").trim().toLowerCase().replace(/_/g, "-");
        if (culture === "zh" || culture.indexOf("zh-") === 0) {
            return "zh-CN";
        }
        return "en";
    }

    function currentUserIdRaw() {
        var api = window.ApiClient;
        if (api && typeof api.getCurrentUserId === "function") {
            try {
                var id = api.getCurrentUserId();
                if (id) {
                    return String(id);
                }
            } catch (err) {
                /* fall through */
            }
        }
        try {
            var raw = localStorage.getItem("jellyfin_credentials") || localStorage.getItem("servercredentials") || "";
            var creds = raw ? JSON.parse(raw) : null;
            var servers = creds && (creds.Servers || creds.servers);
            if (servers && servers.length && (servers[0].UserId || servers[0].userId)) {
                return String(servers[0].UserId || servers[0].userId);
            }
        } catch (e) {
            return "";
        }
        return "";
    }

    function languageFromStorage(userId) {
        var raw = String(userId || "").trim().toLowerCase();
        var compact = raw.replace(/-/g, "");
        var keys = [];
        var sawOwn = false;
        var i;
        if (raw) {
            keys.push(raw + "-language");
        }
        if (compact && compact !== raw) {
            keys.push(compact + "-language");
        }
        if (/^[0-9a-f]{32}$/.test(compact)) {
            keys.push(compact.slice(0, 8) + "-" + compact.slice(8, 12) + "-" + compact.slice(12, 16) + "-" + compact.slice(16, 20) + "-" + compact.slice(20) + "-language");
        }
        try {
            for (i = 0; i < keys.length; i++) {
                var exact = localStorage.getItem(keys[i]);
                if (exact === null) {
                    continue;
                }
                sawOwn = true;
                if (!isAuto(exact)) {
                    return exact;
                }
            }
            if (sawOwn || !compact) {
                return "";
            }
            for (i = 0; i < localStorage.length; i++) {
                var key = localStorage.key(i) || "";
                if (!/language/i.test(key) || key.replace(/-/g, "").toLowerCase().indexOf(compact) === -1) {
                    continue;
                }
                var stored = localStorage.getItem(key);
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
        var stored = languageFromStorage(currentUserIdRaw());
        if (!isAuto(stored)) {
            return mapLocale(stored);
        }
        var nav = (navigator.languages && navigator.languages[0]) || navigator.language || "";
        if (!isAuto(nav)) {
            return mapLocale(nav);
        }
        var attr = document.documentElement.getAttribute("lang") || "";
        if (!isAuto(attr)) {
            return mapLocale(attr);
        }
        return "en";
    }

    function text(key) {
        var pack = COPY[uiLocale()] || COPY.en;
        return pack[key] || COPY.en[key] || "";
    }

    function isStockName(id, name) {
        var list = STOCK[id];
        if (!list) {
            return false;
        }
        var key = String(name || "").trim().toLowerCase();
        return list.indexOf(key) !== -1;
    }

    function displayName(id, stored) {
        if (!isStockName(id, stored)) {
            return stored || text("fresh");
        }
        if (id === 0) {
            return text("white");
        }
        if (id === 1) {
            return text("red");
        }
        if (id === 2) {
            return text("green");
        }
        return text("blue");
    }

    function nameFor(id) {
        var label = labelById(id);
        return label ? displayName(label.id, label.name) : text("white");
    }

    function session() {
        var api = window.ApiClient;
        if (api && typeof api.accessToken === "function") {
            var token = api.accessToken();
            var userId = typeof api.getCurrentUserId === "function" ? api.getCurrentUserId() : "";
            if (token && userId) {
                return {
                    token: token,
                    userId: normId(userId),
                    deviceId: (typeof api.deviceId === "function" && api.deviceId()) || "colordots"
                };
            }
        }

        try {
            var raw = localStorage.getItem("jellyfin_credentials") || localStorage.getItem("servercredentials");
            if (!raw) {
                return null;
            }
            var creds = JSON.parse(raw);
            var servers = creds.Servers || creds.servers || [];
            var i;
            for (i = 0; i < servers.length; i++) {
                var server = servers[i];
                var access = server.AccessToken || server.accessToken;
                var user = server.UserId || server.userId;
                if (access && user) {
                    return {
                        token: access,
                        userId: normId(user),
                        deviceId: server.Id || server.id || "colordots"
                    };
                }
            }
        } catch (err) {
            return null;
        }

        return null;
    }

    function authHeaders(extra) {
        var current = session();
        if (!current) {
            return null;
        }

        var deviceId = String(current.deviceId).replace(/["\r\n]/g, "").slice(0, 80) || "colordots";
        var headers = {
            Accept: "application/json",
            Authorization: 'MediaBrowser Token="' + current.token + '", Client="ColorDots", Device="Browser", DeviceId="' + deviceId + '", Version="1.4.0"',
            "X-Emby-Token": current.token
        };
        if (extra) {
            var key;
            for (key in extra) {
                if (Object.prototype.hasOwnProperty.call(extra, key)) {
                    headers[key] = extra[key];
                }
            }
        }
        return headers;
    }

    function request(path, options) {
        var headers = authHeaders(options && options.headers);
        if (!headers) {
            return Promise.reject(new Error("no-session"));
        }

        return fetch(apiRoot + path, {
            method: (options && options.method) || "GET",
            headers: headers,
            body: options && options.body,
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

    function refreshLabels(force) {
        if (!force && labelsAt && Date.now() - labelsAt < 30000) {
            return Promise.resolve(labels);
        }

        return request("/labels").then(function (data) {
            var list = (data && (data.labels || data.Labels)) || [];
            var next = [];
            var seen = Object.create(null);
            var hasWhite = false;
            var i;
            for (i = 0; i < list.length; i++) {
                var row = list[i] || {};
                var id = parseInt(row.id != null ? row.id : row.Id, 10);
                if (!isFinite(id) || id < 0 || seen[id]) {
                    continue;
                }
                var color = String(row.color || row.Color || "").trim();
                if (!/^#[0-9a-fA-F]{6}$/.test(color)) {
                    color = id === 0 ? WHITE.color : "#888888";
                }
                var name = String(row.name || row.Name || "").replace(/[\u0000-\u001F\u007F]/g, "").trim().slice(0, 24);
                if (!name) {
                    name = id === 0 ? "White" : "New label";
                }
                seen[id] = 1;
                if (id === 0) {
                    hasWhite = true;
                }
                var count = parseInt(row.count != null ? row.count : row.Count, 10);
                next.push({ id: id, name: name, color: color.toLowerCase(), fixed: id === 0, count: isFinite(count) && count > 0 ? count : 0 });
            }
            labels = hasWhite ? next : [WHITE].concat(next);
            labelsReady = true;
            var key;
            for (key in cache) {
                if (Object.prototype.hasOwnProperty.call(cache, key)) {
                    cache[key] = normLabels(cache[key]);
                }
            }
            labelsAt = Date.now();
            applyCached();
            return labels;
        }).catch(function () {
            return labels;
        });
    }

    function inDashboard() {
        var href = (location.pathname || "") + (location.hash || "") + (location.search || "");
        return /dashboard|configurationpage|metadataeditor|edititem/i.test(href);
    }

    function onVideoRoute() {
        var blob = (location.pathname || "") + (location.search || "") + (location.hash || "");
        return /(?:^|[#!\/])video(?:[\/?#]|$)/i.test(blob);
    }

    function idFromUrl() {
        var sources = [location.hash || "", location.search || ""];
        var pattern = /(?:^|[?&#])(?:itemid|id)=([0-9a-fA-F-]{32,36})/i;
        var i;
        for (i = 0; i < sources.length; i++) {
            var match = pattern.exec(sources[i]);
            if (match) {
                return normId(match[1]);
            }
        }
        return "";
    }

    function idFromMedia() {
        var nodes = document.querySelectorAll("video, audio");
        var i;
        for (i = 0; i < nodes.length; i++) {
            var src = nodes[i].currentSrc || nodes[i].src || "";
            var match = /\/videos\/([0-9a-fA-F-]{32,36})(?:\/|\?|$)/i.exec(src);
            if (match) {
                return normId(match[1]);
            }
        }
        return "";
    }

    function idFromViewDetail(event) {
        var detail = event && event.detail;
        var params = detail && (detail.params || detail);
        if (!params) {
            return "";
        }
        return normId(params.id || params.Id || params.itemId || params.ItemId || "");
    }

    function idFromPlaybackManager() {
        var managers = [window.playbackManager, window.PlaybackManager];
        var i;
        for (i = 0; i < managers.length; i++) {
            var manager = managers[i];
            if (!manager || typeof manager.currentItem !== "function") {
                continue;
            }
            try {
                var item = manager.currentItem(0) || manager.currentItem();
                var id = item && (item.Id || item.id);
                if (id) {
                    return normId(id);
                }
            } catch (err) {
                return "";
            }
        }
        return "";
    }

    function currentItemId() {
        var urlId = idFromUrl();
        if (urlId && onVideoRoute()) {
            return urlId;
        }

        var playerId = idFromPlaybackManager();
        if (playerId !== playerItemId) {
            playerItemId = playerId;
            if (playerId) {
                playerItemAt = Date.now();
            }
        }
        var mediaId = idFromMedia();
        if (mediaId !== mediaItemId) {
            mediaItemId = mediaId;
            if (mediaId) {
                mediaItemAt = Date.now();
            }
        }

        var best = "";
        var bestAt = -1;
        function consider(id, at) {
            if (id && at > bestAt) {
                best = id;
                bestAt = at;
            }
        }
        consider(viewItemId, viewItemAt);
        consider(playerItemId, playerItemAt);
        consider(mediaItemId, mediaItemAt);
        consider(sessionItemId, sessionItemAt);
        return best || urlId;
    }

    function deviceKey() {
        try {
            return localStorage.getItem("_deviceId2") || "";
        } catch (err) {
            return "";
        }
    }

    function pickSessionItem(list, current) {
        if (!list || !list.length) {
            return "";
        }
        var wantUser = current && current.userId;
        var wantDevice = deviceKey();
        var fallback = "";
        var i;
        for (i = 0; i < list.length; i++) {
            var row = list[i] || {};
            var item = row.NowPlayingItem || row.nowPlayingItem;
            var itemId = item && (item.Id || item.id);
            if (!itemId) {
                continue;
            }
            var userId = normId(row.UserId || row.userId || "");
            if (wantUser && userId && userId !== wantUser) {
                continue;
            }
            var deviceId = String(row.DeviceId || row.deviceId || "");
            if (wantDevice && deviceId === wantDevice) {
                return normId(itemId);
            }
            if (!fallback) {
                fallback = normId(itemId);
            }
        }
        return fallback;
    }

    function refreshSession() {
        if (sessionFlight || (sessionAt && Date.now() - sessionAt < 1500)) {
            return;
        }
        if (!session()) {
            return;
        }
        sessionFlight = true;
        sessionAt = Date.now();
        var started = sessionAt;
        var root = apiRoot.replace(/\/ColorDots\/?$/, "");
        var headers = authHeaders();
        fetch(root + "/Sessions", {
            headers: headers,
            credentials: "same-origin",
            cache: "no-store"
        }).then(function (response) {
            if (!response.ok) {
                throw new Error(String(response.status));
            }
            return response.json();
        }).then(function (list) {
            sessionFlight = false;
            var next = pickSessionItem(list, session());
            if (viewItemId && viewItemAt > started && next && next !== viewItemId) {
                return;
            }
            if (next !== sessionItemId) {
                sessionItemId = next;
                sessionItemAt = Date.now();
                sync();
            }
        }).catch(function () {
            sessionFlight = false;
        });
    }

    function shown(el) {
        var node = el;
        while (node && node.nodeType === 1) {
            if (node.classList.contains("hide") || node.hidden) {
                return false;
            }
            var style = window.getComputedStyle(node);
            if (style.display === "none" || style.visibility === "hidden") {
                return false;
            }
            node = node.parentElement;
        }
        return !!el;
    }

    function mountRoot() {
        var pages = document.querySelectorAll("[id='videoOsdPage']");
        var i;
        for (i = 0; i < pages.length; i++) {
            if (shown(pages[i])) {
                return pages[i];
            }
        }
        var bars = document.querySelectorAll(".videoOsdBottom");
        for (i = 0; i < bars.length; i++) {
            if (shown(bars[i])) {
                return bars[i].closest("[id='videoOsdPage']") || bars[i];
            }
        }
        return null;
    }

    function collectFavorites(scope) {
        var selectors = [
            ".btnUserRating",
            "button[is='emby-ratingbutton']",
            ".emby-ratingbutton",
            "button.favorite",
            ".btnFavorite",
            "button[data-action='rating']"
        ];
        var found = [];
        var i;
        var j;
        for (i = 0; i < selectors.length; i++) {
            var list = scope.querySelectorAll(selectors[i]);
            for (j = 0; j < list.length; j++) {
                if (list[j].id !== "jfcd-player-hit" && found.indexOf(list[j]) === -1) {
                    found.push(list[j]);
                }
            }
        }
        var icons = scope.querySelectorAll(".material-icons, .md-icon, span[class*='favorite']");
        for (i = 0; i < icons.length; i++) {
            var text = (icons[i].textContent || "").replace(/\s+/g, "");
            var cls = String(icons[i].className || "");
            if (text !== "favorite" && text !== "favorite_border" && !/(^|\s)favorite(_border)?(\s|$)/.test(cls)) {
                continue;
            }
            var node = icons[i].closest("button") || icons[i].parentElement;
            if (node && node.id !== "jfcd-player-hit" && found.indexOf(node) === -1) {
                found.push(node);
            }
        }
        return found;
    }

    function pickFavorite(nodes) {
        var i;
        var fallback = null;
        for (i = 0; i < nodes.length; i++) {
            if (shown(nodes[i])) {
                return nodes[i];
            }
            if (!fallback && nodes[i].parentElement && shown(nodes[i].parentElement)) {
                fallback = nodes[i];
            }
        }
        return fallback;
    }

    function favoriteButton(root) {
        var pick = pickFavorite(collectFavorites(root || document));
        if (pick || !root || root === document) {
            return pick;
        }
        return pickFavorite(collectFavorites(document));
    }

    function buttonRow(root) {
        var scope = root || document;
        return scope.querySelector(".videoOsdBottom .osdControls .buttons")
            || scope.querySelector(".videoOsdBottom .buttons")
            || scope.querySelector(".osdControls .buttons");
    }

    function placeInRow(hit, root) {
        var fav = favoriteButton(root);
        var parent = null;
        var before = null;
        if (fav && fav.parentElement && fav !== hit && !hit.contains(fav)) {
            parent = fav.parentElement;
            before = fav;
        } else {
            parent = buttonRow(root) || buttonRow(document);
        }
        if (!parent || parent === hit || hit.contains(parent)) {
            return false;
        }
        var already = fav
            ? hit.parentElement === parent && hit.nextElementSibling === fav
            : hit.parentElement === parent;
        if (!already) {
            parent.insertBefore(hit, before);
        }
        return !!hit.parentElement;
    }

    function barFaded(hit) {
        var bar = hit.closest(".videoOsdBottom");
        if (!bar) {
            return false;
        }
        if (bar.classList.contains("videoOsdBottom-hidden")) {
            return true;
        }
        return window.getComputedStyle(bar).opacity === "0";
    }

    function installStyles() {
        if (document.getElementById("jfcd-style")) {
            return;
        }

        var style = document.createElement("style");
        style.id = "jfcd-style";
        style.textContent = [
            "button.jfcd-hit{all:unset!important;position:relative!important;z-index:2!important;flex:0 0 auto!important;align-self:center!important;width:40px!important;height:40px!important;min-width:40px!important;min-height:40px!important;display:inline-flex!important;align-items:center!important;justify-content:center!important;padding:0!important;margin:0!important;border:0!important;background:transparent!important;cursor:pointer!important;pointer-events:auto!important;box-sizing:border-box!important;touch-action:manipulation!important;vertical-align:middle!important;}",
            "html.layout-mobile button.jfcd-hit,html.layout-tv button.jfcd-hit{width:48px!important;height:48px!important;min-width:48px!important;min-height:48px!important;}",
            "@media (pointer:coarse){button.jfcd-hit{width:48px!important;height:48px!important;min-width:48px!important;min-height:48px!important;}}",
            "#jfcd-card-layer{position:fixed;inset:0;z-index:30;pointer-events:none;background:none;}",
            "button.jfcd-card-hit{position:fixed;z-index:31;box-sizing:border-box;margin:0;padding:0;border:0;background:transparent;box-shadow:none;display:inline-flex;align-items:center;justify-content:center;cursor:pointer;pointer-events:auto;touch-action:manipulation;color:inherit;font:inherit;appearance:none;-webkit-appearance:none;}",
            "button.jfcd-card-hit[hidden]{display:none!important;}",
            ".playerStats{z-index:1001!important;}",
            ".playerStats .playerStats-closeButton{z-index:2!important;pointer-events:auto!important;}",
            ".card.jfcd-pin .cardOverlayContainer,.card.jfcd-pin .cardOverlayButton-hover{opacity:1;}",
            "button.jfcd-hit .jfcd-dot,button.jfcd-card-hit .jfcd-dot{width:16px!important;height:16px!important;border-radius:50%!important;display:block!important;box-shadow:none!important;border:0!important;outline:none!important;pointer-events:none!important;}",
            ".jfcd-panel{position:fixed!important;z-index:100001!important;display:flex!important;flex-direction:column!important;flex-wrap:nowrap!important;align-items:stretch!important;justify-content:flex-start!important;min-width:320px!important;margin:0!important;padding:6px!important;border-radius:12px!important;background:rgba(22,22,24,.96)!important;box-shadow:0 10px 28px rgba(0,0,0,.4),0 0 0 1px rgba(255,255,255,.08)!important;color:#fff!important;font-family:inherit!important;}",
            ".jfcd-columns{display:flex!important;flex-direction:row!important;align-items:stretch!important;gap:8px!important;}",
            ".jfcd-col{display:flex!important;flex-direction:column!important;flex:1 1 150px!important;min-width:148px!important;}",
            ".jfcd-col-title{padding:4px 10px 2px!important;font-size:11px!important;letter-spacing:.04em!important;opacity:.62!important;}",
            ".jfcd-empty{padding:8px 10px 10px!important;font-size:13px!important;opacity:.55!important;}",
            ".jfcd-panel[hidden]{display:none!important;}",
            "button.jfcd-choice{all:unset!important;display:flex!important;align-items:center!important;gap:10px!important;width:100%!important;box-sizing:border-box!important;padding:8px 10px!important;border-radius:8px!important;cursor:pointer!important;color:rgba(255,255,255,.72)!important;font-size:13px!important;font-weight:400!important;line-height:1.2!important;}",
            "button.jfcd-choice:hover,button.jfcd-choice:focus{background:rgba(255,255,255,.08)!important;outline:none!important;}",
            "button.jfcd-choice.is-selected{color:rgba(255,255,255,.92)!important;}",
            "button.jfcd-choice .jfcd-dot{width:12px!important;height:12px!important;border-radius:50%!important;flex:0 0 auto!important;box-shadow:none!important;border:0!important;outline:none!important;}",
            "button.jfcd-choice .jfcd-count{flex:0 0 auto!important;margin-left:auto!important;min-width:1.15em!important;font-variant-numeric:tabular-nums!important;font-size:12px!important;opacity:.72!important;text-align:right!important;}",
            ".jfcd-choice-name{flex:1 1 auto!important;}",
            ".jfcd-error{margin:2px 8px 6px!important;font-size:12px!important;color:#ffb4b4!important;}",
            ".dashboardDocument a.jfcd-drawer-link{display:none!important;}",
            ".jfcd-toast{position:fixed!important;z-index:100002!important;left:50%!important;bottom:24px!important;transform:translateX(-50%)!important;padding:8px 12px!important;border-radius:999px!important;background:rgba(22,22,24,.94)!important;color:#fff!important;font-size:13px!important;box-shadow:0 8px 20px rgba(0,0,0,.35)!important;}"
        ].join("");
        document.head.appendChild(style);
    }

    function paint(hit, labelIds) {
        var dot = hit.querySelector(".jfcd-dot");
        var next = normLabels(labelIds);
        if (!dot) {
            return;
        }
        dot.style.background = dotFill(next);
        dot.style.boxShadow = "none";
        dot.style.border = "0";
        hit.setAttribute("aria-label", namesFor(next));
        hit.dataset.color = idsKey(next);
    }

    function stop(event) {
        event.preventDefault();
        event.stopPropagation();
    }

    function remember(id) {
        var key = normId(id);
        if (!key || Object.prototype.hasOwnProperty.call(cache, key) || pending[key]) {
            return;
        }
        pending[key] = 1;
        pendingCount += 1;
    }

    function scheduleFlush() {
        if (flushTimer) {
            return;
        }
        flushTimer = window.setTimeout(flush, 80);
    }

    function flush() {
        flushTimer = 0;
        var ids = Object.keys(pending);
        if (!ids.length) {
            return;
        }

        pending = Object.create(null);
        pendingCount = 0;
        var batch = ids.slice(0, 200);
        var rest = ids.slice(200);
        var r;
        for (r = 0; r < rest.length; r++) {
            pending[rest[r]] = 1;
            pendingCount += 1;
        }
        if (pendingCount) {
            scheduleFlush();
        }

        request("/colors", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ ids: batch })
        }).then(function (data) {
            var colors = (data && (data.colors || data.Colors)) || {};
            var i;
            for (i = 0; i < batch.length; i++) {
                var id = batch[i];
                if (!Object.prototype.hasOwnProperty.call(cache, id)) {
                    var raw = Object.prototype.hasOwnProperty.call(colors, id) ? colors[id] : colors[id.toUpperCase()];
                    cache[id] = normLabels(raw == null || raw === "" ? [] : raw);
                }
            }
            applyCached();
        }).catch(function () {
            var j;
            for (j = 0; j < batch.length; j++) {
                if (!Object.prototype.hasOwnProperty.call(cache, batch[j])) {
                    cache[batch[j]] = [];
                }
            }
            applyCached();
        });
    }

    function eachHit(fn) {
        var hits = document.querySelectorAll("#jfcd-player-hit, button.jfcd-card-hit");
        var i;
        for (i = 0; i < hits.length; i++) {
            fn(hits[i]);
        }
    }

    function paintMatches(id, color) {
        eachHit(function (hit) {
            if (hit.dataset.itemId === id) {
                paint(hit, color);
            }
        });
    }

    function applyCached() {
        eachHit(function (hit) {
            var id = hit.dataset.itemId;
            if (id && Object.prototype.hasOwnProperty.call(cache, id)) {
                paint(hit, cache[id]);
            }
        });
    }

    function iconKind(btn) {
        if (!btn || btn.classList.contains("jfcd-card-hit") || btn.id === "jfcd-player-hit") {
            return "";
        }
        if (btn.matches("button[is='emby-playstatebutton'], button.emby-playstatebutton")) {
            return "played";
        }
        if (btn.matches("button[is='emby-ratingbutton'], button.btnUserRating, button.emby-ratingbutton")) {
            return "favorite";
        }
        if ((btn.getAttribute("data-action") || "") === "menu") {
            return "more";
        }
        var nodes = btn.querySelectorAll(".material-icons, .md-icon, span, svg, path");
        var i;
        var text;
        var draw;
        for (i = 0; i < nodes.length; i++) {
            text = (nodes[i].textContent || "").replace(/\s+/g, "");
            if (text === "check" || text === "done" || text === "check_box" || text === "check_circle") {
                return "played";
            }
            if (text === "favorite" || text === "favorite_border") {
                return "favorite";
            }
            if (text === "more_vert" || text === "more_horiz") {
                return "more";
            }
            draw = (nodes[i].getAttribute && nodes[i].getAttribute("d")) || "";
            if (draw.indexOf("M9 16.17") === 0) {
                return "played";
            }
            if (draw.indexOf("M12 21.35") === 0 || draw.indexOf("m12 21.35") === 0) {
                return "favorite";
            }
            if (draw.indexOf("M12 8c1.1") === 0) {
                return "more";
            }
            text = nodes[i].getAttribute && nodes[i].getAttribute("data-testid");
            if (text === "CheckIcon") {
                return "played";
            }
            if (text && text.indexOf("Favorite") === 0) {
                return "favorite";
            }
            if (text && text.indexOf("More") === 0) {
                return "more";
            }
        }
        return "";
    }

    function isCastBar(bar) {
        if (!bar || !bar.closest) {
            return false;
        }
        if (bar.closest("#castCollapsible, #guestCastCollapsible, #castContent, #guestCastContent")) {
            return true;
        }
        var card = bar.closest(".card, .listItem");
        var type = card ? String(card.getAttribute("data-type") || "").toLowerCase() : "";
        return type === "person" || type === "actor";
    }

    function cardItemId(bar) {
        var node = bar;
        while (node && node.nodeType === 1) {
            var raw = node.getAttribute("data-id") || "";
            if (/^[0-9a-fA-F-]{32,36}$/.test(raw)) {
                return normId(raw);
            }
            node = node.parentElement;
        }
        return "";
    }

    function cardMore(bar) {
        var buttons = bar.querySelectorAll("button");
        var i;
        var more = null;
        var moreRight = -Infinity;
        var rightmost = null;
        var right = -Infinity;
        for (i = 0; i < buttons.length; i++) {
            var btn = buttons[i];
            if (!btn || btn.classList.contains("jfcd-card-hit") || btn.id === "jfcd-player-hit") {
                continue;
            }
            var rect = btn.getBoundingClientRect();
            if (rect.width < 4 || rect.height < 4) {
                continue;
            }
            if (iconKind(btn) === "more" && rect.right >= moreRight) {
                more = btn;
                moreRight = rect.right;
            }
            if (rect.right >= right) {
                right = rect.right;
                rightmost = btn;
            }
        }
        return more || rightmost;
    }

    function wireHit(hit) {
        var fromPointer = false;
        hit.addEventListener("pointerdown", function (event) {
            fromPointer = false;
            event.stopPropagation();
        });
        ["mousedown", "touchstart", "dblclick", "contextmenu"].forEach(function (name) {
            hit.addEventListener(name, function (event) {
                event.stopPropagation();
                if (name === "dblclick" || name === "contextmenu") {
                    event.preventDefault();
                }
            });
        });
        function toggleFromPointer(event) {
            event.preventDefault();
            event.stopPropagation();
            if (fromPointer) {
                return;
            }
            fromPointer = true;
            togglePanel(hit);
        }
        hit.addEventListener("pointerup", toggleFromPointer);
        hit.addEventListener("touchend", toggleFromPointer);
        if (hit.classList.contains("jfcd-card-hit")) {
            hit.addEventListener("pointerover", function () {
                setPin(hit, true);
            });
            hit.addEventListener("pointerout", function (event) {
                var bar = barForHit(hit);
                var card = bar && bar.closest(".card");
                var next = event.relatedTarget;
                if (openHit === hit) {
                    return;
                }
                if (card && next && card.contains(next)) {
                    setPin(hit, false);
                    return;
                }
                setPin(hit, false);
            });
        }
        hit.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();
            if (fromPointer) {
                fromPointer = false;
                return;
            }
            togglePanel(hit);
        });
    }

    function bindHit(hit, id) {
        if (!id || hit.dataset.itemId === id) {
            if (id && Object.prototype.hasOwnProperty.call(cache, id)) {
                paint(hit, cache[id]);
            }
            return;
        }
        if (openHit === hit) {
            closePanel();
        }
        hit.dataset.itemId = id;
        paint(hit, Object.prototype.hasOwnProperty.call(cache, id) ? cache[id] : 0);
        if (!Object.prototype.hasOwnProperty.call(cache, id)) {
            remember(id);
        }
    }

    function removeCardHits() {
        var i;
        for (i = 0; i < cardPairs.length; i++) {
            if (openHit === cardPairs[i].hit) {
                closePanel();
            }
            cardPairs[i].hit.remove();
        }
        cardPairs = [];
    }

    function cardLayer() {
        var layer = document.getElementById("jfcd-card-layer");
        if (!layer) {
            layer = document.createElement("div");
            layer.id = "jfcd-card-layer";
            document.body.appendChild(layer);
        }
        return layer;
    }

    function pairFor(bar) {
        var i;
        for (i = 0; i < cardPairs.length; i++) {
            if (cardPairs[i].bar === bar) {
                return cardPairs[i];
            }
        }
        var hit = document.createElement("button");
        hit.type = "button";
        hit.className = "jfcd-card-hit";
        hit.hidden = true;
        hit.setAttribute("aria-haspopup", "menu");
        hit.setAttribute("aria-expanded", "false");
        var dot = document.createElement("span");
        dot.className = "jfcd-dot";
        hit.appendChild(dot);
        paint(hit, 0);
        wireHit(hit);
        cardLayer().appendChild(hit);
        var pair = { bar: bar, hit: hit };
        cardPairs.push(pair);
        return pair;
    }

    function barForHit(hit) {
        var i;
        for (i = 0; i < cardPairs.length; i++) {
            if (cardPairs[i].hit === hit) {
                return cardPairs[i].bar;
            }
        }
        return null;
    }

    function setPin(hit, on) {
        var bar = barForHit(hit);
        var card = bar && bar.closest(".card");
        if (!card) {
            return;
        }
        card.classList.toggle("jfcd-pin", !!on);
    }

    function rowShown(bar, anchor) {
        if (!bar || !anchor || !anchor.isConnected) {
            return false;
        }
        var card = bar.closest(".card");
        var pinned = !!(card && card.classList.contains("jfcd-pin"));
        var overlay = bar.closest(".cardOverlayContainer");
        if (!pinned && overlay) {
            var overlayStyle = window.getComputedStyle(overlay);
            if (overlayStyle.display === "none" || overlayStyle.visibility === "hidden" || parseFloat(overlayStyle.opacity) < 0.35) {
                return false;
            }
        }
        var anchorStyle = window.getComputedStyle(anchor);
        if (anchorStyle.display === "none" || anchorStyle.visibility === "hidden" || (!pinned && parseFloat(anchorStyle.opacity) < 0.35)) {
            return false;
        }
        var box = anchor.getBoundingClientRect();
        return box.width >= 8 && box.height >= 8 && box.bottom > 0 && box.top < window.innerHeight && box.right > 0 && box.left < window.innerWidth;
    }

    function intersects(a, b) {
        return a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top;
    }

    function placeAbove(hit, more) {
        var box = more.getBoundingClientRect();
        var card = more.closest(".card") || more.closest(".cardScalable") || more.closest(".cardBox");
        var bounds = card ? card.getBoundingClientRect() : null;
        var size = Math.max(28, Math.round(Math.min(box.width, box.height) || 28));
        if (bounds && bounds.width > 0 && size > bounds.width) {
            size = Math.floor(bounds.width);
        }
        if (bounds && bounds.height > 0 && size > bounds.height) {
            size = Math.floor(bounds.height);
        }
        if (size < 1) {
            size = 1;
        }
        var left = Math.round(box.left + (box.width - size) / 2);
        var top = Math.round(box.top - 4 - size);
        if (bounds && bounds.width >= 8 && bounds.height >= 8) {
            var minLeft = Math.round(bounds.left);
            var maxLeft = Math.round(bounds.right - size);
            var minTop = Math.round(bounds.top);
            var maxTop = Math.round(Math.min(bounds.bottom - size, box.top - 4 - size));
            if (maxLeft < minLeft) {
                left = minLeft;
            } else {
                if (left < minLeft) {
                    left = minLeft;
                }
                if (left > maxLeft) {
                    left = maxLeft;
                }
            }
            if (top < minTop) {
                top = minTop;
            }
            if (maxTop >= minTop && top > maxTop) {
                top = maxTop;
            }
        }
        var blockers = [];
        if (card) {
            var play = card.querySelector(".cardOverlayFab-primary, .cardOverlayButton-centered");
            if (play) {
                blockers.push(play.getBoundingClientRect());
            }
            var bar = more.closest(".cardOverlayButton-br") || more.parentElement;
            if (bar) {
                var siblings = bar.querySelectorAll("button");
                var i;
                for (i = 0; i < siblings.length; i++) {
                    if (siblings[i] !== more && iconKind(siblings[i]) !== "more") {
                        blockers.push(siblings[i].getBoundingClientRect());
                    }
                }
            }
        }
        var rect = { left: left, top: top, right: left + size, bottom: top + size };
        var guard = 0;
        while (guard < 8) {
            var hitBlocker = false;
            var b;
            for (b = 0; b < blockers.length; b++) {
                if (intersects(rect, blockers[b])) {
                    hitBlocker = true;
                    break;
                }
            }
            if (!hitBlocker) {
                break;
            }
            var nextTop = rect.top - 4;
            if (!bounds || nextTop >= bounds.top) {
                rect.top = nextTop;
                rect.bottom = rect.top + size;
            } else if (size > 28) {
                size = 28;
                left = Math.round(box.left + (box.width - size) / 2);
                if (bounds) {
                    left = Math.max(Math.round(bounds.left), Math.min(left, Math.round(bounds.right - size)));
                }
                rect.left = left;
                rect.right = left + size;
                rect.top = Math.round(box.top - 4 - size);
                if (bounds && rect.top < bounds.top) {
                    rect.top = Math.round(bounds.top);
                }
                rect.bottom = rect.top + size;
            } else {
                break;
            }
            guard += 1;
        }
        hit.hidden = false;
        hit.style.width = size + "px";
        hit.style.height = size + "px";
        hit.style.left = rect.left + "px";
        hit.style.top = rect.top + "px";
    }

    function syncCards() {
        var bars = document.querySelectorAll("div.cardOverlayButton-br");
        var live = [];
        var i;
        for (i = 0; i < bars.length; i++) {
            var bar = bars[i];
            if (!bar.isConnected || bar.closest("#videoOsdPage") || isCastBar(bar)) {
                continue;
            }
            var anchor = cardMore(bar);
            if (!anchor) {
                continue;
            }
            var pair = pairFor(bar);
            live.push(pair);
            bindHit(pair.hit, cardItemId(bar));
            if (rowShown(bar, anchor) || openHit === pair.hit) {
                placeAbove(pair.hit, anchor);
            } else {
                pair.hit.hidden = true;
            }
        }
        var kept = [];
        for (i = 0; i < cardPairs.length; i++) {
            if (live.indexOf(cardPairs[i]) === -1 || !cardPairs[i].bar.isConnected) {
                if (openHit === cardPairs[i].hit) {
                    closePanel();
                }
                setPin(cardPairs[i].hit, false);
                cardPairs[i].hit.remove();
            } else {
                kept.push(cardPairs[i]);
            }
        }
        cardPairs = kept;
    }

    function stripStrayHits() {
        var hits = document.querySelectorAll("button.jfcd-hit");
        var i;
        for (i = 0; i < hits.length; i++) {
            if (hits[i].id !== "jfcd-player-hit") {
                hits[i].remove();
            }
        }
    }

    function removeHit() {
        var hit = document.getElementById("jfcd-player-hit");
        if (openHit && hit && openHit === hit) {
            closePanel();
        }
        if (hit) {
            hit.remove();
        }
    }

    function ensureHit(root) {
        var hit = document.getElementById("jfcd-player-hit");
        if (hit) {
            return placeInRow(hit, root) ? hit : null;
        }

        hit = document.createElement("button");
        hit.type = "button";
        hit.id = "jfcd-player-hit";
        hit.className = "jfcd-hit";
        hit.setAttribute("aria-haspopup", "menu");
        hit.setAttribute("aria-expanded", "false");
        var dot = document.createElement("span");
        dot.className = "jfcd-dot";
        hit.appendChild(dot);
        paint(hit, 0);
        wireHit(hit);

        if (!placeInRow(hit, root)) {
            hit.remove();
            return null;
        }
        return hit;
    }

    function syncPlayer() {
        var root = mountRoot();
        if (!root) {
            var stale = document.getElementById("jfcd-player-hit");
            if (stale && !shown(stale)) {
                removeHit();
            }
            return;
        }

        var hit = ensureHit(root);
        if (!hit) {
            return;
        }
        if (openHit === hit && panel && !panel.hidden) {
            if (barFaded(hit)) {
                closePanel();
            } else {
                place(hit);
            }
        }

        if (!sessionItemId || Date.now() - sessionAt > 1500) {
            refreshSession();
        }
        var id = currentItemId();
        if (id) {
            bindHit(hit, id);
        }
    }

    function playbackSurface() {
        return onVideoRoute() || !!mountRoot();
    }

    function sync() {
        stripStrayHits();
        paintDrawerLink();
        if (inDashboard()) {
            closePanel();
            removeHit();
            removeCardHits();
            return;
        }

        syncPlayer();
        if (playbackSurface()) {
            removeCardHits();
        } else {
            syncCards();
        }
        if (pendingCount) {
            scheduleFlush();
        }
    }

    function ensurePanel() {
        if (panel) {
            return panel;
        }

        panel = document.createElement("div");
        panel.className = "jfcd-panel";
        panel.hidden = true;
        panel.setAttribute("role", "menu");
        document.body.appendChild(panel);
        panel.addEventListener("pointerdown", function (event) {
            event.stopPropagation();
        });
        panel.addEventListener("click", stop);
        return panel;
    }

    function closePanel() {
        if (!panel) {
            return;
        }
        panel.hidden = true;
        var hit = openHit;
        if (hit) {
            hit.setAttribute("aria-expanded", "false");
        }
        openHit = null;
        if (hit && hit.classList.contains("jfcd-card-hit")) {
            var bar = barForHit(hit);
            var card = bar && bar.closest(".card");
            if (!card || (!card.matches(":hover") && !hit.matches(":hover"))) {
                setPin(hit, false);
            }
        }
    }

    function showToast(text) {
        var toast = document.getElementById("jfcd-toast");
        if (!toast) {
            toast = document.createElement("div");
            toast.id = "jfcd-toast";
            toast.className = "jfcd-toast";
            document.body.appendChild(toast);
        }
        toast.textContent = text;
        window.clearTimeout(toastTimer);
        toastTimer = window.setTimeout(function () {
            toast.remove();
        }, 2600);
    }

    function choiceRow(hit, label, selected) {
        var row = document.createElement("button");
        row.type = "button";
        row.className = "jfcd-choice" + (selected ? " is-selected" : "");
        row.dataset.label = String(label.id);
        row.setAttribute("role", "menuitemcheckbox");
        row.setAttribute("aria-checked", selected ? "true" : "false");
        var dot = document.createElement("span");
        dot.className = "jfcd-dot";
        dot.style.background = label.color;
        dot.style.boxShadow = "none";
        dot.style.border = "0";
        var name = document.createElement("span");
        name.className = "jfcd-choice-name";
        name.textContent = displayName(label.id, label.name);
        row.appendChild(dot);
        row.appendChild(name);
        if (label.count > 0) {
            var count = document.createElement("span");
            count.className = "jfcd-count";
            count.textContent = String(label.count);
            row.appendChild(count);
        }
        row.addEventListener("click", function (event) {
            stop(event);
            choose(hit, label.id);
        });
        return row;
    }

    function labelColumn(hit, title, applied, selectedIds) {
        var col = document.createElement("div");
        col.className = "jfcd-col";
        var heading = document.createElement("div");
        heading.className = "jfcd-col-title";
        heading.textContent = title;
        col.appendChild(heading);
        var shown = 0;
        var i;
        for (i = 0; i < labels.length; i++) {
            var label = labels[i];
            if (!label || label.id === 0) {
                continue;
            }
            var selected = selectedIds.indexOf(label.id) !== -1;
            if (selected !== applied) {
                continue;
            }
            shown += 1;
            col.appendChild(choiceRow(hit, label, selected));
        }
        if (!shown) {
            var empty = document.createElement("div");
            empty.className = "jfcd-empty";
            empty.textContent = text("none");
            col.appendChild(empty);
        }
        return col;
    }

    function renderPanel(hit) {
        var menu = ensurePanel();
        menu.classList.remove("jfcd-from-player");
        menu.textContent = "";
        var current = normLabels(hit.dataset.color || "");
        var columns = document.createElement("div");
        columns.className = "jfcd-columns";
        columns.appendChild(labelColumn(hit, text("on"), true, current));
        columns.appendChild(labelColumn(hit, text("off"), false, current));
        menu.appendChild(columns);
    }

    function place(hit) {
        var menu = ensurePanel();
        menu.hidden = false;
        menu.style.left = "0px";
        menu.style.top = "0px";
        var rect = hit.getBoundingClientRect();
        var width = menu.offsetWidth;
        var height = menu.offsetHeight;
        var left = rect.left;
        var top = rect.bottom + 6;
        if (left + width > window.innerWidth - 8) {
            left = window.innerWidth - width - 8;
        }
        if (top + height > window.innerHeight - 8) {
            top = rect.top - height - 6;
        }
        if (left < 8) {
            left = 8;
        }
        if (top < 8) {
            top = 8;
        }
        menu.style.left = left + "px";
        menu.style.top = top + "px";
    }

    function togglePanel(hit) {
        if (openHit === hit && panel && !panel.hidden) {
            closePanel();
            return;
        }

        openHit = hit;
        hit.setAttribute("aria-expanded", "true");
        refreshLabels(true).then(function () {
            if (openHit !== hit) {
                return;
            }
            renderPanel(hit);
            place(hit);
            var selected = panel.querySelector(".is-selected") || panel.querySelector("button");
            if (selected) {
                selected.focus();
            }
        });
    }

    function focusChoice(labelId) {
        if (!panel) {
            return;
        }
        var button = panel.querySelector('button[data-label="' + labelId + '"]');
        if (button) {
            button.focus();
        }
    }

    function choose(hit, labelId) {
        var id = hit.dataset.itemId || currentItemId();
        var label = normLabel(labelId);
        if (!id || label === 0) {
            refreshSession();
            showToast(text("notSaved"));
            return;
        }
        var previous = normLabels(hit.dataset.color || "");
        var next = toggled(previous, label);
        hit.dataset.itemId = id;
        cache[id] = next;
        paintMatches(id, next);
        if (openHit === hit && panel && !panel.hidden) {
            renderPanel(hit);
            place(hit);
            focusChoice(label);
        }
        request("/items/" + id, {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ label: label })
        }).then(function (data) {
            var confirmed = data && (data.labels || data.Labels);
            if (!confirmed) {
                return;
            }
            cache[id] = normLabels(confirmed);
            paintMatches(id, cache[id]);
            if (openHit === hit && panel && !panel.hidden) {
                renderPanel(hit);
                place(hit);
                focusChoice(label);
            }
        }).catch(function () {
            cache[id] = previous;
            paintMatches(id, previous);
            if (openHit === hit && panel && !panel.hidden) {
                renderPanel(hit);
                place(hit);
                focusChoice(label);
            }
            showToast(text("notSaved"));
        });
    }

    function videoViewFrom(target) {
        if (!target || target.nodeType !== 1) {
            return null;
        }
        if (target.id === "videoOsdPage" || target.classList.contains("videoOsdBottom")) {
            return target;
        }
        if (target.closest) {
            var closest = target.closest("#videoOsdPage, .videoOsdBottom");
            if (closest) {
                return closest;
            }
        }
        if (target.querySelector) {
            return target.querySelector("#videoOsdPage, .videoOsdBottom");
        }
        return null;
    }

    function rememberView(event) {
        var id = idFromViewDetail(event);
        if (!id) {
            return;
        }
        viewItemId = id;
        viewItemAt = Date.now();
    }

    function wake() {
        sessionAt = 0;
        sync();
        window.setTimeout(sync, 50);
        window.setTimeout(sync, 400);
    }

    function onViewLifecycle(event) {
        var name = event.type || "";
        var page = videoViewFrom(event.target);
        if (name === "viewhide" || name === "pagehide") {
            if (page) {
                viewItemId = "";
                viewItemAt = 0;
            }
            window.setTimeout(sync, 0);
            window.setTimeout(sync, 400);
            return;
        }
        if (name === "viewshow" || name === "pageshow") {
            if (page || onVideoRoute()) {
                rememberView(event);
            }
            wake();
            return;
        }
        sync();
    }

    var adminState = "unknown";
    var adminAttempt = 0;

    function writeLabel(el, title) {
        if (!el) {
            return;
        }
        var textNode = null;
        var extras = [];
        var i;
        for (i = 0; i < el.childNodes.length; i++) {
            var node = el.childNodes[i];
            if (node.nodeType !== 3) {
                continue;
            }
            if (textNode) {
                extras.push(node);
            } else {
                textNode = node;
            }
        }
        if (!textNode) {
            if (el.childElementCount) {
                return;
            }
            textNode = document.createTextNode("");
            el.appendChild(textNode);
        }
        if (textNode.nodeValue !== title) {
            textNode.nodeValue = title;
        }
        for (i = 0; i < extras.length; i++) {
            extras[i].remove();
        }
    }

    function setMenuTitle(link, title) {
        var named = link.querySelector(".navMenuText, .navMenuOptionText, .MuiListItemText-primary");
        if (named) {
            writeLabel(named, title);
            return;
        }
        var spans = link.querySelectorAll("span");
        var i;
        for (i = spans.length - 1; i >= 0; i--) {
            var span = spans[i];
            if (span.classList.contains("material-icons") || span.classList.contains("navMenuIcon") || span.classList.contains("navMenuOptionIcon") || span.querySelector("span, svg")) {
                continue;
            }
            var current = (span.textContent || "").trim();
            if (!current || current === "Color Dots" || current === "彩色圆点" || current === "Jellyfin Color Tags" || current === "Jellyfin 颜色标签") {
                writeLabel(span, title);
                return;
            }
        }
    }

    function localizeMenus() {
        var title = text("menu");
        var links = document.querySelectorAll("a[href*='configurationpage'], a.jfcd-drawer-link");
        var i;
        for (i = 0; i < links.length; i++) {
            var href = links[i].getAttribute("href") || "";
            if (href.indexOf("Jellyfin%20Color%20Tags") === -1 && href.indexOf("Jellyfin+Color+Tags") === -1 && href.indexOf("Jellyfin Color Tags") === -1 && href.indexOf("Color%20Dots") === -1 && href.indexOf("Color+Dots") === -1 && href.indexOf("Color Dots") === -1 && !links[i].classList.contains("jfcd-drawer-link")) {
                continue;
            }
            setMenuTitle(links[i], title);
        }
    }

    function isLibraryAdminSection(node) {
        if (!node || node.closest(".MuiDrawer-root")) {
            return false;
        }
        return node.classList.contains("adminMenuOptions") || node.classList.contains("adminDrawer");
    }

    function paintDrawerLink() {
        var onDashboard = document.body.classList.contains("dashboardDocument");
        var stray = document.querySelectorAll("a.jfcd-drawer-link");
        var i;
        for (i = 0; i < stray.length; i++) {
            var parent = stray[i].parentElement;
            if (onDashboard || !isLibraryAdminSection(parent)) {
                stray[i].remove();
            }
        }
        if (adminState === "yes" && !onDashboard && !document.querySelector("a.jfcd-drawer-link")) {
            var sections = document.querySelectorAll(".adminMenuOptions, .adminDrawer");
            for (i = 0; i < sections.length; i++) {
                var drawer = sections[i];
                if (!isLibraryAdminSection(drawer) || drawer.querySelector("a[href*='configurationpage']")) {
                    continue;
                }
                var link = document.createElement("a");
                link.className = "navMenuOption lnkMediaFolder lnk jfcd-drawer-link";
                link.href = "#/configurationpage?name=" + encodeURIComponent("Jellyfin Color Tags");
                var icon = document.createElement("span");
                icon.className = "material-icons navMenuOptionIcon navMenuIcon";
                icon.setAttribute("aria-hidden", "true");
                icon.textContent = "palette";
                var label = document.createElement("span");
                label.className = "navMenuOptionText navMenuText";
                label.appendChild(document.createTextNode(text("menu")));
                link.appendChild(icon);
                link.appendChild(label);
                var dash = drawer.querySelector("a[href*='dashboard']");
                if (dash) {
                    dash.insertAdjacentElement("afterend", link);
                } else {
                    drawer.appendChild(link);
                }
                break;
            }
        }
        localizeMenus();
    }

    function noteAdmin(user) {
        var policy = user && (user.Policy || user.policy);
        var config = user && (user.Configuration || user.configuration) || {};
        userDisplayLanguage = config.DisplayLanguage || config.displayLanguage || (user && (user.DisplayLanguage || user.displayLanguage)) || "";
        adminState = policy && (policy.IsAdministrator || policy.isAdministrator) ? "yes" : "no";
        paintDrawerLink();
    }

    function refreshAdmin() {
        if (adminState !== "unknown") {
            paintDrawerLink();
            return;
        }
        var api = window.ApiClient;
        var current = api && typeof api.getCurrentUser === "function" ? api.getCurrentUser() : null;
        if (current && typeof current.then === "function") {
            current.then(noteAdmin).catch(function () {
                if (session()) {
                    adminState = "no";
                }
            });
            return;
        }
        if (current && (current.Policy || current.policy)) {
            noteAdmin(current);
            return;
        }
        var headers = authHeaders();
        if (!headers) {
            return;
        }
        fetch(apiRoot.replace(/\/ColorDots$/, "/Users/Me"), {
            headers: headers,
            credentials: "same-origin",
            cache: "no-store"
        }).then(function (response) {
            if (!response.ok) {
                throw new Error(String(response.status));
            }
            return response.json();
        }).then(noteAdmin).catch(function () {
            if (session()) {
                adminState = "no";
            }
        });
    }

    function maybeAdmin() {
        if (adminState === "yes") {
            paintDrawerLink();
            return;
        }
        if (adminState === "no") {
            return;
        }
        if (adminAttempt && Date.now() - adminAttempt < 2000) {
            return;
        }
        adminAttempt = Date.now();
        refreshAdmin();
    }

    function boot() {
        installStyles();
        document.documentElement.dataset.colordots = "1.4.0";
        maybeAdmin();
        document.addEventListener("viewshow", onViewLifecycle, true);
        document.addEventListener("viewhide", onViewLifecycle, true);
        document.addEventListener("pageshow", onViewLifecycle, true);
        document.addEventListener("pagehide", onViewLifecycle, true);
        sync();
        refreshLabels(true);

        function queueSync() {
            if (syncQueued) {
                return;
            }
            syncQueued = true;
            window.requestAnimationFrame(function () {
                syncQueued = false;
                sync();
                maybeAdmin();
            });
        }
        var observer = new MutationObserver(queueSync);
        observer.observe(document.documentElement, { childList: true, subtree: true });
        document.addEventListener("mouseover", queueSync, true);
        document.addEventListener("mouseout", queueSync, true);
        document.addEventListener("focusin", queueSync, true);
        document.addEventListener("focusout", queueSync, true);
        document.addEventListener("transitionend", queueSync, true);

        window.addEventListener("hashchange", function () {
            if (!onVideoRoute()) {
                viewItemId = "";
                viewItemAt = 0;
            }
            wake();
        });
        window.addEventListener("popstate", function () {
            if (!onVideoRoute()) {
                viewItemId = "";
                viewItemAt = 0;
            }
            wake();
        });
        window.addEventListener("resize", sync);
        window.setInterval(sync, 800);
        window.addEventListener("keydown", function (event) {
            if (event.key === "Escape") {
                closePanel();
            }
        });
        window.addEventListener("scroll", function () {
            queueSync();
            if (!panel || panel.hidden) {
                return;
            }
            closePanel();
        }, true);
        document.addEventListener("pointerdown", function (event) {
            if (!panel || panel.hidden) {
                return;
            }
            var target = event.target;
            if (target === openHit || (openHit && openHit.contains(target)) || panel.contains(target)) {
                return;
            }
            closePanel();
        }, true);
    }

    if (typeof globalThis !== "undefined" && typeof globalThis.__JFCD_TEST === "function") {
        globalThis.__JFCD_TEST({
            normLabels: normLabels,
            dotFill: dotFill,
            toggled: toggled,
            text: text,
            setLabels: function (next) {
                labels = next;
                labelsReady = true;
            }
        });
    } else if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }
})();
