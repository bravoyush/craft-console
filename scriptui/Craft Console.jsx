// EC Console: 1:1 clean-room floating command palette for EffectCraft.
//
// Matches EC Console layout & states:
// - State 1 (Image 1): Slim search capsule [ 🔍 Fx ... 📷 🖼 📥 ⚙ ]
// - State 2 (Image 3): When no layer is selected -> shows "No layer is selected."
// - State 3 (Image 4): When typing -> expands categorized list [ • Effects / • Actions ]

(function fxConsole(thisObj) {
  var SECTION = "EC Console";
  var KEY_FAVORITES = "userFavorites";

  // ------------------------------------------------------------------ helpers

  function getComp() {
    return (app.project && app.project.activeItem instanceof CompItem) ? app.project.activeItem : null;
  }

  function getSelectedLayers() {
    var comp = getComp();
    if (!comp) return [];
    var sel = comp.selectedLayers;
    return (sel && sel.length) ? sel : [];
  }

  function loadCatalog() {
    var list = [];
    try {
      var allFx = app.effects;
      if (allFx && allFx.length) {
        for (var i = 0; i < allFx.length; i++) {
          var ef = allFx[i];
          list.push({
            id: ef.matchName || ef.displayName,
            name: ef.displayName,
            category: ef.category || "Effects",
            type: "effect"
          });
        }
      }
    } catch (e) { }

    // Add EffectCraft built-in Animation Presets (Text Animators)
    var textPresets = [
      { id: "typewriter", name: "Typewriter" },
      { id: "fadeUpCharacters", name: "Fade Up Characters" },
      { id: "bounceInWords", name: "Bounce In Words" },
      { id: "trackingIn", name: "Tracking In" },
      { id: "scramble", name: "Scramble" },
      { id: "blurIn", name: "Blur In" },
      { id: "jitter", name: "Jitter" },
      { id: "dropInLines", name: "Drop In Lines" }
    ];
    for (var tp = 0; tp < textPresets.length; tp++) {
      list.push({
        id: textPresets[tp].id,
        name: textPresets[tp].name,
        category: "Animation Presets",
        type: "preset"
      });
    }

    // Add quick utility actions
    var actions = [
      "Solid Layer", "Null Object", "Adjustment Layer", "Shape Layer", "Text Layer", "Camera", "Light"
    ];
    for (var a = 0; a < actions.length; a++) {
      list.push({ id: actions[a], name: actions[a], category: "Actions", type: "action" });
    }
    return list;
  }

  function applyItem(item) {
    var comp = getComp();
    if (!comp) return;

    if (item.type === "effect") {
      try {
        app.run("effect.apply", { effect: item.id });
        return;
      } catch (e) { }
      var layers = getSelectedLayers();
      if (!layers.length) return;
      app.beginUndoGroup("Apply " + item.name);
      try {
        for (var i = 0; i < layers.length; i++) {
          var ep = layers[i].property("Effects") || layers[i].property("ADBE Effect Parade");
          if (ep) { try { ep.addProperty(item.name); } catch (e2) { } }
        }
      } finally {
        app.endUndoGroup();
      }
    } else if (item.type === "preset") {
      try {
        app.run("layer.applyTextPreset", { preset: item.id });
      } catch (e3) { }
    } else {
      // Action
      app.beginUndoGroup(item.name);
      try {
        if (item.name === "Solid Layer") comp.layers.addSolid([0.2, 0.2, 0.2], "Solid", comp.width, comp.height, comp.pixelAspect || 1.0);
        else if (item.name === "Null Object") comp.layers.addNull();
        else if (item.name === "Adjustment Layer") {
          var adj = comp.layers.addSolid([1, 1, 1], "Adjustment Layer", comp.width, comp.height, comp.pixelAspect || 1.0);
          adj.adjustmentLayer = true;
        }
        else if (item.name === "Shape Layer") comp.layers.addShape();
        else if (item.name === "Text Layer") comp.layers.addText("Text");
        else if (item.name === "Camera") comp.layers.addCamera("Camera 1", [comp.width / 2, comp.height / 2]);
        else if (item.name === "Light") comp.layers.addLight("Light 1", [comp.width / 2, comp.height / 2]);
      } finally {
        app.endUndoGroup();
      }
    }
  }

  // ------------------------------------------------------------------ UI Construction

  var comp = getComp();
  var selLayers = getSelectedLayers();
  var hasSelection = selLayers.length > 0;

  var isDocked = (thisObj instanceof Panel);
  var win = isDocked
    ? thisObj
    : new Window("palette", "EC Console", undefined, { resizeable: false });

  win.orientation = "column";
  win.alignChildren = ["fill", "top"];
  win.spacing = 0;
  win.margins = 4;

  // --- HEADER ROW (Images 1 & 4) ---
  var header = win.add("group");
  header.orientation = "row";
  header.alignChildren = ["fill", "center"];
  header.spacing = 4;
  header.preferredSize = [390, 32];

  var searchIcon = header.add("statictext", undefined, "🔍");
  searchIcon.preferredSize = [20, 26];

  var searchBox = header.add("edittext", undefined, "");
  searchBox.preferredSize = [230, 26];
  searchBox.helpTip = "Fx (type effect name)";

  // Action icons on the right
  var snapBtn = header.add("button", undefined, "📷");
  snapBtn.preferredSize = [28, 26];
  snapBtn.helpTip = "Take Snapshot";

  var galleryBtn = header.add("button", undefined, "🖼");
  galleryBtn.preferredSize = [28, 26];
  galleryBtn.helpTip = "Toggle Snapshot Gallery View";

  var exportBtn = header.add("button", undefined, "📥");
  exportBtn.preferredSize = [28, 26];
  exportBtn.helpTip = "Export Frame As PNG";

  var settingsBtn = header.add("button", undefined, "⚙");
  settingsBtn.preferredSize = [28, 26];
  settingsBtn.helpTip = "Settings";

  // --- NOTICE / STATUS BAR (Image 3: "No layer is selected.") ---
  var noticeBar = win.add("statictext", undefined, "");
  noticeBar.preferredSize = [390, 22];

  if (!hasSelection) {
    noticeBar.text = "  No layer is selected.";
    noticeBar.visible = true;
  } else {
    noticeBar.visible = false;
  }

  // --- RESULTS DROPDOWN (Image 4: Curated effect list) ---
  var catalog = loadCatalog();
  var activeMatches = [];

  var resultsList = win.add("listbox", undefined, []);
  resultsList.preferredSize = [390, 220];
  resultsList.visible = false;

  function filterCatalog(query) {
    resultsList.removeAll();
    activeMatches = [];

    query = String(query || "").replace(/^\s+|\s+$/g, "").toLowerCase();
    if (!query) {
      resultsList.visible = false;
      if (!hasSelection) {
        noticeBar.visible = true;
        noticeBar.text = "  No layer is selected.";
      } else {
        noticeBar.visible = false;
      }
      safeLayout();
      return;
    }

    // Separate into Effects, Presets and Actions
    var fxMatches = [];
    var presetMatches = [];
    var actMatches = [];

    for (var i = 0; i < catalog.length; i++) {
      var it = catalog[i];
      if (it.name.toLowerCase().indexOf(query) >= 0) {
        if (it.type === "effect") fxMatches.push(it);
        else if (it.type === "preset") presetMatches.push(it);
        else actMatches.push(it);
      }
    }

    var totalMatches = fxMatches.length + presetMatches.length + actMatches.length;
    if (totalMatches === 0) {
      resultsList.visible = false;
      noticeBar.visible = true;
      noticeBar.text = "  No matching effects or presets.";
      safeLayout();
      return;
    }

    resultsList.visible = true;
    noticeBar.visible = false;

    // Add • Effects header and items (Image 4)
    if (fxMatches.length > 0) {
      resultsList.add("item", "• Effects");
      activeMatches.push(null); // header marker

      for (var f = 0; f < fxMatches.length; f++) {
        if (f >= 30) break;
        resultsList.add("item", "    " + fxMatches[f].name);
        activeMatches.push(fxMatches[f]);
      }
    }

    // Add • Animation Presets header and items
    if (presetMatches.length > 0) {
      resultsList.add("item", "• Animation Presets");
      activeMatches.push(null);

      for (var p = 0; p < presetMatches.length; p++) {
        if (p >= 30) break;
        resultsList.add("item", "    " + presetMatches[p].name);
        activeMatches.push(presetMatches[p]);
      }
    }

    // Add • Actions header and items
    if (actMatches.length > 0) {
      resultsList.add("item", "• Actions");
      activeMatches.push(null);

      for (var a = 0; a < actMatches.length; a++) {
        resultsList.add("item", "    " + actMatches[a].name);
        activeMatches.push(actMatches[a]);
      }
    }

    // Auto-select first real item
    for (var s = 0; s < activeMatches.length; s++) {
      if (activeMatches[s]) {
        resultsList.selection = s;
        break;
      }
    }

    safeLayout();
  }

  function commitSelection() {
    try {
      if (resultsList.selection) {
        var item = activeMatches[resultsList.selection.index];
        if (item) {
          applyItem(item);
          if (!isDocked) win.close();
        }
      }
    } catch (e) { }
  }

  function safeLayout() {
    try {
      if (win && win.layout && typeof win.layout.layout === "function") {
        win.layout.layout(true);
      }
    } catch (e) { }
  }

  // ------------------------------------------------------------------ Events

  searchBox.onChanging = function () {
    try {
      filterCatalog(searchBox.text);
    } catch (e) { }
  };

  searchBox.onChange = function () {
    try {
      commitSelection();
    } catch (e) { }
  };

  resultsList.onDoubleClick = function () {
    try {
      commitSelection();
    } catch (e) { }
  };

  snapBtn.onClick = function () {
    try {
      app.run("view.takeSnapshot", { scale: 1.0 });
      noticeBar.text = "  Snapshot captured";
      noticeBar.visible = true;
    } catch (e) { }
  };

  galleryBtn.onClick = function () {
    try {
      app.run("view.showSnapshot", {});
      noticeBar.text = "  Toggled snapshot view";
      noticeBar.visible = true;
    } catch (e) { }
  };

  exportBtn.onClick = function () {
    try {
      app.run("comp.saveFrameAs", {});
    } catch (e) { }
  };

  settingsBtn.onClick = function () {
    try {
      alert("EC Console for EffectCraft\n• Type any effect name to filter\n• Press Enter or double-click to apply\n• 📷 Take snapshot | 🖼 View snapshot | 📥 Export frame");
    } catch (e) { }
  };

  // ------------------------------------------------------------------ Launch

  try {
    if (win instanceof Window) {
      win.center();
      win.show();
      searchBox.active = true;
    } else {
      safeLayout();
    }
  } catch (err) { }
})(this);
