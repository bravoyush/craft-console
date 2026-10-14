# Craft Console — Linux & Cross-Platform Edition 🐧

This directory contains the cross-platform version of **Craft Console** built with **Avalonia UI (.NET 8 C#)** for Linux and macOS. It is completely isolated from the stable Windows codebase.

---

## 🚀 Key Features

1. **Spotlight / Raycast-Style Floating Capsule**:
   - Spawns centered on your active screen without cursor tracking quirks on Wayland / XWayland.
   - Smooth dark aesthetic (`#141418`) with glowing cyan accents (`#00D2FF`).
   - Categorized filtering pills: **All**, **Effects**, **Presets**, **Layers**, **Commands**, **Favorites**.
2. **Neon Unicorn Splash Screen**:
   - Embedded high-resolution artwork (`assets/splash.png`).
   - Animated progress bar and engine connection status.
   - Auto-launches `effectcraft` and monitors its process (closes Console when EffectCraft exits).
3. **User-Space Desktop Integration (No Root / Sudo Required)**:
   - Creates `~/.local/share/applications/craft-console.desktop` and `effectcraft-console.desktop`.
   - Optionally places launcher icons directly on `~/Desktop`.
   - Generates and installs high-res 256x256 icons to `~/.local/share/icons/hicolor/`.
4. **Clean Interactive Uninstaller**:
   - Available via CLI (`./CraftConsole --uninstall` or `./uninstall.sh`) or via Settings UI.
   - Prompts whether to keep or remove configuration and favorites (`~/.config/effectcraft/`).
5. **Global IPC Toggle**:
   - Run `./CraftConsole --toggle` anytime to toggle visibility.
   - Seamlessly assignable to any desktop environment hotkey (GNOME, KDE, i3/sway, XFCE).

---

## 📦 How to Run on Linux

### Quick Start (Standalone / Portable):
```bash
# Make executable and launch
chmod +x CraftConsole
./CraftConsole
```

### Install Desktop & Start Menu Shortcuts:
```bash
./install-desktop.sh
# Or directly:
./CraftConsole --setup
```

### Uninstall:
```bash
# Keep configuration & favorites:
./uninstall.sh

# Complete wipe (including ~/.config/effectcraft/):
./CraftConsole --uninstall --purge
```

---

## 🛠️ Building from Source

Requirements: .NET 8.0 SDK

```bash
# Build Debug DLL
dotnet build CraftConsole.CrossPlatform.csproj

# Publish Linux Self-Contained Native Binary (no .NET runtime required on target machine)
dotnet publish CraftConsole.CrossPlatform.csproj -r linux-x64 --self-contained -c Release -p:PublishSingleFile=true -o dist/linux-x64
```
