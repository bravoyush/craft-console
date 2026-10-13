# Craft Console ⚡

> **A fast, keyboard-first HUD command palette and workflow companion for EffectCraft and Adobe After Effects.**

Craft Console brings lightning-fast effect searching, preset browsing, command palette execution, and clipboard screenshot management directly to your fingertips via a floating search bar positioned at your cursor.

---

## 🚀 How to Use Craft Console

### 1. Starting the Launcher
1. Download **`CraftConsole.exe`** from the [Latest Releases](https://github.com/bravoyush/craft-console/releases).
2. Run `CraftConsole.exe`.
3. The app starts and docks into your **Windows System Tray** with the EffectCraft icon.
4. When EffectCraft launches, a sleek splash screen appears featuring the **Neon Unicorn Glass** artwork.

### 2. Summoning the Console (`Ctrl + Space`)
- Press **`Ctrl + Space`** anytime while inside EffectCraft (or After Effects).
- The console capsule immediately spawns right at your mouse cursor.
- Type any effect name (e.g. `Gaussian Blur`, `Curves`, `Glow`) and press **Enter** to instantly apply it to your selected layer!

### 3. Smart Filtering
Type **`@`** or **`#`** in the search bar to filter by category:
- **`@fx `** — Filter effects only.
- **`@preset `** — Filter animation and text animator presets.
- **`@layer `** — Instant layer creation (`Solid`, `Null`, `Adjustment`, `Camera`, `Light`, `Shape`, `Text`).
- **`@cmd `** — Search and execute EffectCraft engine commands (Composition, Edit, File, View, Window, Help).
- **`@recent `** — Search and open recent `.ecproj` project files.
- **`@sc `** or **`@screenshot `** — View your current session capture gallery with live thumbnails, comp names, timecodes, and timestamps.

### 4. Header Action Buttons
- **📷 Camera**: Takes a snapshot of the current composition frame and copies it directly to your Windows clipboard (`Ctrl + V`).
- **🗂 Overlapping Screens**: Opens your `@sc` Session Capture Gallery (or presets if no captures exist).
- **📥 Download Arrow**: Exports the active frame as a timestamped PNG file straight to your Windows Desktop.
- **⚙ Gear Icon**: Opens the full **Craft Console & Studio Settings** customizer window.
- **⎋ Escape**: Dismisses the console instantly.

---

## ✨ Features

- **🎯 Instant Cursor HUD**: Summons directly under your mouse cursor with zero workflow interruption.
- **🎨 Automatic Theme Sync**: Dynamically matches EffectCraft's active appearance theme (**Dark**, **Darker**, and **Light** modes) as well as Windows system theme.
- **📸 In-Memory Session Captures**: Keeps a lightweight rolling history of current session captures, automatically purged on restart (zero disk bloat).
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

## 📁 Repository Structure

```text
craft-console/
├── src/
│   └── Launcher.cs                # Pure C# WPF console & tray application
├── scriptui/
│   └── Craft Console.jsx          # Dockable ExtendScript panel companion
├── assets/
│   ├── icon.ico                   # Application icon
│   └── splash.png                 # Default Neon Unicorn splash artwork
├── splash/
│   ├── art.png                    # Splash screen artwork
│   └── splash.ini                 # Default configuration file
├── build.bat                      # Windows build script
├── .gitignore                     # Git ignore rules
├── LICENSE                        # MIT License
└── ATTRIBUTION.md                 # Asset attribution & clean-room statement
```

---

## 🛡 License & Clean-Room Standard

- Craft Console is licensed under the [MIT License](LICENSE).
- **Clean-room**: Contains zero proprietary Adobe icons, binaries, or artwork. All UI geometry and glyphs are procedurally drawn or openly licensed. See [ATTRIBUTION.md](ATTRIBUTION.md).
