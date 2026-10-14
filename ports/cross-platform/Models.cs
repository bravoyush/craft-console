using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CraftConsole.CrossPlatform
{
    public enum ConsoleItemKind
    {
        Effect,
        Preset,
        Layer,
        Command,
        Project,
        Filter,
        Capture
    }

    public class EffectItem
    {
        public string Name { get; set; } = "";
        public string Id { get; set; } = "";
        public string Category { get; set; } = "";
        public bool IsPreset { get; set; }
        public ConsoleItemKind Kind { get; set; } = ConsoleItemKind.Effect;
        public string? Extra { get; set; }
        public string? Keywords { get; set; }
        public string LowerName { get; set; } = "";
        public string LowerCategory { get; set; } = "";
        public string LowerKeywords { get; set; } = "";
        public bool IsFavorite { get; set; }
    }

    public class SplashConfig
    {
        public string Title { get; set; } = "EffectCraft";
        public string Subtitle { get; set; } = "Motion Graphics & Compositing";
        public string AccentColor { get; set; } = "#00D2FF";
        public string BadgeBackground { get; set; } = "#0F2338";
        public string Theme { get; set; } = "Dark";
        public double CornerRadius { get; set; } = 12.0;
        public string StartupTab { get; set; } = "Home";
        public bool InstantLaunch { get; set; } = false;
        public bool SoundChime { get; set; } = true;
        public string Shortcut { get; set; } = "Ctrl + Space";
        public string FavShortcut { get; set; } = "Ctrl + F";
        public string BinaryPath { get; set; } = "";
        public string SplashStyle { get; set; } = "Classic"; // "Classic" (Windows Studio Split) or "Poster" (Neon Unicorn)
        public bool ShowEffects { get; set; } = true;
        public bool ShowPresets { get; set; } = true;
        public bool ShowLayers { get; set; } = true;
        public bool ShowCommands { get; set; } = true;
        public bool ShowRecent { get; set; } = true;

        public static string GetConfigDir()
        {
            if (OperatingSystem.IsWindows())
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "EffectCraft");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
            else
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string dir = Path.Combine(home, ".config", "effectcraft");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static SplashConfig Load(string? appDir = null)
        {
            var cfg = new SplashConfig();
            string cfgDir = GetConfigDir();
            string path = Path.Combine(cfgDir, "splash.ini");
            if (!File.Exists(path) && !string.IsNullOrEmpty(appDir))
            {
                string localPath = Path.Combine(appDir, "splash.ini");
                if (File.Exists(localPath)) path = localPath;
            }

            if (!File.Exists(path)) return cfg;

            try
            {
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";")) continue;
                    int eq = trimmed.IndexOf('=');
                    if (eq < 0) continue;
                    string key = trimmed.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = trimmed.Substring(eq + 1).Trim();

                    switch (key)
                    {
                        case "title": cfg.Title = val; break;
                        case "subtitle": cfg.Subtitle = val; break;
                        case "accentcolor": cfg.AccentColor = val; break;
                        case "badgebackground": cfg.BadgeBackground = val; break;
                        case "theme": cfg.Theme = val; break;
                        case "cornerradius": double.TryParse(val, out double cr); cfg.CornerRadius = cr > 0 ? cr : 12.0; break;
                        case "startuptab": cfg.StartupTab = val; break;
                        case "instantlaunch": cfg.InstantLaunch = val.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                        case "soundchime": cfg.SoundChime = !val.Equals("false", StringComparison.OrdinalIgnoreCase); break;
                        case "shortcut": cfg.Shortcut = val; break;
                        case "favshortcut": cfg.FavShortcut = val; break;
                        case "binarypath": cfg.BinaryPath = val; break;
                        case "splashstyle": cfg.SplashStyle = val; break;
                    }
                }
            }
            catch { }
            return cfg;
        }

        public void Save()
        {
            try
            {
                string cfgDir = GetConfigDir();
                string path = Path.Combine(cfgDir, "splash.ini");
                var sb = new StringBuilder();
                sb.AppendLine("# EffectCraft Splash & Console Configuration");
                sb.AppendLine("title=" + Title);
                sb.AppendLine("subtitle=" + Subtitle);
                sb.AppendLine("accentcolor=" + AccentColor);
                sb.AppendLine("badgebackground=" + BadgeBackground);
                sb.AppendLine("theme=" + Theme);
                sb.AppendLine("cornerradius=" + CornerRadius);
                sb.AppendLine("startuptab=" + StartupTab);
                sb.AppendLine("instantlaunch=" + (InstantLaunch ? "true" : "false"));
                sb.AppendLine("soundchime=" + (SoundChime ? "true" : "false"));
                sb.AppendLine("shortcut=" + Shortcut);
                sb.AppendLine("favshortcut=" + FavShortcut);
                sb.AppendLine("splashstyle=" + SplashStyle);
                if (!string.IsNullOrEmpty(BinaryPath))
                {
                    sb.AppendLine("binarypath=" + BinaryPath);
                }
                File.WriteAllText(path, sb.ToString());
            }
            catch { }
        }
    }

    public static class FavoritesStorage
    {
        private static HashSet<string> _favs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded = false;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                string path = Path.Combine(SplashConfig.GetConfigDir(), "favorites.txt");
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    foreach (var l in lines)
                    {
                        string s = l.Trim();
                        if (!string.IsNullOrEmpty(s)) _favs.Add(s);
                    }
                }
            }
            catch { }
        }

        public static bool IsFavorite(string name, string id)
        {
            EnsureLoaded();
            return _favs.Contains(name) || (!string.IsNullOrEmpty(id) && _favs.Contains(id));
        }

        public static void ToggleFavorite(string name, string id)
        {
            EnsureLoaded();
            string key = !string.IsNullOrEmpty(id) ? id : name;
            if (_favs.Contains(key) || _favs.Contains(name))
            {
                _favs.Remove(key);
                _favs.Remove(name);
            }
            else
            {
                _favs.Add(key);
            }
            Save();
        }

        private static void Save()
        {
            try
            {
                string path = Path.Combine(SplashConfig.GetConfigDir(), "favorites.txt");
                File.WriteAllLines(path, _favs);
            }
            catch { }
        }
    }
}
