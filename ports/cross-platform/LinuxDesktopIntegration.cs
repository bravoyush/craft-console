using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace CraftConsole.CrossPlatform
{
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

        public static void RegisterGlobalShortcuts(string shortcut = "Ctrl+Space")
        {
            if (!OperatingSystem.IsLinux()) return;
            try
            {
                string desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";

                // KDE Plasma (Plasma 6 / Plasma 5)
                if (desktop.Contains("KDE", StringComparison.OrdinalIgnoreCase))
                {
                    TryRunCommand("kwriteconfig6", $"--file kglobalshortcutsrc --group craft-console.desktop --key _k_friendly_name \"Craft Console\"");
                    TryRunCommand("kwriteconfig6", $"--file kglobalshortcutsrc --group craft-console.desktop --key _launch \"{shortcut},{shortcut},Craft Console\"");
                    TryRunCommand("kwriteconfig5", $"--file kglobalshortcutsrc --group craft-console.desktop --key _k_friendly_name \"Craft Console\"");
                    TryRunCommand("kwriteconfig5", $"--file kglobalshortcutsrc --group craft-console.desktop --key _launch \"{shortcut},{shortcut},Craft Console\"");
                    TryRunCommand("kbuildsycoca6", "");
                    TryRunCommand("qdbus6", "org.kde.KWin /KWin org.kde.KWin.reconfigure");
                    TryRunCommand("qdbus", "org.kde.KWin /KWin org.kde.KWin.reconfigure");
                }
            }
            catch { }
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
