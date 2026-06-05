(() => {
  const apiBase = "__STEAMLOADER_API_BASE__";
  const stateVersion = 2;
  const themeScanEventName = "steamtools:theme-scan-complete";
  const titleNoiseMarkers = [
    "PLAY",
    "SPIELEN",
    "PLAYTIME",
    "SPIELZEIT",
    "LAST PLAYED",
    "ZULETZT GESPIELT",
    "ACTIVITY",
    "AKTIVITAT",
    "AKTIVITÄT",
    "COMMUNITY",
    "SPIELINFO",
    "GAME INFO",
    "STEAM CLOUD",
    "ACHIEVEMENTS",
    "ERFOLGE",
    "CONTROLLER",
    "MAIN STORY",
    "MAIN + EXTRAS",
    "COMPLETIONIST",
    "ALL STYLES",
    "VIEW DETAILS",
    "DETAILS",
  ];
  const appIdPatterns = [
    /\/apps\/(\d+)\//i,
    /\/images\/apps\/(\d+)\//i,
    /\/libraryassets\/(\d+)\//i,
    /[?&]appid=(\d+)/i,
    /\/appdetails\/(\d+)/i,
  ];

  const previousState = window.__steamToolsHltbSurfaceState;
  if (previousState?.version !== stateVersion) {
    if (previousState?.timerHandle) {
      window.clearInterval(previousState.timerHandle);
    }

    if (previousState?.refreshHandle) {
      window.clearTimeout(previousState.refreshHandle);
    }

    if (previousState?.observer) {
      previousState.observer.disconnect();
    }

    if (typeof previousState?.themeScanHandler === "function") {
      window.removeEventListener(themeScanEventName, previousState.themeScanHandler);
    }
  }

  const state =
    previousState?.version === stateVersion
      ? previousState
      : (window.__steamToolsHltbSurfaceState = {
          version: stateVersion,
          installed: false,
          timerHandle: null,
          refreshHandle: null,
          observer: null,
          themeScanHandler: null,
          inFlightKey: "",
          lastRequestKey: "",
          lastSnapshot: null,
          lastFetchAt: 0,
        });

  function ensureStyleElement() {
    let style = document.getElementById("steamtools-hltb-style");
    if (!style) {
      style = document.createElement("style");
      style.id = "steamtools-hltb-style";
      document.head.append(style);
    }

    style.textContent = `
      .steamtools-hltb-panel {
        display: grid;
        grid-template-columns: minmax(0, 1fr) auto;
        gap: 18px;
        align-items: stretch;
        width: 100%;
        box-sizing: border-box;
        padding: 14px 24px;
        margin: 0;
        background: linear-gradient(90deg, rgba(12, 17, 24, 0.9), rgba(18, 26, 34, 0.86));
        border-top: 1px solid rgba(255, 255, 255, 0.06);
        border-bottom: 1px solid rgba(255, 255, 255, 0.04);
        backdrop-filter: blur(14px);
      }

      .steamtools-hltb-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
        gap: 12px 18px;
        min-width: 0;
      }

      .steamtools-hltb-stat {
        display: flex;
        flex-direction: column;
        gap: 3px;
        min-width: 0;
      }

      .steamtools-hltb-value {
        color: #edf3f8;
        font-size: clamp(18px, 1.7vw, 26px);
        line-height: 1.05;
        font-weight: 700;
        letter-spacing: -0.03em;
        white-space: nowrap;
      }

      .steamtools-hltb-label {
        color: rgba(200, 212, 224, 0.78);
        font-size: clamp(11px, 1vw, 14px);
        line-height: 1.15;
        font-weight: 600;
        letter-spacing: 0.08em;
        text-transform: uppercase;
      }

      .steamtools-hltb-actions {
        display: flex;
        align-items: center;
        justify-content: flex-end;
      }

      .steamtools-hltb-button {
        appearance: none;
        border: 0;
        border-radius: 999px;
        padding: 0 18px;
        min-height: 44px;
        background: rgba(42, 135, 219, 0.18);
        color: #49a3ff;
        font-size: 13px;
        font-weight: 700;
        letter-spacing: 0.02em;
        text-transform: uppercase;
        cursor: pointer;
      }

      .steamtools-hltb-button:hover {
        background: rgba(42, 135, 219, 0.28);
      }

      @media (max-width: 1280px) {
        .steamtools-hltb-panel {
          grid-template-columns: 1fr;
          padding: 12px 18px;
        }

        .steamtools-hltb-actions {
          justify-content: flex-start;
        }

        .steamtools-hltb-grid {
          grid-template-columns: repeat(2, minmax(0, 1fr));
        }
      }
    `;

    return style;
  }

  function normalizeText(value) {
    return (value || "").replace(/\s+/g, " ").trim();
  }

  function normalizeTitleScore(value) {
    return normalizeText(value)
      .normalize("NFKD")
      .replace(/[\u0300-\u036f]/g, "")
      .toUpperCase();
  }

  function isVisibleElement(node) {
    return (
      node instanceof HTMLElement &&
      node.isConnected &&
      window.getComputedStyle(node).display !== "none" &&
      window.getComputedStyle(node).visibility !== "hidden"
    );
  }

  function getActiveDetailContainer() {
    const containers = [...document.querySelectorAll(".steamloader-theme-game-detail")]
      .filter((node) => isVisibleElement(node))
      .filter((node) => {
        const rect = node.getBoundingClientRect();
        return rect.width >= 320 && rect.height >= 200;
      })
      .sort((left, right) => {
        const leftRect = left.getBoundingClientRect();
        const rightRect = right.getBoundingClientRect();
        return rightRect.width * rightRect.height - leftRect.width * leftRect.height;
      });

    return containers[0] || null;
  }

  function removeDetachedPanels(activeContainer) {
    for (const panel of document.querySelectorAll(".steamtools-hltb-panel")) {
      if (!(panel instanceof HTMLElement)) {
        continue;
      }

      if (!activeContainer || !activeContainer.contains(panel)) {
        panel.remove();
      }
    }
  }

  function isNoiseTitle(value) {
    const text = normalizeTitleScore(value);
    if (!text || text.length < 2 || text.length > 120) {
      return true;
    }

    if (!/[A-Z]/.test(text)) {
      return true;
    }

    return titleNoiseMarkers.some((marker) => text.includes(marker));
  }

  function readReactRoots(node) {
    const roots = [];
    for (let depth = 0, current = node; current && depth < 7; depth += 1, current = current.parentElement) {
      for (const key of Object.keys(current)) {
        if (key.startsWith("__reactProps$") || key.startsWith("__reactFiber$")) {
          roots.push(current[key]);
        }
      }
    }

    return roots;
  }

  function tryExtractGameInfoFromObject(root) {
    const seen = new WeakSet();
    const queue = [root];
    let inspected = 0;
    let bestTitle = "";
    let bestAppId = null;

    while (queue.length && inspected < 900) {
      const current = queue.shift();
      if (!current || typeof current !== "object") {
        continue;
      }

      if (seen.has(current)) {
        continue;
      }

      seen.add(current);
      inspected += 1;

      const titleCandidate = normalizeText(
        current.display_name ||
          current.strDisplayName ||
          current.title ||
          current.name ||
          current.appName ||
          current.localized_name ||
          "",
      );
      const appIdCandidate = Number(
        current.appid || current.appId || current.appID || current.app_id || 0,
      );

      if (!bestTitle && titleCandidate && !isNoiseTitle(titleCandidate)) {
        bestTitle = titleCandidate;
      }

      if (!bestAppId && Number.isInteger(appIdCandidate) && appIdCandidate > 0) {
        bestAppId = appIdCandidate;
      }

      if (bestTitle && bestAppId) {
        return { title: bestTitle, appId: bestAppId };
      }

      const priorityKeys = [
        "overview",
        "app",
        "game",
        "item",
        "data",
        "props",
        "memoizedProps",
        "pendingProps",
        "memoizedState",
        "return",
        "child",
        "sibling",
      ];

      for (const key of priorityKeys) {
        if (current[key] && typeof current[key] === "object") {
          queue.push(current[key]);
        }
      }

      for (const key of Object.keys(current)) {
        if (priorityKeys.includes(key) || key.startsWith("__react")) {
          continue;
        }

        const value = current[key];
        if (!value || typeof value !== "object") {
          continue;
        }

        if (value instanceof Element || value instanceof Node) {
          continue;
        }

        queue.push(value);
      }
    }

    return {
      title: bestTitle,
      appId: bestAppId,
    };
  }

  function extractGameInfoFromReact(node) {
    const roots = readReactRoots(node);
    for (const root of roots) {
      const result = tryExtractGameInfoFromObject(root);
      if (result.title || result.appId) {
        return result;
      }
    }

    return { title: "", appId: null };
  }

  function extractAppIdFromUrlish(value) {
    if (!value) {
      return null;
    }

    for (const pattern of appIdPatterns) {
      const match = String(value).match(pattern);
      if (match) {
        const parsed = Number(match[1]);
        if (Number.isInteger(parsed) && parsed > 0) {
          return parsed;
        }
      }
    }

    return null;
  }

  function findAppId(container) {
    for (const node of container.querySelectorAll("img, a, [style], [data-panel]")) {
      if (!(node instanceof HTMLElement)) {
        continue;
      }

      if (node instanceof HTMLImageElement) {
        const imageAppId =
          extractAppIdFromUrlish(node.currentSrc) ||
          extractAppIdFromUrlish(node.src) ||
          extractAppIdFromUrlish(node.alt);
        if (imageAppId) {
          return imageAppId;
        }
      }

      const hrefAppId = extractAppIdFromUrlish(node.getAttribute("href"));
      if (hrefAppId) {
        return hrefAppId;
      }

      const styleAppId = extractAppIdFromUrlish(node.style.backgroundImage);
      if (styleAppId) {
        return styleAppId;
      }
    }

    return null;
  }

  function scoreTitleCandidate(value) {
    const text = normalizeText(value);
    const upper = normalizeTitleScore(text);

    if (isNoiseTitle(text)) {
      return -1;
    }

    let score = 0;
    if (text.length >= 4 && text.length <= 64) {
      score += 3;
    }

    if (/[A-Za-z]/.test(text) && /\s/.test(text)) {
      score += 3;
    }

    if (/\d/.test(text)) {
      score += 1;
    }

    if (!upper.includes("STEAM")) {
      score += 1;
    }

    return score;
  }

  function collectTitleCandidates(container) {
    const candidates = [];
    const pushCandidate = (value) => {
      const text = normalizeText(value);
      if (!text) {
        return;
      }

      candidates.push(text);
    };

    for (const node of container.querySelectorAll("[aria-label], [title], img[alt]")) {
      if (!(node instanceof HTMLElement)) {
        continue;
      }

      pushCandidate(node.getAttribute("aria-label"));
      pushCandidate(node.getAttribute("title"));
      if (node instanceof HTMLImageElement) {
        pushCandidate(node.alt);
      }
    }

    const textCandidates = [...container.querySelectorAll("h1, h2, h3, div, span")]
      .filter((node) => node instanceof HTMLElement)
      .filter((node) => isVisibleElement(node))
      .filter((node) => {
        const rect = node.getBoundingClientRect();
        return rect.width >= 80 && rect.height >= 14 && rect.height <= 120;
      })
      .map((node) => normalizeText(node.innerText || ""))
      .filter((text) => text && text.length <= 90)
      .slice(0, 120);

    for (const text of textCandidates) {
      pushCandidate(text);
    }

    return candidates
      .map((value) => ({ value, score: scoreTitleCandidate(value) }))
      .filter((candidate) => candidate.score >= 0)
      .sort((left, right) => right.score - left.score || left.value.length - right.value.length);
  }

  function extractGameContext(container) {
    const reactInfo = extractGameInfoFromReact(container);
    const titleCandidates = collectTitleCandidates(container);
    const bestTitle =
      reactInfo.title ||
      (titleCandidates.length > 0 ? titleCandidates[0].value : "");
    const bestAppId = reactInfo.appId || findAppId(container);

    return {
      title: bestTitle,
      appId: bestAppId,
    };
  }

  function findInsertionAnchor(container) {
    const playbar = container.querySelector(".steamloader-theme-game-detail-playbar");
    if (playbar instanceof HTMLElement) {
      return playbar;
    }

    const fallback = [...container.querySelectorAll("div, section")]
      .filter((node) => node instanceof HTMLElement)
      .find((node) => /PLAYTIME|SPIELZEIT|LAST PLAYED|ZULETZT GESPIELT|CONTROLLER/i.test(node.innerText || ""));

    return fallback instanceof HTMLElement ? fallback : null;
  }

  function formatHours(value) {
    return value && value !== "--" ? `${value} hours` : "--";
  }

  function buildVisibleStats(snapshot) {
    const settings = snapshot?.settings || {};
    const stats = [];

    if (settings.showMainStory) {
      stats.push({ label: "Main Story", value: snapshot.mainStory });
    }

    if (settings.showMainPlus) {
      stats.push({ label: "Main + Extras", value: snapshot.mainPlus });
    }

    if (settings.showCompletionist) {
      stats.push({ label: "Completionist", value: snapshot.completionist });
    }

    if (settings.showAllStyles) {
      stats.push({ label: "All Styles", value: snapshot.allStyles });
    }

    return stats.filter((entry) => entry.value && entry.value !== "--");
  }

  function ensurePanel(container, anchor) {
    let panel = container.querySelector(".steamtools-hltb-panel");
    if (!(panel instanceof HTMLElement)) {
      panel = document.createElement("div");
      panel.className = "steamtools-hltb-panel";
    }

    if (anchor && panel !== anchor.previousElementSibling) {
      anchor.parentElement.insertBefore(panel, anchor);
    } else if (!panel.isConnected) {
      container.append(panel);
    }

    return panel;
  }

  function clearPanel(container) {
    if (!container) {
      return;
    }

    const panel = container.querySelector(".steamtools-hltb-panel");
    if (panel) {
      panel.remove();
    }
  }

  function renderSnapshot(container, snapshot) {
    if (!snapshot || !snapshot.settings?.enabled || !snapshot.found) {
      clearPanel(container);
      return;
    }

    const stats = buildVisibleStats(snapshot);
    if (!stats.length) {
      clearPanel(container);
      return;
    }

    const anchor = findInsertionAnchor(container);
    if (!anchor) {
      clearPanel(container);
      return;
    }

    const panel = ensurePanel(container, anchor);
    panel.replaceChildren();

    const grid = document.createElement("div");
    grid.className = "steamtools-hltb-grid";

    for (const stat of stats) {
      const item = document.createElement("div");
      item.className = "steamtools-hltb-stat";

      const value = document.createElement("div");
      value.className = "steamtools-hltb-value";
      value.textContent = formatHours(stat.value);

      const label = document.createElement("div");
      label.className = "steamtools-hltb-label";
      label.textContent = stat.label;

      item.append(value, label);
      grid.append(item);
    }

    panel.append(grid);

    if (snapshot.settings.showViewDetails && snapshot.detailUrl) {
      const actions = document.createElement("div");
      actions.className = "steamtools-hltb-actions";

      const button = document.createElement("button");
      button.type = "button";
      button.className = "steamtools-hltb-button";
      button.textContent = "View Details";
      button.addEventListener("click", () => {
        void fetch(`${apiBase}api/hltb/open-details`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({ value: snapshot.detailUrl }),
        });
      });

      actions.append(button);
      panel.append(actions);
    }
  }

  async function fetchSnapshot(context) {
    const query = new URLSearchParams();
    if (context.title) {
      query.set("title", context.title);
    }

    if (context.appId) {
      query.set("appId", String(context.appId));
    }

    const response = await fetch(`${apiBase}api/hltb/game?${query.toString()}`, { cache: "no-store" });
    const payload = await response.json();
    if (!response.ok) {
      throw new Error(payload?.message || `HLTB could not be loaded (${response.status}).`);
    }

    return payload;
  }

  async function refreshHltb() {
    state.refreshHandle = null;
    ensureStyleElement();

    if (document.hidden) {
      return;
    }

    const container = getActiveDetailContainer();
    removeDetachedPanels(container);

    if (!container) {
      state.lastSnapshot = null;
      state.lastRequestKey = "";
      return;
    }

    const context = extractGameContext(container);
    const requestKey = `${context.appId || 0}|${context.title || ""}`;
    if (!context.title && !context.appId) {
      clearPanel(container);
      return;
    }

    const now = Date.now();
    if (
      state.lastSnapshot &&
      state.lastRequestKey === requestKey &&
      now - state.lastFetchAt < 4000
    ) {
      renderSnapshot(container, state.lastSnapshot);
      return;
    }

    if (state.inFlightKey === requestKey) {
      return;
    }

    state.inFlightKey = requestKey;
    try {
      const snapshot = await fetchSnapshot(context);
      state.lastRequestKey = requestKey;
      state.lastSnapshot = snapshot;
      state.lastFetchAt = Date.now();
      renderSnapshot(container, snapshot);
    } catch {
      clearPanel(container);
    } finally {
      if (state.inFlightKey === requestKey) {
        state.inFlightKey = "";
      }
    }
  }

  function queueRefresh() {
    if (state.refreshHandle) {
      return;
    }

    state.refreshHandle = window.setTimeout(() => {
      void refreshHltb();
    }, 220);
  }

  function install() {
    ensureStyleElement();
    queueRefresh();

    if (!state.observer) {
      state.observer = new MutationObserver((mutations) => {
        for (const mutation of mutations) {
          if (mutation.type === "childList" && (mutation.addedNodes.length > 0 || mutation.removedNodes.length > 0)) {
            queueRefresh();
            break;
          }
        }
      });

      state.observer.observe(document.documentElement, {
        childList: true,
        subtree: true,
      });
    }

    if (typeof state.themeScanHandler !== "function") {
      state.themeScanHandler = () => {
        queueRefresh();
      };
      window.addEventListener(themeScanEventName, state.themeScanHandler);
    }

    if (state.timerHandle) {
      window.clearInterval(state.timerHandle);
    }

    state.timerHandle = window.setInterval(() => {
      if (!document.hidden) {
        queueRefresh();
      }
    }, 12000);

    state.installed = true;
    return true;
  }

  return install() ? "injected" : "waiting";
})();
