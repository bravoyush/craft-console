using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Avalonia.Media;

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

    public class DisplayRow
    {
        public bool IsHeader { get; set; }
        public EffectItem? Item { get; set; }
        public Avalonia.Controls.Control? Container { get; set; }
        public Avalonia.Controls.TextBlock? Label { get; set; }
        public Avalonia.Controls.TextBlock? ExtraLabel { get; set; }
    }

    public class SmartFilterDef
    {
        public string Prefix { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Category { get; set; }
        public ConsoleItemKind? Kind { get; set; }
        public int Count { get; set; }
        public string Description { get; set; } = "";
    }

    public class SplashConfig
    {
        public string Theme { get; set; } = "Dark";
        public string Title { get; set; } = "EffectCraft";
        public string Subtitle { get; set; } = "Motion & Visual Effects";
        public string BadgeText { get; set; } = "Ec";
        public string AccentColor { get; set; } = "#00D2FF";
        public string BadgeBackground { get; set; } = "#0F2338";
        public string ArtistCredit { get; set; } = "Cosmic Horizon Edition";
        public string ArtImage { get; set; } = "splash/art.png";
        public string Shortcut { get; set; } = "Ctrl + Space";
        public string FavoriteShortcut { get; set; } = "Ctrl + D";
        public double TitleFontSize { get; set; } = 32.0;
        public double SubtitleFontSize { get; set; } = 11.5;
        public double ArtistFontSize { get; set; } = 10.0;
        public double CornerRadius { get; set; } = 18.0;
        public bool ShowLoadingBar { get; set; } = true;
        public bool ShowLoadingText { get; set; } = true;
        public bool ShowFooter { get; set; } = true;
        public double SplashDuration { get; set; } = 3.5;
        public bool ShowSplashScreen { get; set; } = true;
        public string StartupTab { get; set; } = "Home";
        public bool ShowFavoritesOnOpen { get; set; } = true;
        public string PrefixFavorites { get; set; } = "@fav";
        public string PrefixEffects { get; set; } = "@fx";
        public string PrefixPresets { get; set; } = "@preset";
        public string PrefixLayers { get; set; } = "@layer";
        public string PrefixCommands { get; set; } = "@cmd";
        public string PrefixRecent { get; set; } = "@recent";
        public bool ConsoleShowEffects { get; set; } = true;
        public bool ConsoleShowPresets { get; set; } = true;
        public bool ConsoleShowLayers { get; set; } = true;
        public bool ConsoleShowCommands { get; set; } = true;
        public bool ConsoleShowRecent { get; set; } = true;
        public string BinaryPath { get; set; } = "";
        public string SplashStyle { get; set; } = "Classic"; // "Classic" (Windows Stable 1:1) or "Poster" (Neon Unicorn)

        public List<string> Favorites { get; set; } = new List<string> {
            "Fast Box Blur", "Curves", "Glow", "Drop Shadow", "Hue/Saturation", "Solid"
        };

        public bool InstantLaunch
        {
            get => !ShowSplashScreen;
            set => ShowSplashScreen = !value;
        }

        public Color GetAccentColor()
        {
            try
            {
                if (Color.TryParse(AccentColor, out var c)) return c;
            }
            catch { }
            return Color.FromRgb(0, 210, 255);
        }

        public Color GetBadgeBackground()
        {
            try
            {
                if (Color.TryParse(BadgeBackground, out var c)) return c;
            }
            catch { }
            return Color.FromRgb(15, 35, 56);
        }

        public bool IsFavorite(string name, string? id)
        {
            if (Favorites == null) return false;
            foreach (var f in Favorites)
            {
                if (string.Equals(f, name, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(id) && string.Equals(f, id, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }

        public bool ToggleFavorite(string name)
        {
            if (Favorites == null) Favorites = new List<string>();
            for (int i = 0; i < Favorites.Count; i++)
            {
                if (string.Equals(Favorites[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    Favorites.RemoveAt(i);
                    return false;
                }
            }
            Favorites.Add(name);
            return true;
        }

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
                        case "theme": cfg.Theme = val; break;
                        case "title": cfg.Title = val; break;
                        case "subtitle": cfg.Subtitle = val; break;
                        case "badgetext": cfg.BadgeText = val; break;
                        case "accentcolor": cfg.AccentColor = val; break;
                        case "badgebackground": cfg.BadgeBackground = val; break;
                        case "artistcredit": cfg.ArtistCredit = val; break;
                        case "artimage": cfg.ArtImage = val; break;
                        case "shortcut": cfg.Shortcut = val; break;
                        case "favoriteshortcut": cfg.FavoriteShortcut = val; break;
                        case "titlefontsize": if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double tfs)) cfg.TitleFontSize = tfs; break;
                        case "subtitlefontsize": if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double sfs)) cfg.SubtitleFontSize = sfs; break;
                        case "artistfontsize": if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double afs)) cfg.ArtistFontSize = afs; break;
                        case "cornerradius": if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double cr)) cfg.CornerRadius = cr > 0 ? cr : 18.0; break;
                        case "showloadingbar": if (bool.TryParse(val, out bool slb)) cfg.ShowLoadingBar = slb; break;
                        case "showloadingtext": if (bool.TryParse(val, out bool slt)) cfg.ShowLoadingText = slt; break;
                        case "showfooter": if (bool.TryParse(val, out bool sf)) cfg.ShowFooter = sf; break;
                        case "showsplashscreen":
                        case "showsplash": if (bool.TryParse(val, out bool ss)) cfg.ShowSplashScreen = ss; break;
                        case "instantlaunch": if (bool.TryParse(val, out bool il)) cfg.ShowSplashScreen = !il; break;
                        case "splashduration": if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double sd)) cfg.SplashDuration = sd; break;
                        case "startuptab": cfg.StartupTab = val; break;
                        case "showfavoritesonopen": if (bool.TryParse(val, out bool sfo)) cfg.ShowFavoritesOnOpen = sfo; break;
                        case "prefixfavorites": cfg.PrefixFavorites = val; break;
                        case "prefixeffects": cfg.PrefixEffects = val; break;
                        case "prefixpresets": cfg.PrefixPresets = val; break;
                        case "prefixlayers": cfg.PrefixLayers = val; break;
                        case "prefixcommands": cfg.PrefixCommands = val; break;
                        case "prefixrecent": cfg.PrefixRecent = val; break;
                        case "consoleshoweffects": if (bool.TryParse(val, out bool cse)) cfg.ConsoleShowEffects = cse; break;
                        case "consoleshowpresets": if (bool.TryParse(val, out bool csp)) cfg.ConsoleShowPresets = csp; break;
                        case "consoleshowlayers": if (bool.TryParse(val, out bool csl)) cfg.ConsoleShowLayers = csl; break;
                        case "consoleshowcommands": if (bool.TryParse(val, out bool csc)) cfg.ConsoleShowCommands = csc; break;
                        case "consoleshowrecent": if (bool.TryParse(val, out bool csr)) cfg.ConsoleShowRecent = csr; break;
                        case "binarypath": cfg.BinaryPath = val; break;
                        case "splashstyle": cfg.SplashStyle = val; break;
                        case "favorites":
                            cfg.Favorites.Clear();
                            if (!string.IsNullOrEmpty(val))
                            {
                                string[] items = val.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var itm in items)
                                {
                                    string t = itm.Trim();
                                    if (!string.IsNullOrEmpty(t) && !cfg.Favorites.Contains(t)) cfg.Favorites.Add(t);
                                }
                            }
                            break;
                    }
                }
            }
            catch { }
            return cfg;
        }

        public void Save(string? appDir = null)
        {
            try
            {
                string cfgDir = GetConfigDir();
                string path = Path.Combine(cfgDir, "splash.ini");
                var sb = new StringBuilder();
                sb.AppendLine("# EffectCraft Splash & Console Configuration");
                sb.AppendLine("theme=" + Theme);
                sb.AppendLine("title=" + Title);
                sb.AppendLine("subtitle=" + Subtitle);
                sb.AppendLine("badgetext=" + BadgeText);
                sb.AppendLine("accentcolor=" + AccentColor);
                sb.AppendLine("badgebackground=" + BadgeBackground);
                sb.AppendLine("artistcredit=" + ArtistCredit);
                sb.AppendLine("artimage=" + ArtImage);
                sb.AppendLine("shortcut=" + Shortcut);
                sb.AppendLine("favoriteshortcut=" + FavoriteShortcut);
                sb.AppendLine("titlefontsize=" + TitleFontSize.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("subtitlefontsize=" + SubtitleFontSize.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("artistfontsize=" + ArtistFontSize.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("cornerradius=" + CornerRadius.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("showloadingbar=" + (ShowLoadingBar ? "true" : "false"));
                sb.AppendLine("showloadingtext=" + (ShowLoadingText ? "true" : "false"));
                sb.AppendLine("showfooter=" + (ShowFooter ? "true" : "false"));
                sb.AppendLine("showsplashscreen=" + (ShowSplashScreen ? "true" : "false"));
                sb.AppendLine("splashduration=" + SplashDuration.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("startuptab=" + StartupTab);
                sb.AppendLine("showfavoritesonopen=" + (ShowFavoritesOnOpen ? "true" : "false"));
                sb.AppendLine("prefixfavorites=" + PrefixFavorites);
                sb.AppendLine("prefixeffects=" + PrefixEffects);
                sb.AppendLine("prefixpresets=" + PrefixPresets);
                sb.AppendLine("prefixlayers=" + PrefixLayers);
                sb.AppendLine("prefixcommands=" + PrefixCommands);
                sb.AppendLine("prefixrecent=" + PrefixRecent);
                sb.AppendLine("consoleshoweffects=" + (ConsoleShowEffects ? "true" : "false"));
                sb.AppendLine("consoleshowpresets=" + (ConsoleShowPresets ? "true" : "false"));
                sb.AppendLine("consoleshowlayers=" + (ConsoleShowLayers ? "true" : "false"));
                sb.AppendLine("consoleshowcommands=" + (ConsoleShowCommands ? "true" : "false"));
                sb.AppendLine("consoleshowrecent=" + (ConsoleShowRecent ? "true" : "false"));
                sb.AppendLine("splashstyle=" + SplashStyle);
                if (Favorites != null && Favorites.Count > 0)
                {
                    sb.AppendLine("favorites=" + string.Join(",", Favorites));
                }
                if (!string.IsNullOrEmpty(BinaryPath))
                {
                    sb.AppendLine("binarypath=" + BinaryPath);
                }
                File.WriteAllText(path, sb.ToString());

                if (!string.IsNullOrEmpty(appDir) && Directory.Exists(appDir))
                {
                    try
                    {
                        string localPath = Path.Combine(appDir, "splash.ini");
                        File.WriteAllText(localPath, sb.ToString());
                    }
                    catch { }
                }
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

        public static bool IsFavorite(string name, string? id)
        {
            EnsureLoaded();
            return _favs.Contains(name) || (!string.IsNullOrEmpty(id) && _favs.Contains(id));
        }

        public static void ToggleFavorite(string name, string? id)
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
