(() => {
  const existing = window.SteamToolsFrontendRegistry;
  if (existing?.version >= 2) {
    existing.refresh?.();
    return "injected";
  }

  const componentDefinitions = [
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
  ];

  const state = {
    version: 2,
    runtimeReady: false,
    moduleCount: 0,
    lastRefreshIso: null,
    components: {},
    errors: [],
  };

  function getRuntimeRequire() {
    if (window.__steamToolsWebpackRequire) {
      return window.__steamToolsWebpackRequire;
    }

    const chunk = window.webpackChunksteamui;
    if (!Array.isArray(chunk) || typeof chunk.push !== "function") {
      return null;
    }

    let runtimeRequire = null;
    try {
      chunk.push([[`steam-tools-registry-${Date.now()}`], {}, (require) => {
        runtimeRequire = require;
        window.__steamToolsWebpackRequire = require;
      }]);
    } catch (error) {
      state.errors.push(`Unable to capture Steam webpack runtime: ${String(error?.message || error)}`);
    }

    return runtimeRequire;
  }

  function getSource(value) {
    let source = "";

    try {
      if (typeof value === "function") {
        source += value.toString();
      }
    } catch {
    }

    try {
      if (typeof value?.render === "function") {
        source += `\n${value.render.toString()}`;
      }
    } catch {
    }

    return source;
  }

  function getName(value) {
    return value?.displayName || value?.name || value?.render?.displayName || value?.render?.name || "";
  }

  function isComponentLike(value) {
    return typeof value === "function" || typeof value?.render === "function";
  }

  function getExports(runtimeRequire, moduleId) {
    try {
      const exportsObject = runtimeRequire(moduleId);
      return exportsObject && typeof exportsObject === "object"
        ? Object.entries(exportsObject)
        : [["default", exportsObject]];
    } catch {
      return [];
    }
  }

  function matchesDefinition(definition, exportKey, value) {
    if (!isComponentLike(value)) {
      return false;
    }

    const name = getName(value);
    const source = getSource(value);
    const searchable = `${exportKey}\n${name}\n${source}`;

    return definition.required.every((needle) => searchable.includes(needle));
  }

  function scoreMatch(definition, exportKey, value) {
    let score = definition.required.length * 10;
    const name = getName(value);

    if (definition.preferredExports.includes(exportKey)) {
      score += 20;
    }

    if (name) {
      score += 2;
    }

    if (typeof value?.render === "function") {
      score += 2;
    }

    return score;
  }

  function createComponentSnapshot(definition, match) {
    if (!match) {
      return {
        id: definition.id,
        title: definition.title,
        available: false,
        moduleId: null,
        exportKey: null,
        exportName: "",
        exportType: "",
        sourcePreview: "",
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
      sourcePreview: match.source.slice(0, 260),
    };
  }

  function refresh() {
    state.errors = [];
    state.lastRefreshIso = new Date().toISOString();

    const runtimeRequire = getRuntimeRequire();
    state.runtimeReady = Boolean(runtimeRequire);

    if (!runtimeRequire?.m) {
      state.moduleCount = 0;
      state.components = Object.fromEntries(
        componentDefinitions.map((definition) => [
          definition.id,
          createComponentSnapshot(definition, null),
        ]),
      );
      return describe();
    }

    const moduleIds = Object.keys(runtimeRequire.m);
    state.moduleCount = moduleIds.length;
    const matchesByComponent = new Map(componentDefinitions.map((definition) => [definition.id, null]));

    for (const moduleId of moduleIds) {
      const exportsEntries = getExports(runtimeRequire, moduleId);

      for (const [exportKey, value] of exportsEntries) {
        if (!value) {
          continue;
        }

        for (const definition of componentDefinitions) {
          if (!matchesDefinition(definition, exportKey, value)) {
            continue;
          }

          const source = getSource(value);
          const match = {
            moduleId,
            exportKey,
            type: typeof value,
            name: getName(value),
            score: scoreMatch(definition, exportKey, value),
            source,
          };
          const current = matchesByComponent.get(definition.id);

          if (!current || match.score > current.score) {
            matchesByComponent.set(definition.id, match);
          }
        }
      }
    }

    state.components = Object.fromEntries(
      componentDefinitions.map((definition) => [
        definition.id,
        createComponentSnapshot(definition, matchesByComponent.get(definition.id)),
      ]),
    );

    return describe();
  }

  function describe() {
    const components = Object.values(state.components);

    return {
      version: state.version,
      runtimeReady: state.runtimeReady,
      moduleCount: state.moduleCount,
      availableCount: components.filter((component) => component.available).length,
      totalCount: componentDefinitions.length,
      lastRefreshIso: state.lastRefreshIso,
      components,
      errors: [...state.errors],
    };
  }

  function getComponent(id) {
    return state.components[id] || null;
  }

  window.SteamToolsFrontendRegistry = {
    version: state.version,
    refresh,
    describe,
    getComponent,
  };

  refresh();

  return "injected";
})();
