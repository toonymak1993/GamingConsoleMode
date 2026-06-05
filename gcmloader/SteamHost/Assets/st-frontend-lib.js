(() => {
  const existing = window.STFrontendLib;
  if (existing?.version >= 12) {
    return;
  }

  const steamToggleClasses = Object.freeze({
    toggle: "_9Ql-oVe_j8E-vsDdyVdWo",
    rail: "_2bl0iQ9xigbq4Zd1NI6NZl",
    on: "yLrDAetGoWx0GYqA6ShfS",
    switch: "_1PQppcgkuXQAiFPar9AGi-",
    disabled: "aIeh3X5T2M074RLW1qn6_",
  });

  let steamToggleStyleAvailable = null;

  const defaultModel = Object.freeze({
    title: "Tools for Steam",
    subtitle: "",
    status: "",
    error: "",
    note: "",
    headerIcon: null,
    autoFocusIndex: null,
    dividerAfterIndex: null,
    volumePanel: null,
    cards: Object.freeze([]),
    editor: null,
    slots: Object.freeze([]),
  });

  const nativeComponentByRole = Object.freeze({
    action: "dialogButton",
    command: "dialogButton",
    navigation: "dialogButton",
    back: "dialogButton",
    toggle: "toggleField",
    choice: "dropdown",
  });

  const localComponentDefinitions = Object.freeze([
    {
      id: "dialogButton",
      title: "Dialog Button",
      required: ["DialogButton", "Secondary"],
      preferredExports: ["$n"],
    },
    {
      id: "toggleField",
      title: "Toggle Field",
      required: ["ToggleField"],
      preferredExports: ["RF"],
    },
    {
      id: "toggleControl",
      title: "Toggle Control",
      required: ["ToggleRail", "ToggleSwitch", "PlayNavSound"],
      preferredExports: ["J0", "Hk"],
    },
    {
      id: "checkbox",
      title: "Checkbox",
      required: ["DialogCheckbox", "aria-checked"],
      preferredExports: ["Yh"],
    },
    {
      id: "dropdown",
      title: "Dropdown",
      required: ["rgOptions", "selectedOption", "BuildMenu"],
      preferredExports: ["ZU"],
    },
    {
      id: "sliderField",
      title: "Slider Field",
      required: ["onChangeComplete", "onChangeStart", "validValues", "editableValue"],
      preferredExports: ["d3"],
    },
    {
      id: "panelSectionRow",
      title: "Panel Section Row",
      required: ["childrenLayout", "bottomSeparator", "highlightOnFocus", "transparentBackground"],
      preferredExports: ["D0"],
    },
  ]);

  const localRegistryState = {
    version: 1,
    runtimeReady: false,
    moduleCount: 0,
    lastRefreshIso: null,
    components: {},
    errors: [],
  };

  let lastLocalRegistryAttemptMs = 0;

  function getReactPropertyKey(element, prefix) {
    return element
      ? Object.getOwnPropertyNames(element).find((name) => name.startsWith(prefix))
      : null;
  }

  function getReactFiber(element) {
    const fiberKey = getReactPropertyKey(element, "__reactFiber");
    return fiberKey ? element[fiberKey] : null;
  }

  function getTypeSource(type) {
    const directSource = typeof type?.toString === "function" ? type.toString() : "";
    const renderSource = typeof type?.render?.toString === "function" ? type.render.toString() : "";
    return `${directSource}\n${renderSource}`;
  }

  function getTypeName(type) {
    return type?.displayName || type?.name || type?.render?.displayName || type?.render?.name || "anonymous";
  }

  function getCurrentContextRuntimeRequire() {
    if (window.__steamToolsQuickAccessRequire) {
      return window.__steamToolsQuickAccessRequire;
    }

    const chunk = window.webpackChunksteamui;
    if (!Array.isArray(chunk) || typeof chunk.push !== "function") {
      return null;
    }

    let runtimeRequire = null;
    try {
      chunk.push([[`steam-tools-quickaccess-registry-${Date.now()}`], {}, (require) => {
        runtimeRequire = require;
        window.__steamToolsQuickAccessRequire = require;
      }]);
    } catch (error) {
      localRegistryState.errors.push(
        `Unable to capture Quick Access webpack runtime: ${String(error?.message || error)}`,
      );
    }

    return runtimeRequire;
  }

  function isComponentLike(value) {
    return typeof value === "function" || typeof value?.render === "function";
  }

  function getModuleExports(runtimeRequire, moduleId) {
    try {
      const exportsObject = runtimeRequire(moduleId);
      return exportsObject && typeof exportsObject === "object"
        ? Object.entries(exportsObject)
        : [["default", exportsObject]];
    } catch {
      return [];
    }
  }

  function matchesLocalDefinition(definition, exportKey, value) {
    if (!isComponentLike(value)) {
      return false;
    }

    const searchable = `${exportKey}\n${getTypeName(value)}\n${getTypeSource(value)}`;
    return definition.required.every((needle) => searchable.includes(needle));
  }

  function scoreLocalMatch(definition, exportKey, value) {
    let score = definition.required.length * 10;

    if (definition.preferredExports.includes(exportKey)) {
      score += 20;
    }

    if (getTypeName(value) !== "anonymous") {
      score += 2;
    }

    if (typeof value?.render === "function") {
      score += 2;
    }

    return score;
  }

  function createLocalComponentState(definition, match) {
    if (!match) {
      return {
        id: definition.id,
        title: definition.title,
        available: false,
        moduleId: null,
        exportKey: null,
        exportName: "",
        exportType: "",
        value: null,
      };
    }

    return {
      id: definition.id,
      title: definition.title,
      available: true,
      moduleId: match.moduleId,
      exportKey: match.exportKey,
      exportName: match.name,
      exportType: match.type,
      value: match.value,
    };
  }

  function describeLocalRegistry() {
    const components = Object.values(localRegistryState.components).map((component) => ({
      id: component.id,
      title: component.title,
      available: Boolean(component.available),
      moduleId: component.moduleId,
      exportKey: component.exportKey,
      exportName: component.exportName,
      exportType: component.exportType,
    }));

    return {
      version: localRegistryState.version,
      runtimeReady: localRegistryState.runtimeReady,
      moduleCount: localRegistryState.moduleCount,
      availableCount: components.filter((component) => component.available).length,
      totalCount: localComponentDefinitions.length,
      lastRefreshIso: localRegistryState.lastRefreshIso,
      components,
      errors: [...localRegistryState.errors],
    };
  }

  function refreshLocalRegistry(force = false) {
    const now = Date.now();
    if (
      !force &&
      localRegistryState.lastRefreshIso &&
      localRegistryState.runtimeReady &&
      Object.keys(localRegistryState.components).length > 0
    ) {
      return describeLocalRegistry();
    }

    if (!force && localRegistryState.lastRefreshIso && now - lastLocalRegistryAttemptMs < 5000) {
      return describeLocalRegistry();
    }

    lastLocalRegistryAttemptMs = now;
    localRegistryState.errors = [];
    localRegistryState.lastRefreshIso = new Date().toISOString();

    const runtimeRequire = getCurrentContextRuntimeRequire();
    localRegistryState.runtimeReady = Boolean(runtimeRequire);

    if (!runtimeRequire?.m) {
      localRegistryState.moduleCount = 0;
      localRegistryState.components = Object.fromEntries(
        localComponentDefinitions.map((definition) => [
          definition.id,
          createLocalComponentState(definition, null),
        ]),
      );
      return describeLocalRegistry();
    }

    const matches = new Map(localComponentDefinitions.map((definition) => [definition.id, null]));
    const moduleIds = Object.keys(runtimeRequire.m);
    localRegistryState.moduleCount = moduleIds.length;

    for (const moduleId of moduleIds) {
      for (const [exportKey, value] of getModuleExports(runtimeRequire, moduleId)) {
        if (!value) {
          continue;
        }

        for (const definition of localComponentDefinitions) {
          if (!matchesLocalDefinition(definition, exportKey, value)) {
            continue;
          }

          const match = {
            moduleId,
            exportKey,
            type: typeof value,
            name: getTypeName(value),
            score: scoreLocalMatch(definition, exportKey, value),
            value,
          };
          const current = matches.get(definition.id);

          if (!current || match.score > current.score) {
            matches.set(definition.id, match);
          }
        }
      }
    }

    localRegistryState.components = Object.fromEntries(
      localComponentDefinitions.map((definition) => [
        definition.id,
        createLocalComponentState(definition, matches.get(definition.id)),
      ]),
    );

    return describeLocalRegistry();
  }

  function getLocalComponentState(id) {
    refreshLocalRegistry();
    return localRegistryState.components[id] || null;
  }

  function getResolvedNativeComponent(id) {
    return getLocalComponentState(id)?.value || null;
  }

  function walkFiber(root, visitor, limit = 900) {
    const stack = root ? [root] : [];
    const seen = new Set();

    while (stack.length > 0 && seen.size < limit) {
      const node = stack.pop();
      if (!node || seen.has(node)) {
        continue;
      }

      seen.add(node);
      visitor(node);

      if (node.sibling) {
        stack.push(node.sibling);
      }

      if (node.child) {
        stack.push(node.child);
      }
    }
  }

  function getRootFiber(element) {
    let current = getReactFiber(element);
    while (current?.return) {
      current = current.return;
    }

    return current || null;
  }

  function getQuickAccessRootFiber() {
    const root = document.getElementById("QuickAccess-NA");
    const rootKey =
      getReactPropertyKey(root, "__reactFiber") ||
      getReactPropertyKey(root, "__reactContainer");

    return rootKey ? root[rootKey] : null;
  }

  function addCandidate(list, type) {
    const name = getTypeName(type);
    if (!list.some((candidate) => candidate.name === name)) {
      list.push({ name });
    }
  }

  function collectNativeCandidates(state, rootFiber) {
    const candidates = {
      toggles: [],
      choices: [],
      sliders: [],
    };

    walkFiber(rootFiber, (node) => {
      const type = node.elementType || node.type;
      const source = getTypeSource(type);

      if (!source) {
        return;
      }

      if (
        source.includes("ToggleField") ||
        source.includes("DialogCheckbox") ||
        source.includes("bChecked") ||
        source.includes("aria-checked")
      ) {
        addCandidate(candidates.toggles, type);
      }

      if (
        source.includes("Dropdown") ||
        source.includes("DropDown") ||
        source.includes("Combobox") ||
        source.includes("rgOptions") ||
        source.includes("selectedOption")
      ) {
        addCandidate(candidates.choices, type);
      }

      if (source.includes("Slider") || source.includes("onChangeEnd") || source.includes("nMin")) {
        addCandidate(candidates.sliders, type);
      }
    });

    state.nativeUi.componentCandidates = candidates;
    state.nativeUi.steamToggleStyleAvailable = canUseSteamToggleStyle();
  }

  function findDialogButtonType(rootFiber) {
    let dialogButtonType = null;

    walkFiber(rootFiber, (node) => {
      if (dialogButtonType) {
        return;
      }

      const type = node.elementType || node.type;
      const source = getTypeSource(type);

      if (source.includes('"DialogButton"') && source.includes('"Secondary"')) {
        dialogButtonType = type;
      }
    }, 1800);

    return dialogButtonType;
  }

  function canUseSteamToggleStyle() {
    if (steamToggleStyleAvailable !== null) {
      return steamToggleStyleAvailable;
    }

    if (!document?.body) {
      steamToggleStyleAvailable = false;
      return steamToggleStyleAvailable;
    }

    const probe = document.createElement("span");
    probe.className = steamToggleClasses.toggle;
    probe.style.position = "absolute";
    probe.style.left = "-9999px";
    probe.style.top = "-9999px";
    document.body.appendChild(probe);

    const style = getComputedStyle(probe);
    const width = Number.parseFloat(style.width);
    const height = Number.parseFloat(style.height);
    steamToggleStyleAvailable = width >= 30 && height >= 18 && style.borderRadius !== "0px";
    probe.remove();

    return steamToggleStyleAvailable;
  }

  function captureNativeUi(state) {
    if (!state) {
      return false;
    }

    state.nativeUi ??= {};

    if (
      state.nativeUi.dialogButtonType &&
      state.nativeUi.componentCandidates &&
      typeof state.nativeUi.steamToggleStyleAvailable === "boolean"
    ) {
      return true;
    }

    if (typeof state.nativeUi.steamToggleStyleAvailable !== "boolean") {
      state.nativeUi.steamToggleStyleAvailable = canUseSteamToggleStyle();
    }

    state.nativeUi.localRegistrySnapshot = refreshLocalRegistry();
    state.nativeUi.dialogButtonType = state.nativeUi.dialogButtonType || getResolvedNativeComponent("dialogButton");

    const dialogButton = document.querySelector(".DialogButton");
    let current = getReactFiber(dialogButton);
    const rootFiber = getRootFiber(dialogButton) || getQuickAccessRootFiber();

    if (rootFiber && !state.nativeUi.componentCandidates) {
      collectNativeCandidates(state, rootFiber);
    }

    while (current) {
      const renderSource =
        typeof current.elementType?.render?.toString === "function"
          ? current.elementType.render.toString()
          : "";

      if (renderSource.includes('"DialogButton"') && renderSource.includes('"Secondary"')) {
        state.nativeUi.dialogButtonType = current.elementType;
        return Boolean(state.nativeUi.dialogButtonType);
      }

      current = current.return;
    }

    if (!state.nativeUi.dialogButtonType && rootFiber) {
      state.nativeUi.dialogButtonType = findDialogButtonType(rootFiber);
    }

    return Boolean(state.nativeUi.dialogButtonType);
  }

  function playUiSound() {
    const candidates = [
      window.SteamClient?.Audio,
      window.SteamClient?.UI,
      window.SteamClient?.System,
      window.SteamClient,
    ].filter(Boolean);

    for (const target of candidates) {
      for (const methodName of ["PlayUISound", "PlayUiSound", "PlaySound"]) {
        const method = target?.[methodName];
        if (typeof method !== "function") {
          continue;
        }

        try {
          method.call(target, "select");
          return true;
        } catch {
          try {
            method.call(target);
            return true;
          } catch {
          }
        }
      }
    }

    return false;
  }

  function playSoundFile(path) {
    try {
      const audio = new Audio(path);
      audio.volume = 0.72;
      const promise = audio.play();
      if (promise && typeof promise.catch === "function") {
        promise.catch(() => {});
      }
      return true;
    } catch {
      return false;
    }
  }

  function playToggleSound(value) {
    return playSoundFile(
      value
        ? "/sounds/deck_ui_switch_toggle_on.wav"
        : "/sounds/deck_ui_switch_toggle_off.wav",
    );
  }

  function getNativeRegistry(state) {
    return state?.nativeUi?.registrySnapshot || null;
  }

  function getNativeComponent(state, id) {
    const localComponent = getLocalComponentState(id);
    if (localComponent?.available) {
      const { value, ...snapshot } = localComponent;
      return snapshot;
    }

    const components = getNativeRegistry(state)?.components;
    return Array.isArray(components)
      ? components.find((component) => component?.id === id) || null
      : null;
  }

  function isNativeComponentAvailable(state, id) {
    return Boolean(getResolvedNativeComponent(id) || getNativeComponent(state, id)?.available);
  }

  async function refreshComponentRegistry(apiBase, state) {
    if (!apiBase || !state) {
      return null;
    }

    state.nativeUi ??= {};
    if (state.nativeUi.registryLoading) {
      return getNativeRegistry(state);
    }

    state.nativeUi.registryLoading = true;
    state.nativeUi.registryLastAttemptMs = Date.now();

    try {
      const response = await fetch(`${apiBase}api/frontend/components`, { cache: "no-store" });
      const payload = await response.json();
      if (!response.ok) {
        throw new Error(payload.message || `Steam frontend components could not be loaded (${response.status}).`);
      }

      state.nativeUi.registrySnapshot = payload && typeof payload === "object" ? payload : null;
      state.nativeUi.registryError = "";
    } catch (error) {
      state.nativeUi.registryError = error instanceof Error ? error.message : String(error);
    } finally {
      state.nativeUi.registryLoading = false;
    }

    return getNativeRegistry(state);
  }

  function createDialogButton(state, createElement, content, onClick, options = {}) {
    const invoke = (event) => {
      if (options.disabled) {
        return;
      }

      onClick?.(event);
    };

    const commonProps = {
      onClick: invoke,
      onOKButton: invoke,
      onActivate: invoke,
      disabled: Boolean(options.disabled),
      className: options.className || "steamloader-dialog-button",
      children: content,
      ...(options.extraProps || {}),
    };

    const DialogButton = state?.nativeUi?.dialogButtonType || getResolvedNativeComponent("dialogButton");
    if (DialogButton) {
      state.nativeUi ??= {};
      state.nativeUi.dialogButtonType = DialogButton;

      return createElement(DialogButton, {
        ...commonProps,
        focusable: true,
      });
    }

    return createElement("button", {
      type: "button",
      ...commonProps,
      className: "steamloader-fallback-button",
    });
  }

  function renderSwitchAccessory(createElement, withChildren, slot) {
    if (canUseSteamToggleStyle()) {
      return createElement(
        "span",
        withChildren(
          { className: "steamloader-switch-wrap steamtools-native-toggle-wrap", "aria-hidden": "true" },
          createElement(
            "span",
            withChildren(
              {
                className: [
                  "steamtools-native-toggle",
                  slot.switchValue ? "is-on" : "",
                  slot.disabled ? "is-disabled" : "",
                  steamToggleClasses.toggle,
                  slot.switchValue ? steamToggleClasses.on : "",
                  slot.disabled ? steamToggleClasses.disabled : "",
                ]
                  .filter(Boolean)
                  .join(" "),
              },
              createElement("span", {
                className: steamToggleClasses.rail,
              }),
              createElement("span", {
                className: steamToggleClasses.switch,
              }),
            ),
          ),
          slot.switchLabel
            ? createElement("span", {
                className: "steamloader-switch-label",
                children: slot.switchLabel,
              })
            : null,
        ),
      );
    }

    return createElement(
      "span",
      withChildren(
        { className: "steamloader-switch-wrap", "aria-hidden": "true" },
        createElement(
          "span",
          withChildren(
            {
              className: `steamloader-switch${slot.switchValue ? " is-on" : ""}`,
            },
            createElement("span", {
              className: "steamloader-switch-thumb",
            }),
          ),
        ),
        slot.switchLabel
          ? createElement("span", {
              className: "steamloader-switch-label",
              children: slot.switchLabel,
            })
          : null,
      ),
    );
  }

  function renderTrailingContent(createElement, withChildren, slot, helpers = {}) {
    if (typeof slot.switchValue === "boolean") {
      return renderSwitchAccessory(createElement, withChildren, slot);
    }

    if (slot.badge) {
      return createElement("span", {
        className: "steamloader-badge",
        children: slot.badge,
      });
    }

    if (slot.trailing === "none") {
      return null;
    }

    const Icon = slot.trailing === "back" ? helpers.BackIcon : helpers.ChevronIcon;
    return typeof Icon === "function"
      ? createElement(Icon, {})
      : createElement("span", {
          className: "steamloader-row-trailing-glyph",
          children: slot.trailing === "back" ? "<" : ">",
        });
  }

  function createSlot(title, copy, onClick, options = {}) {
    return {
      kind: "button",
      role: options.role || "action",
      title,
      copy: copy || "",
      onClick,
      disabled: Boolean(options.disabled),
      badge: options.badge || "",
      trailing: options.trailing || "chevron",
      switchValue: options.switchValue,
      switchLabel: options.switchLabel || "",
      leadingIcon: options.leadingIcon || null,
      buttonClassName: options.buttonClassName || "",
      rowClassName: options.rowClassName || "",
      selected: Boolean(options.selected),
      value: options.value,
      nativeComponentId:
        options.nativeComponentId || nativeComponentByRole[options.role || "action"] || "dialogButton",
    };
  }

  function createNavigationSlot(title, copy, onClick, options = {}) {
    return createSlot(title, copy, onClick, {
      ...options,
      role: "navigation",
      trailing: options.trailing || "chevron",
      nativeComponentId: options.nativeComponentId || "dialogButton",
    });
  }

  function createBackSlot(title, copy, onClick, options = {}) {
    return createSlot(title, copy, onClick, {
      ...options,
      role: "back",
      trailing: options.trailing || "back",
      nativeComponentId: options.nativeComponentId || "dialogButton",
    });
  }

  function createToggleSlot(title, copy, value, onClick, options = {}) {
    return createSlot(title, copy, onClick, {
      ...options,
      role: "toggle",
      trailing: "none",
      switchValue: value,
      nativeComponentId: options.nativeComponentId || "toggleField",
    });
  }

  function createSettingToggleSlot(scope, key, title, copy, value, onClick, options = {}) {
    return {
      ...createToggleSlot(title, copy, value, onClick, options),
      settingScope: scope || "",
      settingKey: key || "",
    };
  }

  function createChoiceSlot(title, copy, onClick, options = {}) {
    return createSlot(title, copy, onClick, {
      ...options,
      role: "choice",
      badge: options.badge || options.value || "",
      selected: Boolean(options.selected || options.badge === "Selected"),
      nativeComponentId: options.nativeComponentId || "dropdown",
    });
  }

  function createCommandSlot(title, copy, onClick, options = {}) {
    return createSlot(title, copy, onClick, {
      ...options,
      role: "command",
      trailing: options.trailing || "none",
    });
  }

  function createScreenModel(overrides = {}) {
    return {
      ...defaultModel,
      ...overrides,
      cards: Array.isArray(overrides.cards) ? overrides.cards : [],
      slots: Array.isArray(overrides.slots) ? overrides.slots : [],
    };
  }

  function buildRowClassName(slot) {
    const roleClassName = slot.role ? ` steamtools-row-${slot.role}` : "";

    if (slot.leadingIcon) {
      return slot.rowClassName
        ? `steamloader-row-shell steamloader-row-shell-with-icon${roleClassName} ${slot.rowClassName}`
        : `steamloader-row-shell steamloader-row-shell-with-icon${roleClassName}`;
    }

    return slot.rowClassName
      ? `steamloader-row-shell${roleClassName} ${slot.rowClassName}`
      : `steamloader-row-shell${roleClassName}`;
  }

  function createRowContent(createElement, withChildren, slot, trailingNode) {
    return createElement(
      "div",
      withChildren(
        { className: buildRowClassName(slot) },
        slot.leadingIcon
          ? createElement(
              "div",
              withChildren(
                { className: "steamloader-row-icon" },
                createElement(slot.leadingIcon, {}),
              ),
            )
          : null,
        createElement(
          "div",
          withChildren(
            { className: "steamloader-row-main" },
            createElement("div", {
              className: "steamloader-row-title",
              children: slot.title,
            }),
            slot.copy
              ? createElement("div", {
                  className: "steamloader-row-copy",
                  children: slot.copy,
                })
              : null,
          ),
        ),
        createElement(
          "div",
          withChildren(
            { className: "steamloader-row-trailing" },
            trailingNode,
          ),
        ),
      ),
    );
  }

  function createRoleProps(slot) {
    const role = slot.role || "action";
    const props = {
      "data-slot-role": role,
    };

    if (role === "toggle") {
      props.role = "switch";
      props["aria-checked"] = Boolean(slot.switchValue);
    }

    if (role === "choice") {
      props.role = "option";
      props["aria-selected"] = Boolean(slot.selected);
    }

    return props;
  }

  function invokeSlotAction(state, slot, index, helpers) {
    if (slot.disabled) {
      return;
    }

    if (slot.role === "toggle") {
      playToggleSound(!slot.switchValue);
    } else if (!state?.nativeUi?.dialogButtonType) {
      playUiSound();
    }

    helpers.handleSlotClick(index);
  }

  function createButtonSlot(state, createElement, withChildren, slot, index, autoFocusIndex, helpers) {
    const backNavigation = typeof helpers.getBackNavigation === "function"
      ? helpers.getBackNavigation()
      : null;
    const roleProps = createRoleProps(slot);
    const nativeComponentId = slot.nativeComponentId || nativeComponentByRole[slot.role || "action"] || "dialogButton";
    const nativeAvailable =
      isNativeComponentAvailable(state, nativeComponentId) ||
      isNativeComponentAvailable(state, "dialogButton");

    return createDialogButton(
      state,
      createElement,
      createRowContent(
        createElement,
        withChildren,
        slot,
        typeof helpers.renderTrailingContent === "function"
          ? helpers.renderTrailingContent(slot)
          : renderTrailingContent(createElement, withChildren, slot, helpers),
      ),
      () => invokeSlotAction(state, slot, index, helpers),
      {
        disabled: slot.disabled,
        className: slot.buttonClassName || "steamloader-dialog-button",
        extraProps: {
          ...roleProps,
          "data-slot-button": String(index),
          "data-native-component": nativeComponentId,
          "data-native-component-ready": nativeAvailable ? "true" : "false",
          "data-setting-scope": slot.settingScope || undefined,
          "data-setting-key": slot.settingKey || undefined,
          autoFocus: Number.isInteger(autoFocusIndex) && index === autoFocusIndex,
          onCancelButton: backNavigation
            ? () => {
                helpers.navigateBackFromRoute();
              }
            : undefined,
        },
      },
    );
  }

  function createInfoCard(createElement, withChildren, card, index) {
    return createElement(
      "div",
      withChildren(
        {
          className: "steamloader-card",
          key: `card-${index}`,
        },
        createElement("div", {
          className: "steamloader-card-title",
          children: card.title,
        }),
        ...(Array.isArray(card.lines) ? card.lines : []).map((line, lineIndex) =>
          createElement("div", {
            className: "steamloader-card-line",
            key: `card-line-${index}-${lineIndex}`,
            children: line,
          }),
        ),
      ),
      `steamloader-card-${index}`,
    );
  }

  function createEditorCard(createElement, withChildren, editor) {
    return createElement(
      "div",
      withChildren(
        {
          className: "steamloader-editor-card",
        },
        createElement("div", {
          className: "steamloader-editor-label",
          children: editor.label,
        }),
        editor.help
          ? createElement("div", {
              className: "steamloader-editor-help",
              children: editor.help,
            })
          : null,
        createElement("textarea", {
          key: editor.inputKey,
          className: "steamloader-editor-textarea",
          "data-custom-path-input": editor.isCustomPath ? "true" : undefined,
          defaultValue: editor.value || "",
          placeholder: editor.placeholder || "",
          rows: editor.rows || 3,
          spellCheck: false,
          autoCapitalize: "off",
          autoCorrect: "off",
          autoComplete: "off",
          onClick: (event) => {
            event.stopPropagation();
          },
          onInput: (event) => {
            editor.onInput?.(event.target.value);
          },
        }),
      ),
      "steamloader-editor",
    );
  }

  function createDivider(createElement, key) {
    return createElement("div", {
      className: "steamloader-divider",
      key,
      "aria-hidden": "true",
    });
  }

  function createVolumeActionButton(state, createElement, withChildren, action, index, helpers = {}) {
    return createDialogButton(
      state,
      createElement,
      createElement(
        "div",
        withChildren(
          { className: "steamloader-volume-action-shell" },
          createElement("div", {
            className: "steamloader-volume-action-title",
            children: action.title,
          }),
        ),
      ),
      action.onClick,
      {
        disabled: action.disabled,
        className: "steamloader-dialog-button steamloader-volume-action-button",
        extraProps: {
          autoFocus: action.autoFocus && helpers.getActiveVolumeActionIndex?.() === index,
          onGamepadFocus: () => {
            helpers.rememberVolumeActionFocus?.(index);
          },
          onCancelButton: () => {
            action.onCancel?.();
          },
          style: {
            width: "100%",
            minWidth: 0,
            padding: "8px 10px",
          },
        },
      },
    );
  }

  function createVolumePanel(state, createElement, withChildren, panel, helpers = {}) {
    const shouldAutoFocusAction = Boolean(helpers.consumeVolumeActionAutoFocus?.());

    return createElement(
      "div",
      withChildren(
        { className: "steamloader-volume-card" },
        createElement(
          "div",
          withChildren(
            { className: "steamloader-volume-head" },
            createElement(
              "div",
              withChildren(
                { className: "steamloader-volume-copy-wrap" },
                createElement("div", {
                  className: "steamloader-volume-title",
                  children: panel.title,
                }),
                createElement("div", {
                  className: "steamloader-volume-copy",
                  children: panel.copy,
                }),
              ),
            ),
          ),
        ),
        createElement("div", {
          className: panel.error
            ? "steamloader-volume-hint steamloader-volume-hint-error"
            : "steamloader-volume-hint",
          children: panel.error || panel.hint,
        }),
        createElement(
          "div",
          withChildren(
            { className: "steamloader-volume-actions" },
            ...(Array.isArray(panel.actions) ? panel.actions : []).map((action, index) =>
              createVolumeActionButton(
                state,
                createElement,
                withChildren,
                {
                  ...action,
                  autoFocus: shouldAutoFocusAction,
                },
                index,
                helpers,
              ),
            ),
          ),
        ),
      ),
    );
  }

  function createPanelShell(state, createElement, withChildren, model, helpers = {}) {
    state.nativeUi ??= {};
    state.nativeUi.renderError = "";

    const HeaderIcon = model.headerIcon || helpers.DefaultIcon;
    const slots = Array.isArray(model.slots) ? model.slots : [];
    state.slotActions = slots.map((slot) => slot.onClick);
    helpers.consumeResolvedFocus?.(state.route, model.autoFocusIndex);

    const slotChildren = slots.flatMap((slot, index) => {
      const children = [
        createButtonSlot(state, createElement, withChildren, slot, index, model.autoFocusIndex, helpers),
      ];

      if (Number.isInteger(model.dividerAfterIndex) && index === model.dividerAfterIndex) {
        children.push(createDivider(createElement, `divider-${index}`));
      }

      return children;
    });

    return createElement(
      "div",
      withChildren(
        {
          className: "steamloader-panel",
          "data-st-frontend-lib-version": String(window.STFrontendLib?.version || 12),
          "data-st-renderer": "st-frontend-lib",
        },
        createElement(
          "div",
          withChildren(
            { className: "steamloader-header" },
            HeaderIcon
              ? createElement(
                  "div",
                  withChildren({ className: "steamloader-header-mark" }, createElement(HeaderIcon, {})),
                )
              : null,
            createElement(
              "div",
              withChildren(
                { className: "steamloader-title-wrap" },
                createElement("h1", {
                  className: "steamloader-title",
                  children: model.title,
                }),
                model.subtitle
                  ? createElement("div", {
                      className: "steamloader-subtitle",
                      children: model.subtitle,
                    })
                  : null,
              ),
            ),
          ),
        ),
        model.status
          ? createElement("div", {
              className: "steamloader-status",
              children: model.status,
            })
          : null,
        model.error
          ? createElement("div", {
              className: "steamloader-error",
              children: model.error,
            })
          : null,
        model.note
          ? createElement("div", {
              className: "steamloader-note",
              children: model.note,
            })
          : null,
        ...(Array.isArray(model.cards)
          ? model.cards.map((card, index) => createInfoCard(createElement, withChildren, card, index))
          : []),
        model.editor ? createEditorCard(createElement, withChildren, model.editor) : null,
        createElement(
          "div",
          withChildren(
            { className: "steamloader-stack" },
            ...slotChildren,
          ),
        ),
        model.volumePanel
          ? createVolumePanel(state, createElement, withChildren, model.volumePanel, helpers)
          : null,
      ),
    );
  }

  function createDiagnostics(state) {
    const registry = getNativeRegistry(state);
    const localRegistry = refreshLocalRegistry();

    return {
      version: 12,
      renderer: "st-frontend-lib",
      hasDialogButtonType: Boolean(state?.nativeUi?.dialogButtonType),
      steamToggleStyleAvailable: Boolean(state?.nativeUi?.steamToggleStyleAvailable),
      localRegistryAvailable: localRegistry.availableCount || 0,
      localRegistryTotal: localRegistry.totalCount || 0,
      registryVersion: registry?.version || 0,
      registryAvailable: registry?.availableCount || 0,
      registryTotal: registry?.totalCount || 0,
      lastRenderError: state?.nativeUi?.renderError || "",
    };
  }

  window.STFrontendLib = {
    version: 12,
    defaultModel,
    getReactPropertyKey,
    getReactFiber,
    getQuickAccessRootFiber,
    captureNativeUi,
    playUiSound,
    playToggleSound,
    getNativeRegistry,
    getNativeComponent,
    isNativeComponentAvailable,
    refreshLocalRegistry,
    getResolvedNativeComponent,
    refreshComponentRegistry,
    canUseSteamToggleStyle,
    createDialogButton,
    renderSwitchAccessory,
    renderTrailingContent,
    createSlot,
    createNavigationSlot,
    createBackSlot,
    createToggleSlot,
    createSettingToggleSlot,
    createChoiceSlot,
    createCommandSlot,
    createScreenModel,
    buildRowClassName,
    createRowContent,
    createRoleProps,
    createButtonSlot,
    createInfoCard,
    createEditorCard,
    createDivider,
    createVolumeActionButton,
    createVolumePanel,
    createPanelShell,
    createDiagnostics,
  };
})();
