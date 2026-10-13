# Craft Console ⚡

> **A fast, keyboard-first HUD command palette and workflow companion for EffectCraft and Adobe After Effects.**

Craft Console brings lightning-fast effect searching, preset browsing, command palette execution, and clipboard screenshot management directly to your fingertips via a floating search bar positioned at your cursor.

---

## 🚀 How to Use Craft Console

### Option A: Portable Bundle (.zip)
1. Download **`CraftConsole-v1.0.0-portable.zip`** from [Releases](https://github.com/bravoyush/craft-console/releases).
2. Extract the archive anywhere on your machine (or directly inside your `effectcraft` directory).
3. Run `CraftConsole.exe`.

### Option B: Standalone Executable (.exe)
1. Download **`CraftConsole.exe`** from [Releases](https://github.com/bravoyush/craft-console/releases).
2. Run `CraftConsole.exe` from anywhere (Downloads, Desktop, etc.).
3. **Initial Setup Wizard**:
   - On first launch outside the EffectCraft folder, Craft Console automatically scans and locates your `effectcraft.exe` installation!
   - Select or confirm the location, and optionally check **Create Desktop Shortcut**, **Create Start Menu Shortcut**, and **Install Neon Unicorn Splash Screen**.
   - Click **Save & Launch** — your preferences are remembered permanently across launches.

### 🌟 Self-Contained Splash Screen
- **Zero manual file downloading**: The **Neon Unicorn Glass** splash screen artwork is embedded directly into the executable as a resource and unpacks automatically into `splash/art.png` and `splash.ini`.

---

## 🎮 Summoning the Console (Ctrl + Space)
- Press **`Ctrl + Space`** anytime while inside EffectCraft (or After Effects).
- The console capsule immediately spawns right at your mouse cursor.
- Type any effect name (e.g. `Gaussian Blur`, `Curves`, `Glow`) and press **Enter** to instantly apply it to your selected layer!

### 🔍 Smart Filtering
Type **`@`** or **`#`** in the search bar to filter by category:
- **`@fx `** — Filter effects only.
- **`@preset `** — Filter animation and text animator presets.
- **`@layer `** — Instant layer creation (`Solid`, `Null`, `Adjustment`, `Camera`, `Light`, `Shape`, `Text`).
- **`@cmd `** — Search and execute EffectCraft engine commands (Composition, Edit, File, View, Window, Help).
- **`@recent `** — Search and open recent `.ecproj` project files.
- **`@sc `** or **`@screenshot `** — View your current session capture gallery with live thumbnails, comp names, timecodes, and timestamps.

### 📸 Header Actions
- **📷 Camera**: Takes a snapshot of the current composition frame and copies it directly to your Windows clipboard (`Ctrl + V`).
- **🗂 Overlapping Screens**: Opens your `@sc` Session Capture Gallery.
- **📥 Download Arrow**: Exports the active frame as a timestamped PNG file straight to your Windows Desktop.
- **⚙ Gear Icon**: Opens the full **Craft Console & Studio Settings** customizer window.
- **⎋ Escape**: Dismisses the console instantly.

---

## ✨ Features

- **🎯 Instant Cursor HUD**: Summons directly under your mouse cursor with zero workflow interruption.
- **📦 Self-Contained Embedded Artwork**: Zero extra assets to manually copy or download.
- **🛠 Initial Setup & Shortcut Creator**: Auto-detects EffectCraft, installs desktop shortcuts, and registers system paths.
- **🎨 Automatic Theme Sync**: Dynamically matches EffectCraft active appearance theme (**Dark**, **Darker**, and **Light** modes) as well as Windows system theme.
- **📸 In-Memory Session Captures**: Lightweight rolling history of session captures, automatically purged on restart (zero disk bloat).
- **⚙️ Built-in Customizer**: Configure hotkeys, smart filter prefixes, splash screens, and visible categories.
- **🔌 Companion ScriptUI Panel**: Includes a dockable `Craft Console.jsx` panel for running inside workspace docks.

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
│   └── CraftConsole-v1.0.0-portable.zip # Portable distribution package
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
