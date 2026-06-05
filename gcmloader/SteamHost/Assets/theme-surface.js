(() => {
  const apiBase = "__STEAMLOADER_API_BASE__";
  const stateVersion = 7;
  const themeScanEventName = "steamtools:theme-scan-complete";
  const detailViewMarkers = [
    "SPIELZEIT",
    "PLAYTIME",
    "PLAY TIME",
    "AKTIVITÄT",
    "ACTIVITY",
    "ERFOLGE",
    "ACHIEVEMENTS",
    "LETZTE SITZUNG",
    "LAST SESSION",
    "VERWALTEN",
    "MANAGE",
    "FREUNDE, DIE SPIELEN",
    "FRIENDS WHO PLAY",
  ];
  const themeMarkerClasses = [
    "steamloader-theme-artwork",
    "steamloader-theme-artwork-host",
    "steamloader-theme-artwork-panel",
    "steamloader-theme-artwork-bg",
    "steamloader-theme-artwork-portrait",
    "steamloader-theme-artwork-landscape",
    "steamloader-theme-artwork-square",
    "steamloader-theme-portrait",
    "steamloader-theme-portrait-host",
    "steamloader-theme-portrait-bg",
    "steamloader-theme-game-card",
    "steamloader-theme-game-card-host",
    "steamloader-theme-game-card-portrait",
    "steamloader-theme-game-card-landscape",
    "steamloader-theme-game-card-square",
    "steamloader-theme-game-detail",
    "steamloader-theme-game-detail-art",
    "steamloader-theme-game-detail-copy",
    "steamloader-theme-game-detail-topcapsule",
    "steamloader-theme-game-detail-hero",
    "steamloader-theme-game-detail-hero-image",
    "steamloader-theme-game-detail-playbar",
    "steamloader-theme-game-detail-title-section",
    "steamloader-theme-game-detail-logo",
    "steamloader-theme-game-detail-logo-box",
    "steamloader-theme-game-detail-cloud-status",
    "steamloader-theme-game-detail-cloud-label",
    "steamloader-theme-game-detail-cloud-icon",
    "steamloader-theme-game-detail-cloud-problem",
    "steamloader-theme-ui-toggle",
    "steamloader-theme-ui-toggle-on",
  ];

  const previousState = window.__steamLoaderThemeSurfaceState;
  if (previousState?.version !== stateVersion) {
    if (previousState?.timerHandle) {
      window.clearInterval(previousState.timerHandle);
    }

    if (previousState?.scanHandle) {
      window.clearTimeout(previousState.scanHandle);
    }

    if (previousState?.observer) {
      previousState.observer.disconnect();
    }
  }

  const state =
    previousState?.version === stateVersion
      ? previousState
      : (window.__steamLoaderThemeSurfaceState = {
          version: stateVersion,
          installed: false,
          timerHandle: null,
          scanHandle: null,
          observer: null,
          markedNodes: new Set(),
          lastResolveKey: "",
          lastResolvedAt: 0,
        });

  if (!(state.markedNodes instanceof Set)) {
    state.markedNodes = new Set();
  }

  function ensureStyleElement() {
    let style = document.getElementById("steamloader-global-theme-style");
    if (!style) {
      style = document.createElement("style");
      style.id = "steamloader-global-theme-style";
      document.head.append(style);
    }

    return style;
  }

  function clearThemeMarkers() {
    for (const node of state.markedNodes) {
      if (node instanceof Element) {
        node.classList.remove(...themeMarkerClasses);
      }
    }

    state.markedNodes.clear();
  }

  function markNode(node, ...classes) {
    if (!(node instanceof Element)) {
      return;
    }

    node.classList.add(...classes);
    state.markedNodes.add(node);
  }

  function normalizeText(value) {
    return (value || "").replace(/\s+/g, " ").trim().toUpperCase();
  }

  function classifyArtworkRect(rect) {
    if (rect.width < 96 || rect.height < 96) {
      return null;
    }

    if (rect.height >= 170 && rect.height >= rect.width * 1.18) {
      return "portrait";
    }

    if (rect.width >= 180 && rect.width >= rect.height * 1.55) {
      return "landscape";
    }

    if (rect.width >= 160 && rect.height >= 160) {
      return "square";
    }

    return null;
  }

  function isNearlySameSize(referenceRect, candidateRect) {
    const widthDifference = Math.abs(referenceRect.width - candidateRect.width);
    const heightDifference = Math.abs(referenceRect.height - candidateRect.height);
    const widthTolerance = Math.max(10, referenceRect.width * 0.08);
    const heightTolerance = Math.max(10, referenceRect.height * 0.08);

    return widthDifference <= widthTolerance && heightDifference <= heightTolerance;
  }

  function isArtworkSource(source) {
    if (!source) {
      return false;
    }

    const normalizedSource = source.toLowerCase();
    return (
      normalizedSource.includes("/assets/") ||
      normalizedSource.includes("/customimages/") ||
      normalizedSource.includes("steamstatic.com") ||
      normalizedSource.includes("shared.steamstatic.com") ||
      normalizedSource.includes("steamloopback.host") ||
      normalizedSource.includes("library_") ||
      normalizedSource.includes("librarycapsule") ||
      normalizedSource.includes("library_capsule") ||
      normalizedSource.includes("libraryhero") ||
      normalizedSource.includes("library_hero") ||
      normalizedSource.includes("library_header") ||
      normalizedSource.includes("/header.") ||
      normalizedSource.includes("/capsule.") ||
      normalizedSource.includes("/capsule_") ||
      normalizedSource.includes("/hero")
    );
  }

  function hasVisibleText(node) {
    return normalizeText(node.innerText || "").length > 0;
  }

  function getDetailViewContainer(node) {
    let current = node;
    for (let depth = 0; current && depth < 8; depth += 1, current = current.parentElement) {
      const rect = current.getBoundingClientRect();
      if (rect.width < 320 || rect.height < 160) {
        continue;
      }

      const text = normalizeText(current.innerText || "");
      if (!text) {
        continue;
      }

      let markerHits = 0;
      for (const marker of detailViewMarkers) {
        if (text.includes(marker)) {
          markerHits += 1;
        }
      }

      if (markerHits >= 2) {
        return current;
      }
    }

    return null;
  }

  function isDetailViewArtwork(node) {
    return Boolean(getDetailViewContainer(node));
  }

  function hasImageDescendant(node) {
    return Boolean(node.querySelector("img, video, canvas"));
  }

  function isLikelyDetailCopy(node) {
    if (!node || hasImageDescendant(node)) {
      return false;
    }

    const text = normalizeText(node.innerText || "");
    if (text.length < 3 || text.length > 220) {
      return false;
    }

    const rect = node.getBoundingClientRect();
    return rect.width >= 240 && rect.height >= 18 && rect.height <= 180;
  }

  function findBestDetailNode(container, predicate) {
    const candidates = [...container.querySelectorAll("div, section, span, img, canvas, video")]
      .filter((candidate) => candidate instanceof HTMLElement && predicate(candidate));

    if (candidates.length === 0) {
      return null;
    }

    candidates.sort((left, right) => {
      const leftRect = left.getBoundingClientRect();
      const rightRect = right.getBoundingClientRect();
      return rightRect.width * rightRect.height - leftRect.width * leftRect.height;
    });

    return candidates[0];
  }

  function markDetailViewStructure(container) {
    const containerRect = container.getBoundingClientRect();

    const topCapsule = findBestDetailNode(container, (candidate) => {
      const rect = candidate.getBoundingClientRect();
      const text = normalizeText(candidate.innerText || "");
      return (
        rect.width >= containerRect.width * 0.92 &&
        rect.height >= 220 &&
        rect.height <= 520 &&
        rect.top <= containerRect.top + 80 &&
        rect.bottom <= containerRect.top + containerRect.height * 0.55 &&
        text.length < 16
      );
    });

    if (topCapsule) {
      markNode(topCapsule, "steamloader-theme-game-detail-topcapsule");
    }

    const heroImages = [...container.querySelectorAll("img, canvas, video")].filter((candidate) => {
      if (!(candidate instanceof HTMLElement)) {
        return false;
      }

      const rect = candidate.getBoundingClientRect();
      return (
        rect.width >= containerRect.width * 0.65 &&
        rect.height >= 220 &&
        rect.top <= containerRect.top + containerRect.height * 0.35
      );
    });

    for (const heroImage of heroImages) {
      markNode(heroImage, "steamloader-theme-game-detail-hero-image");

      let current = heroImage.parentElement;
      for (let depth = 0; current && depth < 4; depth += 1, current = current.parentElement) {
        const rect = current.getBoundingClientRect();
        if (rect.width >= containerRect.width * 0.75 && rect.height >= 220) {
          markNode(current, "steamloader-theme-game-detail-hero");
        }
      }
    }

    const logoImage = findBestDetailNode(container, (candidate) => {
      if (!(candidate instanceof HTMLImageElement)) {
        return false;
      }

      const rect = candidate.getBoundingClientRect();
      const centerX = rect.left + rect.width / 2;
      return (
        rect.width >= 180 &&
        rect.width <= 720 &&
        rect.height >= 50 &&
        rect.height <= 240 &&
        rect.top <= containerRect.top + containerRect.height * 0.4 &&
        centerX >= containerRect.left + containerRect.width * 0.25 &&
        centerX <= containerRect.right - containerRect.width * 0.25
      );
    });

    if (logoImage) {
      markNode(logoImage, "steamloader-theme-game-detail-logo");
      if (logoImage.parentElement) {
        markNode(logoImage.parentElement, "steamloader-theme-game-detail-logo-box");
      }
    }

    const playBar = findBestDetailNode(container, (candidate) => {
      const rect = candidate.getBoundingClientRect();
      const text = normalizeText(candidate.innerText || "");
      return (
        rect.width >= containerRect.width * 0.7 &&
        rect.height >= 40 &&
        rect.height <= 140 &&
        rect.top >= containerRect.top + 180 &&
        rect.top <= containerRect.top + 470 &&
        (text.includes("PLAYTIME") ||
          text.includes("SPIELZEIT") ||
          text.includes("LAST PLAYED") ||
          text.includes("ZULETZT GESPIELT") ||
          text.includes("CONTROLLER") ||
          text.startsWith("PLAY ") ||
          text.startsWith("SPIELEN "))
      );
    });

    if (playBar) {
      markNode(
        playBar,
        "steamloader-theme-game-detail-playbar",
        "steamloader-theme-game-detail-title-section",
      );
    }

    const cloudStatus = findBestDetailNode(container, (candidate) => {
      const rect = candidate.getBoundingClientRect();
      const text = normalizeText(candidate.innerText || "");
      return (
        rect.width >= 120 &&
        rect.width <= containerRect.width &&
        rect.height >= 18 &&
        rect.height <= 60 &&
        text.includes("STEAM CLOUD")
      );
    });

    if (cloudStatus) {
      markNode(cloudStatus, "steamloader-theme-game-detail-cloud-status");

      for (const candidate of cloudStatus.querySelectorAll("div, span")) {
        const text = normalizeText(candidate.innerText || "");
        if (text.includes("STEAM CLOUD")) {
          markNode(candidate, "steamloader-theme-game-detail-cloud-label");
        }
      }

      for (const candidate of cloudStatus.querySelectorAll("svg")) {
        if (candidate.parentElement) {
          markNode(candidate.parentElement, "steamloader-theme-game-detail-cloud-icon");
        }
      }
    }
  }

  function markDetailView(node) {
    const container = getDetailViewContainer(node);
    if (!container) {
      return;
    }

    markNode(container, "steamloader-theme-game-detail");
    markNode(node, "steamloader-theme-game-detail-art");

    const copyCandidates = [...container.querySelectorAll("div, section, span, h1, h2, h3")]
      .filter((candidate) => candidate instanceof HTMLElement && isLikelyDetailCopy(candidate))
      .slice(0, 6);

    for (const candidate of copyCandidates) {
      markNode(candidate, "steamloader-theme-game-detail-copy");
    }

    markDetailViewStructure(container);
  }

  function getArtworkHost(node) {
    const referenceRect = node.getBoundingClientRect();
    let current = node.parentElement;
    let bestSizedWrapper = null;

    while (current) {
      const candidateRect = current.getBoundingClientRect();
      if (!isNearlySameSize(referenceRect, candidateRect)) {
        break;
      }

      if (!current.classList.contains("Panel") && !hasVisibleText(current)) {
        bestSizedWrapper = current;
      }

      current = current.parentElement;
    }

    return (
      bestSizedWrapper ||
      node.closest(".Focusable") ||
      node.closest("[role='link']") ||
      node.closest("a") ||
      node.parentElement
    );
  }

  function markArtworkImage(image) {
    const rect = image.getBoundingClientRect();
    const artworkType = classifyArtworkRect(rect);
    if (!artworkType) {
      return;
    }

    const src = image.currentSrc || image.src || "";
    if (!isArtworkSource(src)) {
      return;
    }

    if (isDetailViewArtwork(image)) {
      markDetailView(image);
      return;
    }

    markNode(
      image,
      "steamloader-theme-artwork",
      `steamloader-theme-artwork-${artworkType}`,
      "steamloader-theme-game-card",
      `steamloader-theme-game-card-${artworkType}`,
    );

    if (artworkType === "portrait") {
      markNode(image, "steamloader-theme-portrait");
    }

    const host = getArtworkHost(image);

    if (host) {
      markNode(
        host,
        "steamloader-theme-artwork-host",
        `steamloader-theme-artwork-${artworkType}`,
        "steamloader-theme-game-card-host",
        `steamloader-theme-game-card-${artworkType}`,
      );

      if (artworkType === "portrait") {
        markNode(host, "steamloader-theme-portrait-host");
      }
    }
  }

  function markArtworkBackground(node) {
    const style = window.getComputedStyle(node);
    const backgroundImage = style.backgroundImage || "";
    if (!backgroundImage || backgroundImage === "none") {
      return;
    }

    if (!isArtworkSource(backgroundImage)) {
      return;
    }

    const rect = node.getBoundingClientRect();
    const artworkType = classifyArtworkRect(rect);
    if (!artworkType) {
      return;
    }

    if (isDetailViewArtwork(node)) {
      markDetailView(node);
      return;
    }

    if (hasVisibleText(node)) {
      return;
    }

    markNode(
      node,
      "steamloader-theme-artwork-bg",
      `steamloader-theme-artwork-${artworkType}`,
      "steamloader-theme-game-card",
      "steamloader-theme-game-card-host",
      `steamloader-theme-game-card-${artworkType}`,
    );

    if (artworkType === "portrait") {
      markNode(node, "steamloader-theme-portrait-bg");
    }
  }

  function isLikelyToggleNode(node) {
    if (!(node instanceof HTMLElement)) {
      return false;
    }

    const rect = node.getBoundingClientRect();
    if (rect.width < 22 || rect.width > 180 || rect.height < 12 || rect.height > 80) {
      return false;
    }

    const className = `${node.className || ""}`.toLowerCase();
    return (
      node.getAttribute("role") === "switch" ||
      node.hasAttribute("aria-checked") ||
      node.hasAttribute("aria-pressed") ||
      className.includes("toggle") ||
      className.includes("switch")
    );
  }

  function isToggleOn(node) {
    const ariaChecked = node.getAttribute("aria-checked");
    const ariaPressed = node.getAttribute("aria-pressed");
    const className = `${node.className || ""}`.toLowerCase();

    return (
      ariaChecked === "true" ||
      ariaPressed === "true" ||
      className.includes("is-on") ||
      className.includes("checked") ||
      className.includes("active")
    );
  }

  function markToggleNode(node) {
    if (!isLikelyToggleNode(node)) {
      return;
    }

    markNode(node, "steamloader-theme-ui-toggle");
    if (isToggleOn(node)) {
      markNode(node, "steamloader-theme-ui-toggle-on");
    }
  }

  function scanThemeTargets() {
    state.scanHandle = null;
    clearThemeMarkers();

    for (const image of document.images) {
      markArtworkImage(image);
    }

    const backgroundCandidates = document.querySelectorAll(
      ".Panel, .Focusable, [role='link'], a, [style*='background'], [role='switch'], [aria-checked], [aria-pressed], [class*='Toggle'], [class*='toggle'], [class*='Switch'], [class*='switch']",
    );

    for (const node of backgroundCandidates) {
      if (!(node instanceof HTMLElement)) {
        continue;
      }

      markArtworkBackground(node);
      markToggleNode(node);
    }

    window.dispatchEvent(new CustomEvent(themeScanEventName));
  }

  function queueScan(delay = 160) {
    if (state.scanHandle) {
      return;
    }

    state.scanHandle = window.setTimeout(scanThemeTargets, delay);
  }

  async function refreshThemeCss(force = false) {
    const resolveKey = `${document.title || ""}|${location.href || ""}`;
    const now = Date.now();
    if (!force && state.lastResolveKey === resolveKey && now - state.lastResolvedAt < 8000) {
      return;
    }

    try {
      const query = new URLSearchParams({
        title: document.title || "",
        url: location.href || "",
      });
      const response = await fetch(`${apiBase}api/themes/resolve-css?${query.toString()}`, { cache: "no-store" });
      if (!response.ok) {
        return;
      }

      const payload = await response.json();
      ensureStyleElement().textContent =
        payload && typeof payload.css === "string" ? payload.css : "";

      state.lastResolveKey = resolveKey;
      state.lastResolvedAt = now;
      queueScan();
    } catch {
    }
  }

  function install() {
    ensureStyleElement();
    queueScan(0);

    if (!state.observer) {
      state.observer = new MutationObserver((mutations) => {
        let shouldRefresh = false;
        let shouldScan = false;

        for (const mutation of mutations) {
          if (mutation.type === "childList") {
            if (mutation.addedNodes.length > 0 || mutation.removedNodes.length > 0) {
              shouldScan = true;
              shouldRefresh = true;
              break;
            }
          }

          if (
            mutation.type === "attributes" &&
            (mutation.target instanceof HTMLImageElement || mutation.attributeName === "href")
          ) {
            shouldScan = true;
          }
        }

        if (shouldRefresh) {
          void refreshThemeCss();
        }

        if (shouldScan) {
          queueScan();
        }
      });

      state.observer.observe(document.documentElement, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["src", "href"],
      });
    }

    if (state.timerHandle) {
      window.clearInterval(state.timerHandle);
    }

    state.timerHandle = window.setInterval(() => {
      if (!document.hidden) {
        queueScan();
        void refreshThemeCss();
      }
    }, 8000);

    void refreshThemeCss(true);
    state.installed = true;
    return true;
  }

  return install() ? "injected" : "waiting";
})();
