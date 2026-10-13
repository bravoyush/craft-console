# Craft Console ⚡

> **A fast, keyboard-first HUD command palette and workflow companion for EffectCraft and Adobe After Effects.**

Craft Console brings lightning-fast effect searching, preset browsing, command palette execution, and clipboard screenshot management directly to your fingertips via a floating search bar positioned at your cursor.

---

## ✨ Features

- **🎯 Instant Cursor HUD**: Summons directly under your mouse cursor with `Ctrl + Space` (customizable) with zero workflow interruption.
- **🔍 Smart Filtering**: Type `@` or `#` to filter categories dynamically:
  - `@fx` — Browse 300+ effects with fuzzy-search.
  - `@preset` — Browse animation and text animator presets.
  - `@layer` — Instant layer creation (`Solid`, `Null`, `Adjustment`, `Camera`, `Light`, `Shape`, `Text`).
  - `@cmd` — Search and run EffectCraft engine commands (Composition, Edit, File, View, Window, Help).
  - `@recent` — Search and open recent `.ecproj` project files.
  - `@sc` / `@screenshot` — Live session capture gallery with scaled visual thumbnails, composition name, timecode, resolution, and timestamps.
- **🎨 Automatic Theme Sync**: Dynamically matches EffectCraft's active appearance theme (**Dark**, **Darker**, and **Light** modes) as well as Windows system theme.
- **📸 1-Click Frame Capture**:
  - **📷 Snapshot**: Renders the current frame and copies the bitmap directly to your Windows clipboard (`Ctrl + V`).
  - **📥 Export PNG**: Saves the current frame as a high-resolution PNG straight to your Desktop.
- **⚙️ Built-in Customizer**: Configure hotkeys, smart filter prefixes, splash screens, and visible categories.
- **🔌 Companion ScriptUI Panel**: Includes a dockable `Craft Console.jsx` panel for running inside workspace docks.

---

## 🛠 Building from Source

Craft Console is written in pure C# with WPF and requires **zero external SDKs or Visual Studio installations**. It builds with the native C# compiler included in every Windows installation.

### Option 1: Double-Click Build
Simply run:
```cmd
build.bat
```
The compiled executable will be placed in `bin\CraftConsole.exe`.

### Option 2: PowerShell / CLI
```powershell
& "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /out:"bin\CraftConsole.exe" /win32icon:"assets\icon.ico" /r:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" /r:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" /r:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" /r:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" /r:"System.dll" /r:"System.Drawing.dll" /r:"System.Windows.Forms.dll" /r:"System.Core.dll" "src\Launcher.cs"
```

---

## 🚀 Usage

1. Launch `CraftConsole.exe`.
2. Open **EffectCraft** (or After Effects).
3. Press `Ctrl + Space` to summon the console capsule.
4. Type an effect name (e.g. `Gaussian Blur`, `Curves`, `Glow`) and press **Enter** to apply!
5. Use `@sc` to view session snapshots and click to copy them back to your clipboard.

---

## 📁 Repository Structure

```text
craft-console/
├── src/
│   └── Launcher.cs                # Pure C# WPF console & tray application
├── scriptui/
│   └── Craft Console.jsx          # Dockable ExtendScript panel companion
├── assets/
│   └── icon.ico                   # Application icon
├── build.bat                      # Windows build script
├── .gitignore                     # Git ignore rules
├── LICENSE                        # MIT License
└── ATTRIBUTION.md                 # Asset attribution & clean-room statement
```

---

## 🛡 License & Clean-Room Standard

- Craft Console is licensed under the [MIT License](LICENSE).
- **Clean-room**: Contains zero proprietary Adobe icons, binaries, or artwork. All UI geometry and glyphs are procedurally drawn or openly licensed. See [ATTRIBUTION.md](ATTRIBUTION.md).
