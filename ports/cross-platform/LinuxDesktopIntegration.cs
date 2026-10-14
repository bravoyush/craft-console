using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CraftConsole.CrossPlatform
{
    public struct ScreenGeometry
    {
        public string Name;
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    public static class LinuxDesktopIntegration
    {
        public static string GetCurrentExecutablePath()
        {
            string? procPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(procPath) && File.Exists(procPath))
                return Path.GetFullPath(procPath);

            return Path.Combine(AppContext.BaseDirectory, "CraftConsole");
        }

        public static string? FindEffectCraftBinary(string? preferredPath = null)
        {
            if (!string.IsNullOrWhiteSpace(preferredPath) && File.Exists(preferredPath))
                return Path.GetFullPath(preferredPath);

            string appDir = AppContext.BaseDirectory;

            // 1. Same directory
            string localSame = Path.Combine(appDir, "effectcraft");
            if (File.Exists(localSame)) return localSame;

            // 2. Relative cargo target paths & known portable locations
            string[] relativeCandidates = new[]
            {
                Path.Combine(appDir, "..", "effectcraft"),
                Path.Combine(appDir, "..", "bin", "effectcraft"),
                Path.Combine(appDir, "..", "..", "target", "release", "effectcraft"),
                Path.Combine(appDir, "..", "..", "target", "debug", "effectcraft"),
                Path.Combine(appDir, "..", "..", "..", "target", "release", "effectcraft"),
                "/mnt/Backup&cache/ALOS/effectcraft-0.8.0-linux-x86_64/bin/effectcraft"
            };
            foreach (var candidate in relativeCandidates)
            {
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }

            // 3. User local bin
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string userLocal = Path.Combine(home, ".local", "bin", "effectcraft");
            if (File.Exists(userLocal)) return userLocal;

            // 4. System standard paths
            string[] sysCandidates = new[]
            {
                "/usr/local/bin/effectcraft",
                "/usr/bin/effectcraft",
                "/opt/effectcraft/effectcraft",
                "/opt/effectcraft/bin/effectcraft"
            };
            foreach (var candidate in sysCandidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            // 5. Look in PATH
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        string p = Path.Combine(dir.Trim(), "effectcraft");
                        if (File.Exists(p)) return p;
                    }
                    catch { }
                }
            }

            return null;
        }

        public static string InstallIcon()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string iconDir = Path.Combine(home, ".local", "share", "icons", "hicolor", "256x256", "apps");
            if (!Directory.Exists(iconDir)) Directory.CreateDirectory(iconDir);

            string targetIconPath = Path.Combine(iconDir, "craft-console.png");

            // Look for icon.png in assets directory next to binary
            string localIcon = Path.Combine(AppContext.BaseDirectory, "assets", "icon.png");
            if (!File.Exists(localIcon))
            {
                localIcon = Path.Combine(AppContext.BaseDirectory, "icon.png");
            }

            if (File.Exists(localIcon))
            {
                try
                {
                    File.Copy(localIcon, targetIconPath, true);
                    return targetIconPath;
                }
                catch { }
            }

            return targetIconPath;
        }

        public static bool CreateDesktopEntries(bool createOnDesktop = true, bool createUnified = true)
        {
            try
            {
                string exePath = GetCurrentExecutablePath();
                string iconPath = InstallIcon();
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string appsDir = Path.Combine(home, ".local", "share", "applications");
                if (!Directory.Exists(appsDir)) Directory.CreateDirectory(appsDir);

                // 1. Craft Console shortcut (standalone quick console)
                string consoleDesktopContent = $@"# EffectCraft Console Desktop Entry
[Desktop Entry]
Version=1.0
Type=Application
Name=Craft Console
Comment=Quick floating effects launcher and console for EffectCraft
Exec=""{exePath}"" --toggle
Icon={iconPath}
Terminal=false
Categories=Graphics;AudioVideo;Video;
Keywords=effectcraft;effects;motion;plugins;quick;console;
StartupNotify=true
X-KDE-Shortcuts=Ctrl+Space
";

                string consoleEntryPath = Path.Combine(appsDir, "craft-console.desktop");
                File.WriteAllText(consoleEntryPath, consoleDesktopContent, new UTF8Encoding(false));
                SetExecutablePermission(consoleEntryPath);

                // Register global hotkey in desktop environment (KDE Plasma 6/5)
                RegisterGlobalShortcuts("Ctrl+Space");

                // 2. Unified "EffectCraft (with Console)" shortcut
                if (createUnified)
                {
                    string unifiedDesktopContent = $@"# EffectCraft with Console Desktop Entry
[Desktop Entry]
Version=1.0
Type=Application
Name=EffectCraft (with Console)
Comment=Launch EffectCraft with integrated Craft Console & Splash Screen
Exec=""{exePath}""
Icon={iconPath}
Terminal=false
Categories=Graphics;AudioVideo;Video;
Keywords=effectcraft;aftereffects;motion;compositing;vfx;
StartupNotify=true
";
                    string unifiedEntryPath = Path.Combine(appsDir, "effectcraft-console.desktop");
                    File.WriteAllText(unifiedEntryPath, unifiedDesktopContent, new UTF8Encoding(false));
                    SetExecutablePermission(unifiedEntryPath);
                }

                // 3. Optional Desktop folder shortcut
                if (createOnDesktop)
                {
                    string desktopDir = Path.Combine(home, "Desktop");
                    if (Directory.Exists(desktopDir))
                    {
                        string deskConsole = Path.Combine(desktopDir, "craft-console.desktop");
                        File.WriteAllText(deskConsole, consoleDesktopContent, new UTF8Encoding(false));
                        SetExecutablePermission(deskConsole);

                        if (createUnified)
                        {
                            string deskUnified = Path.Combine(desktopDir, "effectcraft-console.desktop");
                            string unifiedDesktopContent = $@"# EffectCraft with Console Desktop Entry
[Desktop Entry]
Version=1.0
Type=Application
Name=EffectCraft (with Console)
Comment=Launch EffectCraft with integrated Craft Console & Splash Screen
Exec=""{exePath}""
Icon={iconPath}
Terminal=false
Categories=Graphics;AudioVideo;Video;
Keywords=effectcraft;aftereffects;motion;compositing;vfx;
StartupNotify=true
";
                            File.WriteAllText(deskUnified, unifiedDesktopContent, new UTF8Encoding(false));
                            SetExecutablePermission(deskUnified);
                        }
                    }
                }

                // Try refreshing desktop database
                TryRunCommand("update-desktop-database", appsDir);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating desktop entries: " + ex.Message);
                return false;
            }
        }

        public static (int removedShortcuts, bool configPurged) Uninstall(bool purgeConfig)
        {
            int count = 0;
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Targets to remove
            string[] desktopFiles = new[]
            {
                Path.Combine(home, ".local", "share", "applications", "craft-console.desktop"),
                Path.Combine(home, ".local", "share", "applications", "effectcraft-console.desktop"),
                Path.Combine(home, "Desktop", "craft-console.desktop"),
                Path.Combine(home, "Desktop", "effectcraft-console.desktop"),
                Path.Combine(home, ".local", "share", "icons", "hicolor", "256x256", "apps", "craft-console.png")
            };

            foreach (var f in desktopFiles)
            {
                if (File.Exists(f))
                {
                    try { File.Delete(f); count++; } catch { }
                }
            }

            bool cfgPurged = false;
            if (purgeConfig)
            {
                string configDir = Path.Combine(home, ".config", "effectcraft");
                if (Directory.Exists(configDir))
                {
                    try
                    {
                        Directory.Delete(configDir, true);
                        cfgPurged = true;
                    }
                    catch { }
                }
            }

            // Refresh desktop database and KDE system configuration
            string appsDir = Path.Combine(home, ".local", "share", "applications");
            TryRunCommand("update-desktop-database", appsDir);
            TryRunCommand("kwriteconfig6", "--file kglobalshortcutsrc --group craft-console.desktop --delete");
            TryRunCommand("kwriteconfig5", "--file kglobalshortcutsrc --group craft-console.desktop --delete");
            TryRunCommand("kbuildsycoca6", "");
            TryRunCommand("qdbus6", "org.kde.KWin /KWin org.kde.KWin.reconfigure");

            return (count, cfgPurged);
        }
        public static void CleanGlobalShortcuts()
        {
            if (!OperatingSystem.IsLinux()) return;
            try
            {
                // Remove any legacy global shortcut entries from KDE system configs
                TryRunCommand("kwriteconfig6", "--file kglobalshortcutsrc --group craft-console.desktop --delete");
                TryRunCommand("kwriteconfig5", "--file kglobalshortcutsrc --group craft-console.desktop --delete");
                TryRunCommand("kbuildsycoca6", "");
                TryRunCommand("qdbus6", "org.kde.KWin /KWin org.kde.KWin.reconfigure");
                TryRunCommand("qdbus", "org.kde.KWin /KWin org.kde.KWin.reconfigure");
            }
            catch { }
        }

        public static void RegisterGlobalShortcuts(string shortcut = "Ctrl+Space")
        {
            // Backward compatibility stub - routes to non-intrusive In-App shortcut configuration
            ConfigureInAppShortcut(shortcut);
        }

        public static void ConfigureInAppShortcut(string shortcut = "Ctrl+Space")
        {
            if (!OperatingSystem.IsLinux()) return;
            try
            {
                // 1. Clean any global desktop shortcut pollution so system hotkeys are never hijacked
                CleanGlobalShortcuts();

                // 2. Normalize shortcut format (e.g. "Ctrl + Space" -> "Ctrl+Space")
                string norm = (string.IsNullOrEmpty(shortcut) ? "Ctrl+Space" : shortcut)
                    .Replace(" ", "");

                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string configDir = Path.Combine(home, ".config", "effectcraft");
                if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);

                // 3. Write CraftConsole in-app trigger script
                string scriptPath = Path.Combine(configDir, "CraftConsole.jsx");
                string scriptContent = @"// Craft Console In-App Trigger
// Triggers Craft Console palette over IPC exclusively when EffectCraft is focused
(function() {
    try {
        var s = new Socket();
        if (s.open(""127.0.0.1:9879"", ""UTF-8"")) {
            s.writeln(""toggle"");
            s.close();
        }
    } catch(e) {}
})();
";
                File.WriteAllText(scriptPath, scriptContent, new UTF8Encoding(false));

                // 4. Ensure allowScriptsWriteFiles is enabled in prefs.json
                string prefsPath = Path.Combine(configDir, "prefs.json");
                if (File.Exists(prefsPath))
                {
                    try
                    {
                        string content = File.ReadAllText(prefsPath);
                        if (!content.Contains("\"allowScriptsWriteFiles\": true"))
                        {
                            content = content.Replace("\"allowScriptsWriteFiles\": false", "\"allowScriptsWriteFiles\": true");
                            File.WriteAllText(prefsPath, content, new UTF8Encoding(false));
                        }
                    }
                    catch { }
                }

                // 5. If EffectCraft is running, dynamically push shortcut to engine via TCP (port 9877)
                if (EngineClient.IsEngineListening(150))
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            string json = $"{{\"id\":100,\"method\":\"shortcuts.set\",\"params\":{{\"command\":\"file.runScript\",\"params\":{{\"path\":\"{scriptPath.Replace("\\", "/")}\"}},\"keys\":\"{norm}\"}}}}";
                            EngineClient.SendTcpCommand(json, 1500);
                        }
                        catch { }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error configuring in-app shortcut: " + ex.Message);
            }
        }

        public static bool IsEffectCraftForeground()
        {
            if (!OperatingSystem.IsLinux()) return true;

            try
            {
                // 1. If Console window is currently visible, allow toggling it
                if (ConsoleWindow.CurrentInstance != null && ConsoleWindow.CurrentInstance.IsVisible)
                {
                    return true;
                }

                // 2. Query active window under X11
                string activeWinId = RunCommandOutput("xprop", "-root _NET_ACTIVE_WINDOW")?.Trim() ?? "";
                if (!string.IsNullOrEmpty(activeWinId) && activeWinId.Contains("#"))
                {
                    string id = activeWinId.Split('#')[1].Trim();
                    string winClass = RunCommandOutput("xprop", $"-id {id} WM_CLASS")?.ToLowerInvariant() ?? "";
                    if (winClass.Contains("effectcraft") || winClass.Contains("craftconsole"))
                    {
                        return true;
                    }
                }

                // 3. Query active window under Hyprland
                string hypr = RunCommandOutput("hyprctl", "activewindow -j")?.ToLowerInvariant() ?? "";
                if (!string.IsNullOrEmpty(hypr) && (hypr.Contains("\"class\": \"effectcraft\"") || hypr.Contains("\"class\": \"craftconsole\"")))
                {
                    return true;
                }

                // 4. Check if EffectCraft process exists and engine is listening
                if (EngineClient.IsEngineListening(60))
                {
                    var procs = Process.GetProcessesByName("effectcraft");
                    if (procs.Length > 0)
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public static ScreenGeometry GetActiveScreenGeometry()
        {
            // Default fallback
            var def = new ScreenGeometry { Name = "Default", X = 0, Y = 0, Width = 1920, Height = 1080 };
            if (!OperatingSystem.IsLinux()) return def;

            try
            {
                // 1. Query active output name from KWin (e.g. DP-1, HDMI-A-1, eDP-1)
                string activeOutput = RunCommandOutput("qdbus6", "org.kde.KWin /KWin org.kde.KWin.activeOutputName")?.Trim() ?? "";
                if (string.IsNullOrEmpty(activeOutput))
                {
                    activeOutput = RunCommandOutput("qdbus", "org.kde.KWin /KWin org.kde.KWin.activeOutputName")?.Trim() ?? "";
                }

                // 2. Query kscreen-doctor -o for output geometries
                string kscreenOutput = RunCommandOutput("kscreen-doctor", "-o") ?? "";
                if (!string.IsNullOrEmpty(kscreenOutput))
                {
                    var lines = kscreenOutput.Split('\n');
                    string currentOutput = "";
                    foreach (var rawLine in lines)
                    {
                        string line = rawLine.Trim();
                        if (line.StartsWith("Output:"))
                        {
                            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 3) currentOutput = parts[2];
                        }
                        else if (line.StartsWith("Geometry:") && !string.IsNullOrEmpty(currentOutput))
                        {
                            // Example: Geometry: 3840,312 1366x768
                            if (string.IsNullOrEmpty(activeOutput) || string.Equals(currentOutput, activeOutput, StringComparison.OrdinalIgnoreCase))
                            {
                                string geomStr = line.Substring("Geometry:".Length).Trim();
                                var sp = geomStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (sp.Length == 2)
                                {
                                    var posParts = sp[0].Split(',');
                                    var sizeParts = sp[1].Split('x');
                                    if (posParts.Length == 2 && sizeParts.Length == 2 &&
                                        int.TryParse(posParts[0], out int gx) &&
                                        int.TryParse(posParts[1], out int gy) &&
                                        int.TryParse(sizeParts[0], out int gw) &&
                                        int.TryParse(sizeParts[1], out int gh))
                                    {
                                        return new ScreenGeometry { Name = currentOutput, X = gx, Y = gy, Width = gw, Height = gh };
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return def;
        }

        public static bool TryGetCursorPosition(out int x, out int y)
        {
            x = 0;
            y = 0;
            if (!OperatingSystem.IsLinux()) return false;

            try
            {
                // Run a fast KWin script snippet to query workspace.cursorPos
                string script = "var p = workspace.cursorPos;\nconsole.error('CRAFT_CURSOR:' + p.x + ',' + p.y);\n";
                string tmpFile = Path.Combine(Path.GetTempPath(), "craft_cur_" + Guid.NewGuid().ToString("N") + ".js");
                File.WriteAllText(tmpFile, script);

                try
                {
                    string scriptName = "craft_cur_query";
                    RunCommandOutput("qdbus6", $"org.kde.KWin /Scripting loadScript \"{tmpFile}\" \"{scriptName}\"");
                    RunCommandOutput("qdbus6", "org.kde.KWin /Scripting start");
                    RunCommandOutput("qdbus6", $"org.kde.KWin /Scripting unloadScript \"{scriptName}\"");

                    // Read journalctl last message for CRAFT_CURSOR
                    string logOut = RunCommandOutput("journalctl", "--user -b -n 10 -g CRAFT_CURSOR") ?? "";
                    if (logOut.Contains("CRAFT_CURSOR:"))
                    {
                        int idx = logOut.LastIndexOf("CRAFT_CURSOR:");
                        string sub = logOut.Substring(idx + "CRAFT_CURSOR:".Length).Trim();
                        int nl = sub.IndexOfAny(new[] { '\n', '\r', ' ' });
                        if (nl >= 0) sub = sub.Substring(0, nl);
                        var parts = sub.Split(',');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int cx) && int.TryParse(parts[1], out int cy))
                        {
                            x = cx;
                            y = cy;
                            return true;
                        }
                    }
                }
                finally
                {
                    if (File.Exists(tmpFile)) File.Delete(tmpFile);
                }
            }
            catch { }

            // Secondary check: Focused window geometry from KWin
            try
            {
                string winInfo = RunCommandOutput("qdbus6", "org.kde.KWin /KWin org.kde.KWin.queryWindowInfo") ?? "";
                if (!string.IsNullOrEmpty(winInfo))
                {
                    int wx = 0, wy = 0, ww = 0, wh = 0;
                    foreach (var line in winInfo.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("x:") && int.TryParse(trimmed.Substring(2).Trim(), out int valX)) wx = valX;
                        if (trimmed.StartsWith("y:") && int.TryParse(trimmed.Substring(2).Trim(), out int valY)) wy = valY;
                        if (trimmed.StartsWith("width:") && int.TryParse(trimmed.Substring(6).Trim(), out int valW)) ww = valW;
                        if (trimmed.StartsWith("height:") && int.TryParse(trimmed.Substring(7).Trim(), out int valH)) wh = valH;
                    }
                    if (ww > 0 && wh > 0)
                    {
                        x = wx + (ww / 2);
                        y = wy + 200;
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private static string? RunCommandOutput(string cmd, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(cmd, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null) return null;
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(1000);
                return output;
            }
            catch
            {
                return null;
            }
        }

        private static void SetExecutablePermission(string filePath)
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
            try
            {
                var psi = new ProcessStartInfo("chmod", $"+x \"{filePath}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(1000);
            }
            catch { }
        }

        private static void TryRunCommand(string cmd, string args)
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
            try
            {
                var psi = new ProcessStartInfo(cmd, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(2000);
            }
            catch { }
        }
    }
}
