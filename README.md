# Craft Console ⚡

> **A fast, keyboard-first HUD command palette and workflow companion for EffectCraft and Adobe After Effects.**

Craft Console brings lightning-fast effect searching, preset browsing, command palette execution, and clipboard screenshot management directly to your fingertips via a floating search bar positioned at your cursor.

---

## 🚀 How to Install & Upgrade

### Option A: Portable Bundle (.zip)
1. Download **`CraftConsole-v1.0.2-portable.zip`** from [Releases](https://github.com/bravoyush/craft-console/releases).
2. Extract the archive anywhere on your machine (or directly inside your `effectcraft` directory).
3. Run `CraftConsole.exe`.

### Option B: Standalone Executable (.exe)
1. Download **`CraftConsole.exe`** from [Releases](https://github.com/bravoyush/craft-console/releases).
2. Run `CraftConsole.exe` from anywhere (Downloads, Desktop, etc.).
3. **Smart Installation**:
   - Craft Console automatically detects your `effectcraft.exe` installation.
   - It installs itself permanently into your EffectCraft folder (or `%LOCALAPPDATA%\CraftConsole\`), ensuring that **deleting your downloaded installer/zip from Downloads will never break your application**.
   - Select your preferred shortcuts: **Unified 'EffectCraft (with Console)'**, **Craft Console Desktop**, and **Start Menu**.
   - Click **Save & Launch** — your settings are remembered permanently across launches.

---

## 🔄 How to Upgrade from an Existing Version

* **Upgrading the Portable Version**:
  1. Download **`CraftConsole-v1.0.2-portable.zip`**.
  2. Extract and replace `CraftConsole.exe` in your existing folder.
  3. *Your existing favorites, custom themes, and configuration (`splash.ini` / `config.ini`) are completely preserved.*
* **Upgrading the Installed Version**:
  1. Download **`CraftConsole.exe`** v1.0.2.
  2. Run it once and click **Save & Launch** to update the installed executable and refresh shortcuts.

---

## 🗑️ Clean Uninstallation

Craft Console includes a built-in uninstaller:
* **Via Settings**: Open Settings (⚙) ▸ scroll to *Installation & Integration* ▸ click **Uninstall Craft Console...**
* **Via Command Line**: Run `CraftConsole.exe --uninstall`
* **Interactive Options**:
  - Choose to remove shortcuts and integration while **keeping your custom favorites, presets, and splash themes**, or
  - Choose complete removal to wipe all data cleanly.

---

## 🎮 Summoning the Console (Ctrl + Space)
- Press **`Ctrl + Space`** anytime while inside EffectCraft (or After Effects).
- The console capsule immediately spawns right at your mouse cursor.
- Type any effect name (e.g. `Gaussian Blur`, `Curves`, `Roughen Edges`) and press **Enter** to instantly apply it to your selected layer!

### 🔍 Smart Filtering
Type **`@`** or **`#`** in the search bar to filter by category:
- **`@fx `** — Filter effects only.
- **`@preset `** — Filter animation and text animator presets.
- **`@fav `** or **`*`** — Show your saved favorites.
- **`@layer `** — Instant layer creation (`Solid`, `Null`, `Adjustment`, `Camera`, `Light`, `Shape`, `Text`).
- **`@cmd `** — Search and execute EffectCraft engine commands (Composition, Edit, File, View, Window, Help).
- **`@recent `** — Search and open recent `.ecproj` project files.
- **`@sc `** or **`@screenshot `** — View your current session capture gallery with live thumbnails, comp names, timecodes, and timestamps.

### ⭐ Favorites System
- Star/favorite any effect, preset, or command for instant access.
- Filter by favorites using `/fav` or typing `*`.
- Customize your favorite toggle keybinding in Settings (default: `Ctrl + F`).
- Instant $O(N)$ priority partitioning puts your favorites at the top of search results without keystroke lag.

### 📸 Header Actions
- **📷 Camera**: Takes a snapshot of the current composition frame and copies it directly to your Windows clipboard (`Ctrl + V`).
- **🗂 Overlapping Screens**: Opens your `@sc` Session Capture Gallery.
- **📥 Download Arrow**: Exports the active frame as a timestamped PNG file straight to your Windows Desktop.
- **⚙ Gear Icon**: Opens the full **Craft Console & Studio Settings** customizer window.
- **⎋ Escape**: Dismisses the console instantly.

---

## ✨ Release Highlights & Fixes (v1.0.2)

- **Fixed "Rough Edges" Error ([Issue #1](https://github.com/bravoyush/craft-console/issues/1))**:
  - EffectCraft's engine registers this effect under its official name **`"Roughen Edges"`** (`ec.stylize.roughenedges`).
  - Corrected display name to `"Roughen Edges"` and added `"Rough Edges"` as an alias search keyword.
- **Corrected 10 Mismatched Engine Effect IDs**:
  - Updated *Levels (Individual Controls)* (`levelsic`), *Polar Coordinates* (`polar`), *Linear Color Key*, *Color Difference Key*, *Color Range*, *Difference Matte*, *Extract*, *Key Cleaner*, *Spill Suppressor*, and *Strobe Light* (`strobe`).
- **Engine Parity & Error Toasts**:
  - `ApplySelectedItem` now passes canonical `ec.*` IDs directly to `effect.apply`.
  - Engine warnings (e.g., missing active layer/composition) now surface as clear visual toasts rather than failing silently.
  - Live menu sync (`ui.menu.list`) extracts genuine engine IDs from `params.effect`.
- **Typing Lag & Performance**:
  - Background asynchronous menu caching on startup eliminates synchronous TCP stalls during search.
  - Replaced sorting bottlenecks with fast memory partitioning.
- **Recent Projects Auto-Switching**:
  - Opening recent projects now automatically dismisses the Home screen overlay (`ui.set { "home": false }`), taking you straight to your workspace.
- **Permanent Installation & Clean Uninstaller**:
  - Self-installs to a permanent directory so deleting downloads won't break shortcuts.
  - Added full `--uninstall` support with options to preserve or wipe custom themes and favorites.
- **Unified Shortcut**:
  - Launch EffectCraft and Craft Console together with a single desktop icon; both close together cleanly on exit.

---

## 🛠 Building from Source

Craft Console is written in pure C# with WPF and requires **zero external SDKs or Visual Studio installations**. It builds with the native C# compiler included in every Windows installation.

```cmd
build.bat
```
The compiled executable will be placed in `bin\CraftConsole.exe`.

---

## 📁 Repository Structure

```text
craft-console/
├── src/
│   └── Launcher.cs                # Pure C# WPF console, setup wizard & tray app
├── scriptui/
│   └── Craft Console.jsx          # Dockable ExtendScript panel companion
├── assets/
│   ├── icon.ico                   # Application icon
│   └── splash.png                 # Default Neon Unicorn splash artwork
├── splash/
│   ├── art.png                    # Splash screen artwork
│   └── splash.ini                 # Default configuration file
├── dist/
│   └── CraftConsole-v1.0.2-portable.zip # Portable distribution package
├── build.bat                      # Windows build script
├── .gitignore                     # Git ignore rules
├── LICENSE                        # MIT License
└── ATTRIBUTION.md                 # Asset attribution & clean-room statement
```

---

## 🛡 License & Clean-Room Standard

- **Code & Project**: Licensed under the [MIT License](LICENSE).
- **Splash Artwork**: Licensed under Creative Commons Attribution 4.0 International ([CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)). Author: Ayush.
- **Clean-room**: 100% original code and assets created without copying Adobe proprietary code or artwork.
