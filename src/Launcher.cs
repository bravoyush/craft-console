using System.Reflection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.Forms.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using Path = System.IO.Path;
using TextBox = System.Windows.Controls.TextBox;
using WpfPath = System.Windows.Shapes.Path;

namespace EffectCraftLauncher
{
    static class Program
    {
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9001;
        private const uint MOD_CONTROL  = 0x0002;
        private const uint MOD_ALT      = 0x0001;
        private const uint MOD_SHIFT    = 0x0004;
        private const uint MOD_NOREPEAT = 0x4000;
        public const int VK_SPACE = 0x20;
        public const int CONTROL_PORT = 9877;
        public const string ARTCRAFT_MARK_PATH = "M 104.28,49.49 L 81.55,0 H 50.32 L 47.15,4.76 L 14.75,53.63 L 0,75.85 L 21.55,97.4 L 85.34,60.46 L 102.33,97.5 L 116.34,75.76 L 104.28,49.49 Z M 32.89,65.66 L 65.31,16.79 L 76.22,40.56 L 32.89,65.66 Z";

        private static IntPtr _kbHookId = IntPtr.Zero;
        private static LowLevelKeyboardProc _kbProc;
        public static Process _childProc;
        private static long _lastTriggerTick = 0;
        private static bool _ctrlDown = false;
        private static NotifyIcon _tray;
        public static FXConsoleWindow _consoleWindow;
        public static SplashWindow _splashWindow;
        public static SettingsWindow _settingsWindow;
        public static Application _wpfApp;
        public static string AppDir;
        public static SplashConfig CurrentConfig = null;
        public static string EffectCraftExePath = null;

        public static string GetConfigIniPath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EffectCraft");
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return Path.Combine(dir, "craft_console.ini");
        }

        public static Dictionary<string, string> LoadGlobalSettings()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string iniPath = GetConfigIniPath();
                if (File.Exists(iniPath))
                {
                    string[] lines = File.ReadAllLines(iniPath);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("#") || trimmed.StartsWith(";") || trimmed.StartsWith("[") || !trimmed.Contains("=")) continue;
                        string[] parts = trimmed.Split(new char[] { '=' }, 2);
                        map[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }
            catch { }
            return map;
        }

        public static string GetGlobalSetting(string key, string defaultValue)
        {
            var map = LoadGlobalSettings();
            string val;
            if (map.TryGetValue(key, out val))
            {
                return val;
            }
            return defaultValue;
        }

        public static void SaveGlobalSetting(string key, string value)
        {
            try
            {
                string iniPath = GetConfigIniPath();
                var map = LoadGlobalSettings();
                map[key.Trim().ToLowerInvariant()] = value;
                var sb = new StringBuilder();
                sb.AppendLine("[CraftConsole]");
                foreach (var kvp in map)
                {
                    sb.AppendLine(string.Format("{0} = {1}", kvp.Key, kvp.Value));
                }
                File.WriteAllText(iniPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static string DetectEffectCraftExecutable(string baseDir)
        {
            // 1. Local directory bundle
            string localExe = Path.Combine(baseDir, "effectcraft.exe");
            if (File.Exists(localExe)) return localExe;

            // 2. Saved preference in %APPDATA%
            string saved = GetGlobalSetting("effectcraftexe", null);
            if (!string.IsNullOrEmpty(saved) && File.Exists(saved)) return saved;

            // 3. Active running process
            try
            {
                Process[] procs = Process.GetProcessesByName("effectcraft");
                foreach (var p in procs)
                {
                    try
                    {
                        string fn = p.MainModule.FileName;
                        if (!string.IsNullOrEmpty(fn) && File.Exists(fn)) return fn;
                    }
                    catch { }
                }
            }
            catch { }

            // 4. Subdirectories in base directory
            try
            {
                if (Directory.Exists(baseDir))
                {
                    string[] subDirs = Directory.GetDirectories(baseDir, "*effectcraft*", SearchOption.TopDirectoryOnly);
                    foreach (string sd in subDirs)
                    {
                        string cand = Path.Combine(sd, "effectcraft.exe");
                        if (File.Exists(cand)) return cand;
                    }
                }
            }
            catch { }

            // 5. Parent directory subdirectories (e.g. running from G:\local-ai\ or sibling folders)
            try
            {
                string parentDir = Path.GetFullPath(Path.Combine(baseDir, ".."));
                if (Directory.Exists(parentDir))
                {
                    string[] subDirs = Directory.GetDirectories(parentDir, "*effectcraft*", SearchOption.TopDirectoryOnly);
                    foreach (string sd in subDirs)
                    {
                        string cand = Path.Combine(sd, "effectcraft.exe");
                        if (File.Exists(cand)) return cand;
                    }
                }
            }
            catch { }

            // 6. Grandparent subdirectories (e.g. running from craft-console\bin\)
            try
            {
                string grandDir = Path.GetFullPath(Path.Combine(baseDir, "..", ".."));
                if (Directory.Exists(grandDir))
                {
                    string[] subDirs = Directory.GetDirectories(grandDir, "*effectcraft*", SearchOption.TopDirectoryOnly);
                    foreach (string sd in subDirs)
                    {
                        string cand = Path.Combine(sd, "effectcraft.exe");
                        if (File.Exists(cand)) return cand;
                    }
                }
            }
            catch { }

            // 7. Standard install locations
            string[] standardPaths = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "EffectCraft", "effectcraft.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EffectCraft", "effectcraft.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "EffectCraft", "effectcraft.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "effectcraft", "effectcraft.exe")
            };
            foreach (string sp in standardPaths)
            {
                if (File.Exists(sp)) return sp;
            }

            return null;
        }

        public static Stream GetEmbeddedSplashStream()
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetManifestResourceStream("SplashArt");
            }
            catch
            {
                return null;
            }
        }

        public static void EnsureSplashFiles(string targetDir)
        {
            try
            {
                if (string.IsNullOrEmpty(targetDir)) return;
                string splashDir = Path.Combine(targetDir, "splash");
                if (!Directory.Exists(splashDir))
                {
                    Directory.CreateDirectory(splashDir);
                }

                string artFile = Path.Combine(splashDir, "art.png");
                if (!File.Exists(artFile))
                {
                    using (Stream res = GetEmbeddedSplashStream())
                    {
                        if (res != null)
                        {
                            using (FileStream fs = new FileStream(artFile, FileMode.Create, FileAccess.Write))
                            {
                                byte[] buffer = new byte[81920];
                                int read;
                                while ((read = res.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    fs.Write(buffer, 0, read);
                                }
                            }
                            Log("Extracted embedded SplashArt to: " + artFile);
                        }
                    }
                }

                string iniFile = Path.Combine(splashDir, "splash.ini");
                if (!File.Exists(iniFile))
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("[Splash]");
                    sb.AppendLine("Theme = Dark");
                    sb.AppendLine("Title = EffectCraft");
                    sb.AppendLine("Subtitle = Motion & Visual Effects");
                    sb.AppendLine("BadgeText = Ec");
                    sb.AppendLine("AccentColor = #4E7BFB");
                    sb.AppendLine("BadgeBackground = #0F2338");
                    sb.AppendLine("ArtistCredit = Neon Unicorn Edition");
                    sb.AppendLine("ArtImage = splash\\art.png");
                    sb.AppendLine("Shortcut = Ctrl + Space");
                    sb.AppendLine("TitleFontSize = 32");
                    sb.AppendLine("SubtitleFontSize = 11.5");
                    sb.AppendLine("ArtistFontSize = 10");
                    sb.AppendLine("CornerRadius = 18");
                    sb.AppendLine("ShowLoadingBar = True");
                    sb.AppendLine("ShowLoadingText = True");
                    sb.AppendLine("ShowFooter = True");
                    sb.AppendLine("SplashDuration = 2.5");
                    sb.AppendLine("ConsoleShowEffects = True");
                    sb.AppendLine("ConsoleShowPresets = True");
                    sb.AppendLine("ConsoleShowLayers = True");
                    sb.AppendLine("ConsoleShowCommands = True");
                    sb.AppendLine("ConsoleShowRecent = True");
                    sb.AppendLine("PrefixEffects = @fx");
                    sb.AppendLine("PrefixPresets = @preset");
                    sb.AppendLine("PrefixLayers = @layer");
                    sb.AppendLine("PrefixCommands = @cmd");
                    sb.AppendLine("PrefixRecent = @recent");
                    File.WriteAllText(iniFile, sb.ToString(), Encoding.UTF8);
                    Log("Created default splash.ini at: " + iniFile);
                }
            }
            catch (Exception ex)
            {
                Log("EnsureSplashFiles error: " + ex.Message);
            }
        }

        public static void CreateShortcut(string shortcutPath, string targetExePath, string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut",
                    BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath",
                    BindingFlags.SetProperty, null, shortcut, new object[] { targetExePath });
                shortcutType.InvokeMember("WorkingDirectory",
                    BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetExePath) });
                shortcutType.InvokeMember("Description",
                    BindingFlags.SetProperty, null, shortcut, new object[] { description });
                shortcutType.InvokeMember("IconLocation",
                    BindingFlags.SetProperty, null, shortcut, new object[] { targetExePath + ",0" });
                shortcutType.InvokeMember("Save",
                    BindingFlags.InvokeMethod, null, shortcut, null);
                Log("Created shortcut: " + shortcutPath + " -> " + targetExePath);
            }
            catch (Exception ex)
            {
                Log("CreateShortcut error: " + ex.Message);
            }
        }

        public static int HotkeyVk = VK_SPACE;
        public static bool HotkeyNeedCtrl = true;
        public static bool HotkeyNeedAlt = false;
        public static bool HotkeyNeedShift = false;
        private static HotkeyWindow _hotkeyWin = null;

        public static void Log(string msg)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher.log");
                File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine);
            }
            catch { }
        }

        public static void ApplyShortcut(string shortcutStr)
        {
            if (string.IsNullOrEmpty(shortcutStr)) shortcutStr = "Ctrl + Space";
            bool ctrl = false, alt = false, shift = false;
            int vk = VK_SPACE;

            string[] parts = shortcutStr.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in parts)
            {
                string p = raw.Trim();
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || p.Equals("Control", StringComparison.OrdinalIgnoreCase)) ctrl = true;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) alt = true;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) shift = true;
                else
                {
                    try
                    {
                        Key k;
                        if (Enum.TryParse<Key>(p, true, out k))
                        {
                            vk = KeyInterop.VirtualKeyFromKey(k);
                        }
                        else
                        {
                            vk = VK_SPACE;
                        }
                    }
                    catch
                    {
                        vk = VK_SPACE;
                    }
                }
            }

            HotkeyNeedCtrl = ctrl;
            HotkeyNeedAlt = alt;
            HotkeyNeedShift = shift;
            HotkeyVk = vk;

            if (_tray != null)
            {
                _tray.Text = "EffectCraft (" + shortcutStr + " = EC Console)";
            }

            Log("Shortcut applied: " + shortcutStr + " (VK=" + vk + ", Ctrl=" + ctrl + ", Alt=" + alt + ", Shift=" + shift + ")");

            if (_hotkeyWin != null)
            {
                _hotkeyWin.ReRegister();
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher.log");
                File.WriteAllText(logPath, "");
            }
            catch { }

            Log("Craft Console starting with Splash Screen & Initial Setup support");

            AppDir = AppDomain.CurrentDomain.BaseDirectory;
            EnsureSplashFiles(AppDir);

            try
            {
                Environment.SetEnvironmentVariable("EFFECTCRAFT_CONTROL_PORT", CONTROL_PORT.ToString(), EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("EFFECTCRAFT_CONTROL_PORT", CONTROL_PORT.ToString(), EnvironmentVariableTarget.User);
            }
            catch { }

            try
            {
                SetCurrentProcessExplicitAppUserModelID("ArtCraft.EffectCraft");
            }
            catch { }

            bool forceSetup = false;
            foreach (string a in args)
            {
                if (a.Equals("--setup", StringComparison.OrdinalIgnoreCase) || a.Equals("/setup", StringComparison.OrdinalIgnoreCase))
                {
                    forceSetup = true;
                    break;
                }
            }
            if ((GetAsyncKeyState(0x10) & 0x8000) != 0) // Shift key
            {
                forceSetup = true;
            }

            string detectedExe = DetectEffectCraftExecutable(AppDir);
            bool isLocalBundle = File.Exists(Path.Combine(AppDir, "effectcraft.exe"));
            string savedExe = GetGlobalSetting("effectcraftexe", null);

            bool needsSetup = forceSetup || (!isLocalBundle && (string.IsNullOrEmpty(savedExe) || !File.Exists(savedExe))) || (detectedExe == null || !File.Exists(detectedExe));

            _wpfApp = new Application();
            _wpfApp.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs ex) {
                Log("UnhandledException: " + ex.ExceptionObject);
            };
            _wpfApp.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs ex) {
                Log("DispatcherUnhandledException: " + ex.Exception);
                ex.Handled = true;
            };

            if (needsSetup || detectedExe == null || !File.Exists(detectedExe))
            {
                var setupWin = new FirstLaunchSetupWindow(detectedExe);
                bool? result = setupWin.ShowDialog();
                if (result != true || string.IsNullOrEmpty(EffectCraftExePath) || !File.Exists(EffectCraftExePath))
                {
                    Log("User cancelled setup or no valid effectcraft.exe configured.");
                    return;
                }
            }
            else
            {
                EffectCraftExePath = !string.IsNullOrEmpty(savedExe) && File.Exists(savedExe) ? savedExe : detectedExe;
            }

            string effectcraftExe = EffectCraftExePath;
            string effectcraftDir = Path.GetDirectoryName(effectcraftExe);
            EnsureSplashFiles(effectcraftDir);

            CurrentConfig = SplashConfig.Load(effectcraftDir);
            SplashConfig splashCfg = CurrentConfig;
            ApplyShortcut(splashCfg.Shortcut);

            // Setup system tray menu with authentic EffectCraft icon
            _tray = new NotifyIcon();
            try
            {
                if (File.Exists(effectcraftExe))
                {
                    _tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(effectcraftExe);
                }
            }
            catch { }
            if (_tray.Icon == null)
            {
                try
                {
                    string localIco = Path.Combine(AppDir, "effectcraft.ico");
                    if (File.Exists(localIco)) _tray.Icon = new System.Drawing.Icon(localIco);
                }
                catch { }
            }
            if (_tray.Icon == null)
            {
                _tray.Icon = System.Drawing.SystemIcons.Application;
            }

            _tray.Text = "EffectCraft (" + splashCfg.Shortcut + " = EC Console)";
            _tray.Visible = true;
            _tray.DoubleClick += delegate(object s, EventArgs a) {
                if (_wpfApp != null)
                {
                    _wpfApp.Dispatcher.Invoke(new Action(delegate { OpenSettingsUI(); }));
                }
            };
            _tray.ContextMenu = new System.Windows.Forms.ContextMenu(new System.Windows.Forms.MenuItem[]
            {
                new System.Windows.Forms.MenuItem("Open EC Console", delegate(object s, EventArgs a) {
                    if (_wpfApp != null)
                    {
                        _wpfApp.Dispatcher.Invoke(new Action(delegate { ToggleConsoleUI(); }));
                    }
                }),
                new System.Windows.Forms.MenuItem("Settings & Customizer...", delegate(object s, EventArgs a) {
                    if (_wpfApp != null)
                    {
                        _wpfApp.Dispatcher.Invoke(new Action(delegate { OpenSettingsUI(); }));
                    }
                }),
                new System.Windows.Forms.MenuItem("Setup & EffectCraft Link...", delegate(object s, EventArgs a) {
                    if (_wpfApp != null)
                    {
                        _wpfApp.Dispatcher.Invoke(new Action(delegate {
                            var setup = new FirstLaunchSetupWindow(EffectCraftExePath);
                            setup.ShowDialog();
                        }));
                    }
                }),
                new System.Windows.Forms.MenuItem("-"),
                new System.Windows.Forms.MenuItem("Exit", delegate(object s, EventArgs a) {
                    if (_wpfApp != null)
                    {
                        _wpfApp.Dispatcher.Invoke(new Action(delegate { _wpfApp.Shutdown(); }));
                    }
                })
            });

            // Install low-level keyboard hook (only for trigger shortcut)
            _kbProc = HookCallback;
            IntPtr hMod = IntPtr.Zero;
            using (var cp = Process.GetCurrentProcess())
            using (var cm = cp.MainModule)
                hMod = GetModuleHandle(cm.ModuleName);

            _kbHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _kbProc, hMod, 0);
            Log("Keyboard Hook -> " + (_kbHookId != IntPtr.Zero));

            // Secondary hotkey via hidden NativeWindow
            try
            {
                _hotkeyWin = new HotkeyWindow();
            }
            catch (Exception ex)
            {
                Log("HotkeyWindow create error: " + ex.Message);
            }

            _wpfApp.Startup += delegate(object s, StartupEventArgs e) {
                try
                {
                    _consoleWindow = new FXConsoleWindow();
                    Log("FXConsoleWindow instantiated");

                    _splashWindow = new SplashWindow(splashCfg);
                    _splashWindow.Show();
                    Log("SplashWindow shown");

                    StartEffectCraftAndMonitor(effectcraftExe, AppDir, args, splashCfg);
                }
                catch (Exception ex)
                {
                    Log("Startup initialization error: " + ex);
                }
            };

            _wpfApp.Exit += delegate(object s, ExitEventArgs e) {
                if (_hotkeyWin != null) { _hotkeyWin.DestroyHandle(); _hotkeyWin = null; }
                if (_kbHookId != IntPtr.Zero) { UnhookWindowsHookEx(_kbHookId); _kbHookId = IntPtr.Zero; }
                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            };

            Log("Entering WPF Application.Run");
            _wpfApp.Run();
        }

        private static void StartEffectCraftAndMonitor(string effectcraftExe, string appDir, string[] args, SplashConfig splashCfg)
        {
            ThreadPool.QueueUserWorkItem(delegate {
                Process validProc = null;
                Process[] existing = Process.GetProcessesByName("effectcraft");
                foreach (var p in existing)
                {
                    try
                    {
                        p.Refresh();
                        if (p.MainWindowHandle != IntPtr.Zero)
                        {
                            validProc = p;
                            break;
                        }
                    }
                    catch { }
                }

                if (validProc != null)
                {
                    _childProc = validProc;
                    Log("EffectCraft already running with active window (PID=" + _childProc.Id + ")");
                    SetForegroundWindow(_childProc.MainWindowHandle);
                }
                else
                {
                    foreach (var p in existing)
                    {
                        try { p.Kill(); Log("Killed orphaned ghost effectcraft PID=" + p.Id); } catch { }
                    }

                    try
                    {
                        string lockPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EffectCraft", "session.lock");
                        if (File.Exists(lockPath)) { File.Delete(lockPath); Log("Removed stale session.lock"); }
                    }
                    catch { }

                    var psi = new ProcessStartInfo
                    {
                        FileName = effectcraftExe,
                        Arguments = "--control " + CONTROL_PORT + (args.Length > 0 ? " " + string.Join(" ", args) : ""),
                        WorkingDirectory = Path.GetDirectoryName(effectcraftExe),
                        UseShellExecute = true
                    };

                    try
                    {
                        _childProc = Process.Start(psi);
                        Log("Launched fresh effectcraft (PID=" + (_childProc != null ? _childProc.Id.ToString() : "unknown") + ")");
                    }
                    catch (Exception ex)
                    {
                        Log("Failed to launch EffectCraft: " + ex.Message);
                    }
                }

                if (_childProc != null)
                {
                    try
                    {
                        _childProc.EnableRaisingEvents = true;
                        _childProc.Exited += delegate(object s, EventArgs e) {
                            if (_wpfApp != null)
                            {
                                _wpfApp.Dispatcher.Invoke(new Action(delegate { _wpfApp.Shutdown(); }));
                            }
                        };
                    }
                    catch { }
                }

                int minWaitMs = (int)(Math.Max(1.0, splashCfg.SplashDuration) * 1000.0);
                long startTick = (long)Environment.TickCount & 0x7FFFFFFFL;
                int checks = 0;
                bool portReady = false;

                while (checks < 60)
                {
                    Thread.Sleep(200);
                    checks++;
                    if (!portReady)
                    {
                        try
                        {
                            using (var test = new TcpClient())
                            {
                                var ar = test.BeginConnect("127.0.0.1", CONTROL_PORT, null, null);
                                if (ar.AsyncWaitHandle.WaitOne(120))
                                {
                                    test.EndConnect(ar);
                                    portReady = true;
                                    Log("EffectCraft control port ready after " + (checks * 200) + "ms");
                                }
                            }
                        }
                        catch { }
                    }

                    long elapsed = ((long)Environment.TickCount & 0x7FFFFFFFL) - startTick;
                    if (portReady && elapsed >= minWaitMs)
                    {
                        break;
                    }
                }

                try
                {
                    SendTcpCommand("{\"id\":99,\"method\":\"ui.focus\",\"params\":{}}");
                    if (_childProc != null)
                    {
                        _childProc.Refresh();
                        if (_childProc.MainWindowHandle != IntPtr.Zero)
                        {
                            SetForegroundWindow(_childProc.MainWindowHandle);
                        }
                    }
                }
                catch { }

                if (_splashWindow != null)
                {
                    _splashWindow.Dispatcher.BeginInvoke(new Action(delegate {
                        _splashWindow.DismissSplash();
                    }));
                }
            });
        }

        private class HotkeyWindow : NativeWindow
        {
            public HotkeyWindow()
            {
                CreateHandle(new CreateParams());
                ReRegister();
            }

            public void ReRegister()
            {
                try
                {
                    UnregisterHotKey(this.Handle, HOTKEY_ID);
                    uint mods = MOD_NOREPEAT;
                    if (Program.HotkeyNeedCtrl) mods |= MOD_CONTROL;
                    if (Program.HotkeyNeedAlt) mods |= MOD_ALT;
                    if (Program.HotkeyNeedShift) mods |= MOD_SHIFT;
                    bool ok = RegisterHotKey(this.Handle, HOTKEY_ID, mods, (uint)Program.HotkeyVk);
                    Log("RegisterHotKey (VK=" + Program.HotkeyVk + ") -> " + ok);
                }
                catch (Exception ex)
                {
                    Log("HotkeyWindow ReRegister error: " + ex.Message);
                }
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
                {
                    Log("WM_HOTKEY fired");
                    if (IsEffectCraftForeground())
                    {
                        TriggerConsole();
                    }
                }
                base.WndProc(ref m);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool isKeyDown = (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN);

                if (isKeyDown && vkCode == Program.HotkeyVk)
                {
                    bool ctrlActive = ((GetAsyncKeyState(0x11) & 0x8000) != 0)
                        || ((GetAsyncKeyState(0xA2) & 0x8000) != 0)
                        || ((GetAsyncKeyState(0xA3) & 0x8000) != 0);

                    bool altActive = ((GetAsyncKeyState(0x12) & 0x8000) != 0)
                        || ((GetAsyncKeyState(0xA4) & 0x8000) != 0)
                        || ((GetAsyncKeyState(0xA5) & 0x8000) != 0);

                    bool shiftActive = ((GetAsyncKeyState(0x10) & 0x8000) != 0)
                        || ((GetAsyncKeyState(0xA0) & 0x8000) != 0)
                        || ((GetAsyncKeyState(0xA1) & 0x8000) != 0);

                    if (ctrlActive == Program.HotkeyNeedCtrl &&
                        altActive == Program.HotkeyNeedAlt &&
                        shiftActive == Program.HotkeyNeedShift)
                    {
                        if (IsEffectCraftForeground())
                        {
                            TriggerConsole();
                            return (IntPtr)1; // Suppress hotkey in EffectCraft
                        }
                    }
                }
            }
            return CallNextHookEx(_kbHookId, nCode, wParam, lParam);
        }

        private static bool IsEffectCraftForeground()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;

            if (_consoleWindow != null && _consoleWindow.IsVisible) return true;

            uint pid = 0;
            GetWindowThreadProcessId(fg, out pid);

            bool matches = false;
            string procName = "";
            try { procName = Process.GetProcessById((int)pid).ProcessName; } catch { }

            if (_childProc != null && !_childProc.HasExited && pid == (uint)_childProc.Id)
                matches = true;
            else if (procName.IndexOf("effectcraft", StringComparison.OrdinalIgnoreCase) >= 0)
                matches = true;

            return matches;
        }

        private static void TriggerConsole()
        {
            long now = (long)Environment.TickCount & 0x7FFFFFFFL;
            if (now - _lastTriggerTick < 250) return;
            _lastTriggerTick = now;

            if (_wpfApp != null)
            {
                _wpfApp.Dispatcher.BeginInvoke(new Action(delegate {
                    ToggleConsoleUI();
                }));
            }
        }

        public static void ToggleConsoleUI()
        {
            if (_consoleWindow == null) _consoleWindow = new FXConsoleWindow();

            if (_consoleWindow.IsVisible) _consoleWindow.HideConsole();
            else _consoleWindow.ShowConsole();
        }

        public static void OpenSettingsUI()
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsWindow();
                _settingsWindow.Closed += delegate { _settingsWindow = null; };
            }
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        public static string SendTcpCommand(string json)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    client.Connect("127.0.0.1", CONTROL_PORT);
                    using (var stream = client.GetStream())
                    {
                        stream.ReadTimeout = 4000;
                        stream.WriteTimeout = 4000;
                        var utf8 = new UTF8Encoding(false);
                        var reader = new StreamReader(stream, utf8);
                        var writer = new StreamWriter(stream, utf8) { AutoFlush = true };

                        writer.WriteLine(json);
                        return reader.ReadLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Log("SendTcpCommand error: " + ex.Message);
                return null;
            }
        }
    }

    public class SplashConfig
    {
        public string Theme = "Dark";
        public string Title = "EffectCraft";
        public string Subtitle = "Motion & Visual Effects";
        public string BadgeText = "Ec";
        public string AccentColor = "#00D2FF";
        public string BadgeBackground = "#0F2338";
        public string ArtistCredit = "Cosmic Horizon Edition";
        public string ArtImage = "splash/art.png";
        public string Shortcut = "Ctrl + Space";
        public double TitleFontSize = 32.0;
        public double SubtitleFontSize = 11.5;
        public double ArtistFontSize = 10.0;
        public double CornerRadius = 18.0;
        public bool ShowLoadingBar = true;
        public bool ShowLoadingText = true;
        public bool ShowFooter = true;
        public double SplashDuration = 3.5;

        // EC Console Catalog Sections
        public bool ConsoleShowEffects = true;
        public bool ConsoleShowPresets = true;
        public bool ConsoleShowLayers = true;
        public bool ConsoleShowCommands = true;
        public bool ConsoleShowRecent = true;

        // EC Console Smart Keyword Prefixes
        public string PrefixEffects = "@fx";
        public string PrefixPresets = "@preset";
        public string PrefixLayers = "@layer";
        public string PrefixCommands = "@cmd";
        public string PrefixRecent = "@recent";

        public static bool IsVideoFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".mp4" || ext == ".wmv" || ext == ".avi" || ext == ".mov" || ext == ".webm";
        }

        public static void PopulateMediaPortal(Border portal, string path)
        {
            portal.Child = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                try
                {
                    using (Stream st = Program.GetEmbeddedSplashStream())
                    {
                        if (st != null)
                        {
                            var img = new System.Windows.Controls.Image
                            {
                                Stretch = Stretch.UniformToFill,
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                VerticalAlignment = System.Windows.VerticalAlignment.Center
                            };
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.StreamSource = st;
                            bmp.EndInit();
                            img.Source = bmp;
                            portal.Child = img;
                            return;
                        }
                    }
                }
                catch { }
                return;
            }

            try
            {
                if (IsVideoFile(path))
                {
                    var media = new System.Windows.Controls.MediaElement
                    {
                        Source = new Uri(path, UriKind.Absolute),
                        LoadedBehavior = System.Windows.Controls.MediaState.Play,
                        UnloadedBehavior = System.Windows.Controls.MediaState.Manual,
                        Stretch = Stretch.UniformToFill,
                        IsMuted = true
                    };
                    media.MediaEnded += delegate(object s, RoutedEventArgs e) {
                        media.Position = TimeSpan.Zero;
                        media.Play();
                    };
                    portal.Child = media;
                }
                else
                {
                    var img = new System.Windows.Controls.Image
                    {
                        Stretch = Stretch.UniformToFill,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center
                    };
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.EndInit();
                    img.Source = bmp;
                    portal.Child = img;
                }
            }
            catch { }
        }

        public static SplashConfig Load(string baseDir)
        {
            var cfg = new SplashConfig();
            string iniPath = Path.Combine(baseDir, "splash", "splash.ini");
            if (!File.Exists(iniPath)) iniPath = Path.Combine(baseDir, "splash.ini");

            if (File.Exists(iniPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(iniPath);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("#") || trimmed.StartsWith(";") || !trimmed.Contains("=")) continue;
                        string[] parts = trimmed.Split(new char[] { '=' }, 2);
                        string key = parts[0].Trim().ToLowerInvariant();
                        string val = parts[1].Trim();

                        if (key == "theme") cfg.Theme = val;
                        else if (key == "title") cfg.Title = val;
                        else if (key == "subtitle") cfg.Subtitle = val;
                        else if (key == "badgetext") cfg.BadgeText = val;
                        else if (key == "accentcolor") cfg.AccentColor = val;
                        else if (key == "badgebackground") cfg.BadgeBackground = val;
                        else if (key == "artistcredit") cfg.ArtistCredit = val;
                        else if (key == "artimage") cfg.ArtImage = val;
                        else if (key == "shortcut") cfg.Shortcut = val;
                        else if (key == "titlefontsize") { double v; if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v)) cfg.TitleFontSize = v; }
                        else if (key == "subtitlefontsize") { double v; if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v)) cfg.SubtitleFontSize = v; }
                        else if (key == "artistfontsize") { double v; if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v)) cfg.ArtistFontSize = v; }
                        else if (key == "cornerradius") { double v; if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v)) cfg.CornerRadius = v; }
                        else if (key == "showloadingbar") { bool b; if (bool.TryParse(val, out b)) cfg.ShowLoadingBar = b; }
                        else if (key == "showloadingtext") { bool b; if (bool.TryParse(val, out b)) cfg.ShowLoadingText = b; }
                        else if (key == "showfooter") { bool b; if (bool.TryParse(val, out b)) cfg.ShowFooter = b; }
                        else if (key == "splashduration") { double v; if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v)) cfg.SplashDuration = v; }
                        else if (key == "consoleshoweffects") { bool b; if (bool.TryParse(val, out b)) cfg.ConsoleShowEffects = b; }
                        else if (key == "consoleshowpresets") { bool b; if (bool.TryParse(val, out b)) cfg.ConsoleShowPresets = b; }
                        else if (key == "consoleshowlayers") { bool b; if (bool.TryParse(val, out b)) cfg.ConsoleShowLayers = b; }
                        else if (key == "consoleshowcommands") { bool b; if (bool.TryParse(val, out b)) cfg.ConsoleShowCommands = b; }
                        else if (key == "consoleshowrecent") { bool b; if (bool.TryParse(val, out b)) cfg.ConsoleShowRecent = b; }
                        else if (key == "prefixeffects") { if (!string.IsNullOrEmpty(val)) cfg.PrefixEffects = val; }
                        else if (key == "prefixpresets") { if (!string.IsNullOrEmpty(val)) cfg.PrefixPresets = val; }
                        else if (key == "prefixlayers") { if (!string.IsNullOrEmpty(val)) cfg.PrefixLayers = val; }
                        else if (key == "prefixcommands") { if (!string.IsNullOrEmpty(val)) cfg.PrefixCommands = val; }
                        else if (key == "prefixrecent") { if (!string.IsNullOrEmpty(val)) cfg.PrefixRecent = val; }
                    }
                }
                catch { }
            }

            if (!Path.IsPathRooted(cfg.ArtImage))
            {
                string candidate = Path.Combine(baseDir, cfg.ArtImage);
                if (File.Exists(candidate))
                {
                    cfg.ArtImage = candidate;
                }
                else if (!string.IsNullOrEmpty(Program.EffectCraftExePath))
                {
                    string ecDir = Path.GetDirectoryName(Program.EffectCraftExePath);
                    string candidate2 = Path.Combine(ecDir, cfg.ArtImage);
                    if (File.Exists(candidate2)) cfg.ArtImage = candidate2;
                    else cfg.ArtImage = candidate;
                }
                else
                {
                    cfg.ArtImage = candidate;
                }
            }

            return cfg;
        }

        public void Save(string baseDir)
        {
            try
            {
                string splashDir = Path.Combine(baseDir, "splash");
                if (!Directory.Exists(splashDir)) Directory.CreateDirectory(splashDir);
                string iniPath = Path.Combine(splashDir, "splash.ini");

                string relArt = ArtImage;
                if (relArt.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                {
                    relArt = relArt.Substring(baseDir.Length).TrimStart('\\', '/');
                }

                var sb = new StringBuilder();
                sb.AppendLine("[Splash]");
                sb.AppendLine("Theme = " + Theme);
                sb.AppendLine("Title = " + Title);
                sb.AppendLine("Subtitle = " + Subtitle);
                sb.AppendLine("BadgeText = " + BadgeText);
                sb.AppendLine("AccentColor = " + AccentColor);
                sb.AppendLine("BadgeBackground = " + BadgeBackground);
                sb.AppendLine("ArtistCredit = " + ArtistCredit);
                sb.AppendLine("ArtImage = " + relArt);
                sb.AppendLine("Shortcut = " + Shortcut);
                sb.AppendLine("TitleFontSize = " + TitleFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("SubtitleFontSize = " + SubtitleFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("ArtistFontSize = " + ArtistFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("CornerRadius = " + CornerRadius.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("ShowLoadingBar = " + ShowLoadingBar);
                sb.AppendLine("ShowLoadingText = " + ShowLoadingText);
                sb.AppendLine("ShowFooter = " + ShowFooter);
                sb.AppendLine("SplashDuration = " + SplashDuration.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("ConsoleShowEffects = " + ConsoleShowEffects);
                sb.AppendLine("ConsoleShowPresets = " + ConsoleShowPresets);
                sb.AppendLine("ConsoleShowLayers = " + ConsoleShowLayers);
                sb.AppendLine("ConsoleShowCommands = " + ConsoleShowCommands);
                sb.AppendLine("ConsoleShowRecent = " + ConsoleShowRecent);
                sb.AppendLine("PrefixEffects = " + PrefixEffects);
                sb.AppendLine("PrefixPresets = " + PrefixPresets);
                sb.AppendLine("PrefixLayers = " + PrefixLayers);
                sb.AppendLine("PrefixCommands = " + PrefixCommands);
                sb.AppendLine("PrefixRecent = " + PrefixRecent);

                File.WriteAllText(iniPath, sb.ToString());
            }
            catch (Exception ex)
            {
                Program.Log("Save splash.ini error: " + ex.Message);
            }
        }

        public Color GetAccentColor()
        {
            try { return (Color)ColorConverter.ConvertFromString(AccentColor); }
            catch { return Color.FromRgb(0, 210, 255); }
        }

        public Color GetBadgeBackground()
        {
            try { return (Color)ColorConverter.ConvertFromString(BadgeBackground); }
            catch { return Color.FromRgb(15, 35, 56); }
        }
    }

    public class SplashWindow : Window
    {
        private TextBlock _statusLabel;
        private Border _progressBar;
        private DispatcherTimer _animTimer;
        private int _step = 0;

        private readonly string[] _loadingSteps = new string[]
        {
            "Initializing Engine Core...",
            "Warming GPU Compositor (Vulkan)...",
            "Scanning Built-in Effects (306/306)...",
            "Loading Plug-ins & Wasm Extensions...",
            "Binding EC Console Bridge...",
            "Starting Interface Session...",
            "Ready!"
        };

        public SplashWindow(SplashConfig cfg)
        {
            this.Title = "EffectCraft";
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.Width = 730;
            this.Height = 450;
            this.ResizeMode = ResizeMode.NoResize;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            bool isLight = cfg.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase);
            Color accent = cfg.GetAccentColor();
            Color badgeBg = cfg.GetBadgeBackground();

            var rootBorder = new Border
            {
                CornerRadius = new CornerRadius(cfg.CornerRadius),
                BorderThickness = new Thickness(1),
                Background = isLight ? Brushes.White : new SolidColorBrush(Color.FromRgb(20, 20, 22)),
                BorderBrush = isLight ? new SolidColorBrush(Color.FromRgb(225, 225, 230)) : new SolidColorBrush(Color.FromRgb(46, 46, 50)),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 14,
                    ShadowDepth = 3,
                    Opacity = isLight ? 0.3 : 0.6
                }
            };

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftStack = new Grid { Margin = new Thickness(34, 30, 20, 26) };
            leftStack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftStack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            leftStack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Top Brand Row
            var topBrandRow = new StackPanel { Orientation = Orientation.Horizontal };
            var badgeBorder = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(Math.Max(6, cfg.CornerRadius * 0.55)),
                Background = new SolidColorBrush(badgeBg),
                BorderBrush = new SolidColorBrush(accent),
                BorderThickness = new Thickness(1.6)
            };
            var badgeMark = new WpfPath
            {
                Data = Geometry.Parse(Program.ARTCRAFT_MARK_PATH),
                Fill = new SolidColorBrush(accent),
                Width = 26,
                Height = 26,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            badgeBorder.Child = badgeMark;
            topBrandRow.Children.Add(badgeBorder);

            var metaStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(12, 2, 0, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            var publisherPill = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Background = isLight ? new SolidColorBrush(Color.FromRgb(240, 240, 244)) : new SolidColorBrush(Color.FromRgb(38, 38, 42)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            var publisherText = new TextBlock
            {
                Text = "ArtCraft",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(100, 100, 105)) : new SolidColorBrush(Color.FromRgb(160, 160, 165))
            };
            publisherPill.Child = publisherText;
            metaStack.Children.Add(publisherPill);

            var subtitleText = new TextBlock
            {
                Text = cfg.Subtitle,
                FontSize = cfg.SubtitleFontSize,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(115, 115, 120)) : new SolidColorBrush(Color.FromRgb(140, 140, 145)),
                Margin = new Thickness(0, 3, 0, 0)
            };
            metaStack.Children.Add(subtitleText);
            topBrandRow.Children.Add(metaStack);
            Grid.SetRow(topBrandRow, 0);
            leftStack.Children.Add(topBrandRow);

            // Middle: Big Title & Credits
            var titleStack = new StackPanel
            {
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(0, 14, 0, 0)
            };
            var mainTitle = new TextBlock
            {
                Text = cfg.Title,
                FontSize = cfg.TitleFontSize,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI, Arial"),
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(25, 25, 28)) : Brushes.White
            };
            titleStack.Children.Add(mainTitle);

            var copyrightText = new TextBlock
            {
                Text = "© 2026 EffectCraft Open Engine. All rights reserved.",
                FontSize = 9.5,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(140, 140, 145)) : new SolidColorBrush(Color.FromRgb(105, 105, 110)),
                Margin = new Thickness(0, 10, 0, 0)
            };
            titleStack.Children.Add(copyrightText);

            var artistText = new TextBlock
            {
                Text = "Artwork: " + cfg.ArtistCredit,
                FontSize = cfg.ArtistFontSize,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(120, 120, 125)) : new SolidColorBrush(Color.FromRgb(135, 135, 140)),
                Margin = new Thickness(0, 3, 0, 0)
            };
            titleStack.Children.Add(artistText);
            Grid.SetRow(titleStack, 1);
            leftStack.Children.Add(titleStack);

            // Bottom: Progress & Brand
            var bottomStack = new StackPanel { VerticalAlignment = System.Windows.VerticalAlignment.Bottom };
            _statusLabel = new TextBlock
            {
                Text = _loadingSteps[0],
                FontSize = 11.5,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(90, 90, 95)) : new SolidColorBrush(Color.FromRgb(180, 180, 185)),
                Margin = new Thickness(0, 0, 0, cfg.ShowLoadingBar ? 8 : 0),
                Visibility = cfg.ShowLoadingText ? Visibility.Visible : Visibility.Collapsed
            };
            bottomStack.Children.Add(_statusLabel);

            var progressTrack = new Border
            {
                Height = 3.5,
                CornerRadius = new CornerRadius(2),
                Background = isLight ? new SolidColorBrush(Color.FromRgb(230, 230, 235)) : new SolidColorBrush(Color.FromRgb(40, 40, 45)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                Visibility = cfg.ShowLoadingBar ? Visibility.Visible : Visibility.Collapsed
            };
            _progressBar = new Border
            {
                Height = 3.5,
                Width = 40,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(accent),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            progressTrack.Child = _progressBar;
            bottomStack.Children.Add(progressTrack);

            var brandFootnote = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 12, 0, 0),
                Visibility = cfg.ShowFooter ? Visibility.Visible : Visibility.Collapsed
            };
            var brandIcon = new WpfPath
            {
                Data = Geometry.Parse(Program.ARTCRAFT_MARK_PATH),
                Fill = new SolidColorBrush(accent),
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            brandFootnote.Children.Add(brandIcon);

            var brandLabel = new TextBlock
            {
                Text = "ArtCraft Motion Creative Suite",
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(120, 120, 125)) : new SolidColorBrush(Color.FromRgb(110, 110, 115)),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            brandFootnote.Children.Add(brandLabel);
            bottomStack.Children.Add(brandFootnote);
            Grid.SetRow(bottomStack, 2);
            leftStack.Children.Add(bottomStack);

            Grid.SetColumn(leftStack, 0);
            mainGrid.Children.Add(leftStack);

            // Right Artwork Portal (Supports both Video and Images)
            var artPortal = new Border
            {
                CornerRadius = new CornerRadius(Math.Max(4, cfg.CornerRadius - 4)),
                ClipToBounds = true,
                Margin = new Thickness(8, 14, 14, 14),
                Background = new SolidColorBrush(Color.FromRgb(15, 15, 20))
            };

            SplashConfig.PopulateMediaPortal(artPortal, cfg.ArtImage);

            Grid.SetColumn(artPortal, 1);
            mainGrid.Children.Add(artPortal);

            rootBorder.Child = mainGrid;
            this.Content = rootBorder;

            int tickMs = (int)Math.Max(80, (cfg.SplashDuration * 1000.0) / (_loadingSteps.Length + 1));
            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(tickMs) };
            _animTimer.Tick += OnLoadingTick;
            _animTimer.Start();
        }

        private void OnLoadingTick(object sender, EventArgs e)
        {
            _step++;
            if (_step < _loadingSteps.Length)
            {
                _statusLabel.Text = _loadingSteps[_step];
                double targetWidth = 40 + (_step * 38.0);
                if (targetWidth > 275) targetWidth = 275;
                _progressBar.Width = targetWidth;
            }
            else
            {
                _animTimer.Stop();
            }
        }

        public void DismissSplash()
        {
            if (_animTimer != null) _animTimer.Stop();
            _statusLabel.Text = "Ready!";
            _progressBar.Width = 275;

            var fade = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(380)));
            fade.Completed += delegate { this.Close(); };
            this.BeginAnimation(Window.OpacityProperty, fade);
        }
    }

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

    public class CaptureItem
    {
        public string ImagePath { get; set; }
        public string CompName { get; set; }
        public string ProjectName { get; set; }
        public string Timecode { get; set; }
        public DateTime CaptureTime { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public BitmapSource Thumbnail { get; set; }
    }

    public class EffectItem
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Category { get; set; }
        public bool IsPreset { get; set; }
        public ConsoleItemKind Kind { get; set; }
        public string Extra { get; set; }
        public CaptureItem Capture { get; set; }
    }

    public class DisplayRow
    {
        public bool IsHeader { get; set; }
        public string HeaderText { get; set; }
        public Color HeaderColor { get; set; }
        public EffectItem Item { get; set; }
        public Border Container { get; set; }
        public TextBlock Label { get; set; }
        public TextBlock ExtraLabel { get; set; }
    }

    public class SmartFilterDef
    {
        public string Prefix { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public ConsoleItemKind? Kind { get; set; }
        public int Count { get; set; }
        public string Description { get; set; }
    }

    public class FXConsoleWindow : Window
    {
        public enum ConsoleTheme { Dark, Darker, Light }

        private static List<CaptureItem> _sessionCaptures = new List<CaptureItem>();
        private static object _sessionCapturesLock = new object();
        private static bool _exitHookRegistered = false;

        private ConsoleTheme _currentTheme = ConsoleTheme.Dark;
        private Border _outerBorder;
        private WpfPath _searchIcon;
        private List<Button> _headerButtons = new List<Button>();
        private List<WpfPath> _headerButtonIcons = new List<WpfPath>();

        private TextBox _searchBox;
        private Border _divider;
        private ScrollViewer _scrollViewer;
        private StackPanel _resultsStack;
        private TextBlock _statusText;

        private List<EffectItem> _catalog = new List<EffectItem>();
        private List<EffectItem> _recentProjects = new List<EffectItem>();
        private List<DisplayRow> _currentRows = new List<DisplayRow>();
        private int _selectedIndex = -1;
        private IntPtr _lastFgHwnd = IntPtr.Zero;
        private bool _isOpening = false;
        private bool _commandsSynced = false;

        public FXConsoleWindow()
        {
            if (!_exitHookRegistered)
            {
                _exitHookRegistered = true;
                AppDomain.CurrentDomain.ProcessExit += delegate {
                    PurgeSessionCaptures();
                };
            }

            InitializeCatalog();
            SyncLiveCommandsFromEngine();

            this.Title = "EC Console";
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.Width = 530;
            this.SizeToContent = SizeToContent.Height;
            this.ResizeMode = ResizeMode.NoResize;
            this.Deactivated += delegate {
                if (_isOpening) return;
                HideConsole();
            };

            // Modern sleek dynamic scrollbar style matching EffectCraft UI
            try
            {
                string scrollStyleXaml = @"
                <ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                    <Style TargetType=""{x:Type ScrollBar}"">
                        <Setter Property=""Background"" Value=""Transparent""/>
                        <Setter Property=""BorderThickness"" Value=""0""/>
                        <Setter Property=""Width"" Value=""6""/>
                        <Setter Property=""Template"">
                            <Setter.Value>
                                <ControlTemplate TargetType=""{x:Type ScrollBar}"">
                                    <Grid Background=""Transparent"">
                                        <Track x:Name=""PART_Track"" IsDirectionReversed=""true"">
                                            <Track.Thumb>
                                                <Thumb>
                                                    <Thumb.Template>
                                                        <ControlTemplate TargetType=""{x:Type Thumb}"">
                                                            <Border x:Name=""thumbBorder"" Background=""{DynamicResource ThumbBrush}"" CornerRadius=""3"" Margin=""1,2,1,2""/>
                                                            <ControlTemplate.Triggers>
                                                                <Trigger Property=""IsMouseOver"" Value=""true"">
                                                                    <Setter TargetName=""thumbBorder"" Property=""Background"" Value=""{DynamicResource ThumbHoverBrush}""/>
                                                                </Trigger>
                                                            </ControlTemplate.Triggers>
                                                        </ControlTemplate>
                                                    </Thumb.Template>
                                                </Thumb>
                                            </Track.Thumb>
                                        </Track>
                                    </Grid>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>
                </ResourceDictionary>";
                var dict = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(scrollStyleXaml);
                this.Resources.MergedDictionaries.Add(dict);
            }
            catch { }

            this.Resources["ThumbBrush"] = new SolidColorBrush(Color.FromRgb(74, 74, 84));
            this.Resources["ThumbHoverBrush"] = new SolidColorBrush(Color.FromRgb(101, 101, 117));

            _outerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 24,
                    ShadowDepth = 4,
                    Opacity = 0.75
                }
            };

            var mainStack = new StackPanel { Orientation = Orientation.Vertical };

            // Top Search Bar
            var searchHeader = new Grid
            {
                Height = 44,
                Margin = new Thickness(12, 0, 8, 0)
            };
            searchHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            searchHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _searchIcon = new WpfPath
            {
                Data = Geometry.Parse("M 5,9 A 4,4 0 1,1 13,9 A 4,4 0 1,1 5,9 M 12,12 L 18,18"),
                Stroke = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                StrokeThickness = 2.2,
                Width = 22,
                Height = 22,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            Grid.SetColumn(_searchIcon, 0);
            searchHeader.Children.Add(_searchIcon);

            _searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                CaretBrush = new SolidColorBrush(Color.FromRgb(0, 168, 255)),
                FontSize = 17,
                FontFamily = new FontFamily("Segoe UI, Arial"),
                VerticalContentAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 8, 0)
            };
            _searchBox.TextChanged += OnSearchTextChanged;
            _searchBox.PreviewKeyDown += OnSearchPreviewKeyDown;
            Grid.SetColumn(_searchBox, 1);
            searchHeader.Children.Add(_searchBox);

            var actionStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            // 1. Snapshot Camera Button
            var cameraBtn = CreateIconButton(
                "M 6,9 L 8,6 L 14,6 L 16,9 L 19,9 C 20,9 21,10 21,11 L 21,18 C 21,19 20,20 19,20 L 3,20 C 2,20 1,19 1,18 L 1,11 C 1,10 2,9 3,9 Z M 11,17 A 3.5,3.5 0 1,0 11,10 A 3.5,3.5 0 1,0 11,17",
                "Snapshot / Copy Frame to Clipboard",
                delegate { TakeSnapshot(); }
            );
            actionStack.Children.Add(cameraBtn);

            // 2. Captures Gallery / Presets Button
            var galleryBtn = CreateIconButton(
                "M 3,4 L 16,4 C 17,4 17,5 17,5 L 17,15 C 17,16 16,16 16,16 L 3,16 C 2,16 2,15 2,15 L 2,5 C 2,4 3,4 3,4 Z M 6,19 L 19,19 C 20,19 20,18 20,18 L 20,8",
                "Captures Gallery / Animation Presets",
                delegate {
                    lock (_sessionCapturesLock)
                    {
                        if (_sessionCaptures.Count > 0)
                        {
                            _searchBox.Text = "@sc ";
                            _searchBox.CaretIndex = _searchBox.Text.Length;
                            return;
                        }
                    }
                    string pre = (Program.CurrentConfig != null && !string.IsNullOrEmpty(Program.CurrentConfig.PrefixPresets)) ? Program.CurrentConfig.PrefixPresets : "@preset";
                    _searchBox.Text = pre + " ";
                    _searchBox.CaretIndex = _searchBox.Text.Length;
                }
            );
            actionStack.Children.Add(galleryBtn);

            // 3. Export PNG Button
            var exportBtn = CreateIconButton(
                "M 11,3 L 11,14 M 7,10 L 11,14 L 15,10 M 3,17 L 3,19 L 19,19 L 19,17",
                "Save Frame As PNG",
                delegate { SaveFramePng(); }
            );
            actionStack.Children.Add(exportBtn);

            // 4. Settings Gear Button -> Opens Settings & Customizer Window!
            var gearBtn = CreateIconButton(
                "M 11,2 L 13,2 L 13.5,5 L 15.5,5.8 L 18,3.8 L 19.5,5.3 L 17.5,7.8 L 18.3,9.8 L 21.3,10.3 L 21.3,12.3 L 18.3,12.8 L 17.5,14.8 L 19.5,17.3 L 18,18.8 L 15.5,16.8 L 13.5,17.6 L 13,20.6 L 11,20.6 L 10.5,17.6 L 8.5,16.8 L 6,18.8 L 4.5,17.3 L 6.5,14.8 L 5.7,12.8 L 2.7,12.3 L 2.7,10.3 L 5.7,9.8 L 6.5,7.8 L 4.5,5.3 L 6,3.8 L 8.5,5.8 L 10.5,5 Z M 12,14 A 2.8,2.8 0 1,0 12,8.4 A 2.8,2.8 0 1,0 12,14",
                "EC Console & Studio Settings",
                delegate {
                    HideConsole();
                    Program.OpenSettingsUI();
                }
            );
            actionStack.Children.Add(gearBtn);

            // 5. Official ArtCraft Brand Emblem Button
            var logoBtn = CreateIconButton(
                Program.ARTCRAFT_MARK_PATH,
                "EffectCraft",
                null,
                Color.FromRgb(78, 123, 251)
            );
            actionStack.Children.Add(logoBtn);

            Grid.SetColumn(actionStack, 2);
            searchHeader.Children.Add(actionStack);
            mainStack.Children.Add(searchHeader);

            // Divider Line
            _divider = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(42, 42, 42)),
                Visibility = Visibility.Collapsed
            };
            mainStack.Children.Add(_divider);

            // Results Dropdown
            _resultsStack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 4, 0, 4) };
            _scrollViewer = new ScrollViewer
            {
                MaxHeight = 280,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _resultsStack,
                Visibility = Visibility.Collapsed
            };
            mainStack.Children.Add(_scrollViewer);

            // Toast Text
            _statusText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 255)),
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI"),
                Margin = new Thickness(14, 4, 14, 8),
                Visibility = Visibility.Collapsed
            };
            mainStack.Children.Add(_statusText);

            _outerBorder.Child = mainStack;
            this.Content = _outerBorder;

            ApplyTheme(DetectEffectCraftTheme());
        }

        private Button CreateIconButton(string pathData, string toolTip, Action onClick, Color? fillColor = null)
        {
            var btn = new Button
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(2, 0, 2, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = toolTip
            };

            Color normColor = fillColor ?? (_currentTheme == ConsoleTheme.Light ? Color.FromRgb(70, 70, 75) : Color.FromRgb(180, 180, 180));
            var path = new WpfPath
            {
                Data = Geometry.Parse(pathData),
                Fill = (fillColor != null) ? new SolidColorBrush(normColor) : null,
                Stroke = (fillColor == null) ? new SolidColorBrush(normColor) : null,
                StrokeThickness = (fillColor == null) ? 1.5 : 0,
                Width = 18,
                Height = 18,
                Stretch = Stretch.Uniform,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };
            btn.Content = path;

            btn.MouseEnter += delegate {
                btn.Background = (_currentTheme == ConsoleTheme.Light) 
                    ? new SolidColorBrush(Color.FromRgb(220, 220, 226)) 
                    : new SolidColorBrush(Color.FromRgb(48, 48, 48));
                if (fillColor == null) path.Stroke = (_currentTheme == ConsoleTheme.Light) ? Brushes.Black : Brushes.White;
            };
            btn.MouseLeave += delegate {
                btn.Background = Brushes.Transparent;
                if (fillColor == null) path.Stroke = new SolidColorBrush(_currentTheme == ConsoleTheme.Light ? Color.FromRgb(70, 70, 75) : Color.FromRgb(180, 180, 180));
            };

            if (onClick != null) btn.Click += delegate { onClick(); };
            _headerButtons.Add(btn);
            _headerButtonIcons.Add(path);
            return btn;
        }

        private ConsoleTheme DetectEffectCraftTheme()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string prefsPath = Path.Combine(appData, "EffectCraft", "prefs.json");
                if (File.Exists(prefsPath))
                {
                    string content = File.ReadAllText(prefsPath);
                    string mode = "auto";
                    int idxMode = content.IndexOf("\"appearanceMode\"");
                    if (idxMode >= 0)
                    {
                        int colon = content.IndexOf(":", idxMode);
                        int q1 = content.IndexOf("\"", colon);
                        int q2 = content.IndexOf("\"", q1 + 1);
                        if (q1 >= 0 && q2 > q1) mode = content.Substring(q1 + 1, q2 - q1 - 1).ToLowerInvariant();
                    }

                    if (mode == "light") return ConsoleTheme.Light;
                    if (mode == "dark")
                    {
                        if (content.Contains("\"darkTheme\":\"darker\"") || content.Contains("\"darkTheme\": \"darker\""))
                            return ConsoleTheme.Darker;
                        return ConsoleTheme.Dark;
                    }
                    if (mode == "auto")
                    {
                        try
                        {
                            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                            {
                                if (key != null)
                                {
                                    object val = key.GetValue("AppsUseLightTheme");
                                    if (val is int && ((int)val) == 1) return ConsoleTheme.Light;
                                }
                            }
                        }
                        catch { }
                        return ConsoleTheme.Dark;
                    }
                }
            }
            catch { }
            return ConsoleTheme.Dark;
        }

        private void ApplyTheme(ConsoleTheme theme)
        {
            _currentTheme = theme;

            Color bgColor;
            Color borderColor;
            Color inputFg;
            Color iconColor;
            Color divColor;
            Color thumbColor;
            Color thumbHoverColor;

            if (theme == ConsoleTheme.Light)
            {
                bgColor = Color.FromRgb(242, 242, 246);
                borderColor = Color.FromRgb(190, 190, 198);
                inputFg = Color.FromRgb(25, 25, 25);
                iconColor = Color.FromRgb(70, 70, 75);
                divColor = Color.FromRgb(215, 215, 222);
                thumbColor = Color.FromRgb(180, 180, 190);
                thumbHoverColor = Color.FromRgb(150, 150, 162);
            }
            else if (theme == ConsoleTheme.Darker)
            {
                bgColor = Color.FromRgb(16, 16, 16);
                borderColor = Color.FromRgb(36, 36, 36);
                inputFg = Colors.White;
                iconColor = Color.FromRgb(160, 160, 165);
                divColor = Color.FromRgb(32, 32, 32);
                thumbColor = Color.FromRgb(55, 55, 62);
                thumbHoverColor = Color.FromRgb(80, 80, 90);
            }
            else // Dark (Default)
            {
                bgColor = Color.FromRgb(26, 26, 26);
                borderColor = Color.FromRgb(50, 50, 50);
                inputFg = Colors.White;
                iconColor = Color.FromRgb(180, 180, 180);
                divColor = Color.FromRgb(50, 50, 50);
                thumbColor = Color.FromRgb(74, 74, 84);
                thumbHoverColor = Color.FromRgb(101, 101, 117);
            }

            if (_outerBorder != null)
            {
                _outerBorder.Background = new SolidColorBrush(bgColor);
                _outerBorder.BorderBrush = new SolidColorBrush(borderColor);
            }
            if (_searchBox != null)
            {
                _searchBox.Foreground = new SolidColorBrush(inputFg);
                _searchBox.CaretBrush = (theme == ConsoleTheme.Light)
                    ? new SolidColorBrush(Color.FromRgb(0, 120, 215))
                    : new SolidColorBrush(Color.FromRgb(0, 168, 255));
            }
            if (_searchIcon != null)
            {
                _searchIcon.Stroke = new SolidColorBrush(iconColor);
            }
            if (_divider != null)
            {
                _divider.Background = new SolidColorBrush(divColor);
            }
            for (int i = 0; i < _headerButtonIcons.Count; i++)
            {
                var p = _headerButtonIcons[i];
                if (p.Fill == null && p.Stroke != null)
                {
                    p.Stroke = new SolidColorBrush(iconColor);
                }
            }

            this.Resources["ThumbBrush"] = new SolidColorBrush(thumbColor);
            this.Resources["ThumbHoverBrush"] = new SolidColorBrush(thumbHoverColor);
        }

        private void RefreshRecentProjects()
        {
            _recentProjects.Clear();
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string prefsPath = Path.Combine(appData, "EffectCraft", "prefs.json");
                if (File.Exists(prefsPath))
                {
                    string content = File.ReadAllText(prefsPath);
                    int idx = content.IndexOf("\"recentProjects\"");
                    if (idx < 0) idx = content.IndexOf("\"recent_projects\"");
                    if (idx >= 0)
                    {
                        int start = content.IndexOf('[', idx);
                        int end = content.IndexOf(']', start);
                        if (start >= 0 && end > start)
                        {
                            string arr = content.Substring(start + 1, end - start - 1);
                            string[] items = arr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string raw in items)
                            {
                                string p = raw.Trim().Trim('"', ' ', '\r', '\n').Replace("\\\\", "\\");
                                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                                {
                                    string filename = Path.GetFileName(p);
                                    _recentProjects.Add(new EffectItem {
                                        Name = filename,
                                        Id = p,
                                        Category = "Recent Projects",
                                        Kind = ConsoleItemKind.Project,
                                        Extra = p
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public void ShowConsole()
        {
            _isOpening = true;

            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            IntPtr thisHwnd = helper.EnsureHandle();

            IntPtr fg = Program.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != thisHwnd)
            {
                _lastFgHwnd = fg;
            }

            // Spawn directly at the current mouse cursor location!
            var cur = System.Windows.Forms.Cursor.Position;
            var screen = System.Windows.Forms.Screen.FromPoint(cur).WorkingArea;

            double targetLeft = cur.X - (this.Width / 2.0);
            double targetTop = cur.Y - 22; // Align search input with cursor height

            // Clamp within screen boundaries so it never overflows off-screen
            if (targetLeft < screen.Left + 10) targetLeft = screen.Left + 10;
            if (targetLeft + this.Width > screen.Right - 10) targetLeft = screen.Right - this.Width - 10;
            if (targetTop < screen.Top + 10) targetTop = screen.Top + 10;
            if (targetTop + 330 > screen.Bottom - 10) targetTop = screen.Bottom - 330 - 10;

            this.Left = targetLeft;
            this.Top = targetTop;

            ApplyTheme(DetectEffectCraftTheme());
            _searchBox.Text = "";
            _statusText.Visibility = Visibility.Collapsed;
            _statusText.Foreground = (_currentTheme == ConsoleTheme.Light)
                ? new SolidColorBrush(Color.FromRgb(0, 120, 215))
                : new SolidColorBrush(Color.FromRgb(0, 200, 255));
            RefreshRecentProjects();
            if (!_commandsSynced)
            {
                SyncLiveCommandsFromEngine();
            }
            this.Show();

            // Seamless foreground transfer via Win32 AttachThreadInput
            uint fgThread = 0;
            if (_lastFgHwnd != IntPtr.Zero)
            {
                uint dummyPid;
                fgThread = Program.GetWindowThreadProcessId(_lastFgHwnd, out dummyPid);
            }
            uint appThread = Program.GetCurrentThreadId();

            if (fgThread != 0 && fgThread != appThread)
            {
                Program.AttachThreadInput(appThread, fgThread, true);
                Program.BringWindowToTop(thisHwnd);
                Program.SetForegroundWindow(thisHwnd);
                Program.AttachThreadInput(appThread, fgThread, false);
            }
            else
            {
                Program.BringWindowToTop(thisHwnd);
                Program.SetForegroundWindow(thisHwnd);
            }

            this.Topmost = true;
            this.Activate();
            this.Focus();

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(delegate {
                _searchBox.Focus();
                Keyboard.Focus(_searchBox);
                _searchBox.SelectAll();
                _isOpening = false;
            }));
        }

        public void HideConsole()
        {
            _isOpening = false;
            this.Hide();
            if (_lastFgHwnd != IntPtr.Zero)
            {
                uint dummyPid;
                uint targetThread = Program.GetWindowThreadProcessId(_lastFgHwnd, out dummyPid);
                uint appThread = Program.GetCurrentThreadId();

                if (targetThread != 0 && targetThread != appThread)
                {
                    Program.AttachThreadInput(appThread, targetThread, true);
                    Program.SetForegroundWindow(_lastFgHwnd);
                    Program.BringWindowToTop(_lastFgHwnd);
                    Program.AttachThreadInput(appThread, targetThread, false);
                }
                else
                {
                    Program.SetForegroundWindow(_lastFgHwnd);
                }
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            string query = _searchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                _divider.Visibility = Visibility.Collapsed;
                _scrollViewer.Visibility = Visibility.Collapsed;
                _resultsStack.Children.Clear();
                _currentRows.Clear();
                _selectedIndex = -1;
                return;
            }
            UpdateFilter(query);
        }

        private List<SmartFilterDef> GetDynamicFilters(string triggerChar)
        {
            var list = new List<SmartFilterDef>();
            var cfg = Program.CurrentConfig ?? new SplashConfig();
            string trig = string.IsNullOrEmpty(triggerChar) ? "@" : triggerChar;

            Func<string, string, string> formatPrefix = delegate(string customPref, string defaultSuffix) {
                if (!string.IsNullOrEmpty(customPref))
                {
                    if (trig == "#" && customPref.StartsWith("@")) return "#" + customPref.Substring(1);
                    if (trig == "@" && customPref.StartsWith("#")) return "@" + customPref.Substring(1);
                    return customPref;
                }
                return trig + defaultSuffix;
            };

            // 1. Recent Projects (dynamically included only if enabled and projects exist!)
            if (cfg.ConsoleShowRecent && _recentProjects.Count > 0)
            {
                list.Add(new SmartFilterDef {
                    Prefix = formatPrefix(cfg.PrefixRecent, "recent"),
                    Name = "Recent Projects",
                    Kind = ConsoleItemKind.Project,
                    Count = _recentProjects.Count,
                    Description = "Filter recent .ecproj files (" + _recentProjects.Count + ")"
                });
            }

            // 2. Effects Catalog (dynamically included only if enabled and effects exist!)
            if (cfg.ConsoleShowEffects)
            {
                int effCount = _catalog.FindAll(x => x.Kind == ConsoleItemKind.Effect).Count;
                if (effCount > 0)
                {
                    list.Add(new SmartFilterDef {
                        Prefix = formatPrefix(cfg.PrefixEffects, "fx"),
                        Name = "Effects",
                        Kind = ConsoleItemKind.Effect,
                        Count = effCount,
                        Description = "Filter effects (" + effCount + ")"
                    });
                }
            }

            // 3. Animation Presets
            if (cfg.ConsoleShowPresets)
            {
                int preCount = _catalog.FindAll(x => x.Kind == ConsoleItemKind.Preset).Count;
                if (preCount > 0)
                {
                    list.Add(new SmartFilterDef {
                        Prefix = formatPrefix(cfg.PrefixPresets, "preset"),
                        Name = "Presets",
                        Kind = ConsoleItemKind.Preset,
                        Count = preCount,
                        Description = "Filter animation presets (" + preCount + ")"
                    });
                }
            }

            // 4. Quick Layer Generators
            if (cfg.ConsoleShowLayers)
            {
                int layCount = _catalog.FindAll(x => x.Kind == ConsoleItemKind.Layer).Count;
                if (layCount > 0)
                {
                    list.Add(new SmartFilterDef {
                        Prefix = formatPrefix(cfg.PrefixLayers, "layer"),
                        Name = "New Layers",
                        Kind = ConsoleItemKind.Layer,
                        Count = layCount,
                        Description = "Filter layer generators (" + layCount + ")"
                    });
                }
            }

            // 5. Session Captures (Screenshots)
            lock (_sessionCapturesLock)
            {
                if (_sessionCaptures.Count > 0)
                {
                    list.Add(new SmartFilterDef {
                        Prefix = formatPrefix(null, "sc"),
                        Name = "Screenshots",
                        Kind = ConsoleItemKind.Capture,
                        Count = _sessionCaptures.Count,
                        Description = "Browse current session captures (" + _sessionCaptures.Count + ")"
                    });
                }
            }

            // 6. Dynamic Engine & Menu Command Categories from _catalog
            if (cfg.ConsoleShowCommands)
            {
                var catCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in _catalog)
                {
                    if (item.Kind == ConsoleItemKind.Command && !string.IsNullOrEmpty(item.Category))
                    {
                        int c;
                        catCounts.TryGetValue(item.Category, out c);
                        catCounts[item.Category] = c + 1;
                    }
                }

                var shortAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    { "Composition", "comp" },
                    { "Edit", "edit" },
                    { "File", "file" },
                    { "Animation", "anim" },
                    { "View", "view" },
                    { "Window", "window" },
                    { "Help", "help" }
                };

                foreach (var kvp in catCounts)
                {
                    string catName = kvp.Key;
                    int count = kvp.Value;
                    if (count == 0) continue;

                    string alias;
                    if (!shortAliases.TryGetValue(catName, out alias))
                    {
                        alias = catName.ToLowerInvariant().Replace(" ", "").Replace("&", "").Replace("▸", "");
                        if (alias.Length > 8) alias = alias.Substring(0, 8);
                    }

                    list.Add(new SmartFilterDef {
                        Prefix = trig + alias,
                        Name = catName,
                        Category = catName,
                        Kind = ConsoleItemKind.Command,
                        Count = count,
                        Description = "Filter " + catName + " commands (" + count + ")"
                    });
                }

                int totalCmds = _catalog.FindAll(x => x.Kind == ConsoleItemKind.Command).Count;
                if (totalCmds > 0)
                {
                    list.Add(new SmartFilterDef {
                        Prefix = formatPrefix(cfg.PrefixCommands, "cmd"),
                        Name = "All Commands",
                        Kind = ConsoleItemKind.Command,
                        Count = totalCmds,
                        Description = "Filter all commands (" + totalCmds + ")"
                    });
                }
            }

            return list;
        }

        private void UpdateFilter(string query)
        {
            _resultsStack.Children.Clear();
            _currentRows.Clear();
            _selectedIndex = -1;

            var cfg = Program.CurrentConfig ?? new SplashConfig();

            string trigChar = query.StartsWith("#") ? "#" : "@";
            var dynamicFilters = GetDynamicFilters(trigChar);

            SmartFilterDef activeFilter = null;
            string cleanQuery = query.Trim();

            // Detect if query activates any dynamic filter
            foreach (var f in dynamicFilters)
            {
                if (query.StartsWith(f.Prefix + " ", StringComparison.OrdinalIgnoreCase) ||
                    query.Equals(f.Prefix, StringComparison.OrdinalIgnoreCase))
                {
                    activeFilter = f;
                    cleanQuery = query.Substring(f.Prefix.Length).Trim();
                    break;
                }
                else if (f.Kind == ConsoleItemKind.Capture &&
                    (query.StartsWith(trigChar + "screenshot ", StringComparison.OrdinalIgnoreCase) ||
                     query.Equals(trigChar + "screenshot", StringComparison.OrdinalIgnoreCase)))
                {
                    activeFilter = f;
                    cleanQuery = query.Substring((trigChar + "screenshot").Length).Trim();
                    break;
                }
            }

            // 0. Dynamic Smart Filter Suggestions (when typing trigger or searching prefix name without spaces)
            var matchingFilters = new List<EffectItem>();
            if (!query.Contains(" "))
            {
                string qLower = query.Trim().ToLowerInvariant();
                foreach (var f in dynamicFilters)
                {
                    bool match = false;
                    if (qLower == "@" || qLower == "#")
                    {
                        match = true;
                    }
                    else if (f.Prefix.ToLowerInvariant().StartsWith(qLower))
                    {
                        match = true;
                    }
                    else if (qLower.StartsWith("@") || qLower.StartsWith("#"))
                    {
                        string qTrimmed = qLower.TrimStart('@', '#');
                        string pTrimmed = f.Prefix.ToLowerInvariant().TrimStart('@', '#');
                        if (!string.IsNullOrEmpty(qTrimmed) && (pTrimmed.StartsWith(qTrimmed) || f.Name.ToLowerInvariant().StartsWith(qTrimmed)))
                        {
                            match = true;
                        }
                    }

                    if (match)
                    {
                        matchingFilters.Add(new EffectItem {
                            Name = f.Prefix + "  —  " + f.Name,
                            Id = f.Prefix,
                            Category = "Smart Filters",
                            Kind = ConsoleItemKind.Filter,
                            Extra = f.Description
                        });
                    }
                }
            }

            var matchCaptures = new List<EffectItem>();
            var matchRecent = new List<EffectItem>();
            var matchLayers = new List<EffectItem>();
            var matchEffects = new List<EffectItem>();
            var matchPresets = new List<EffectItem>();
            var matchCommands = new List<EffectItem>();

            bool isOnlyTrigger = (query == "@" || query == "#");

            if (!isOnlyTrigger)
            {
                // 0. Session Captures (Screenshots)
                if (activeFilter != null && activeFilter.Kind == ConsoleItemKind.Capture)
                {
                    lock (_sessionCapturesLock)
                    {
                        foreach (var cap in _sessionCaptures)
                        {
                            if (string.IsNullOrEmpty(cleanQuery) ||
                                cap.CompName.IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                cap.ProjectName.IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                matchCaptures.Add(new EffectItem {
                                    Name = cap.CompName + (string.IsNullOrEmpty(cap.Timecode) ? "" : " [" + cap.Timecode + "]"),
                                    Id = cap.ImagePath,
                                    Category = "Screenshots",
                                    Kind = ConsoleItemKind.Capture,
                                    Extra = (string.IsNullOrEmpty(cap.ProjectName) ? "" : "📁 " + cap.ProjectName + " • ") +
                                            (cap.Width > 0 ? (cap.Width + "×" + cap.Height + " • ") : "") +
                                            cap.CaptureTime.ToString("hh:mm:ss tt"),
                                    Capture = cap
                                });
                            }
                        }
                    }
                }

                // 1. Recent Projects
                bool allowRecent = cfg.ConsoleShowRecent && (activeFilter == null || activeFilter.Kind == ConsoleItemKind.Project);
                if (allowRecent)
                {
                    foreach (var proj in _recentProjects)
                    {
                        if (string.IsNullOrEmpty(cleanQuery) ||
                            proj.Name.IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            proj.Id.IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchRecent.Add(proj);
                        }
                    }
                }

                // 2. Catalog Items (Layers, Commands, Effects, Presets)
                if (activeFilter == null || activeFilter.Kind != ConsoleItemKind.Capture)
                {
                    foreach (var item in _catalog)
                    {
                        bool matchesQuery = string.IsNullOrEmpty(cleanQuery) ||
                            item.Name.IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            item.Category.IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0;

                        if (!matchesQuery) continue;

                        if (activeFilter != null)
                        {
                            if (activeFilter.Kind == ConsoleItemKind.Project)
                            {
                                continue;
                            }
                            else if (!string.IsNullOrEmpty(activeFilter.Category))
                            {
                                if (!item.Category.Equals(activeFilter.Category, StringComparison.OrdinalIgnoreCase))
                                    continue;
                            }
                            else if (activeFilter.Kind != null)
                            {
                                if (item.Kind != activeFilter.Kind.Value)
                                    continue;
                            }
                        }

                        if (item.Kind == ConsoleItemKind.Layer)
                        {
                            if (cfg.ConsoleShowLayers || activeFilter != null)
                                matchLayers.Add(item);
                        }
                        else if (item.Kind == ConsoleItemKind.Command)
                        {
                            if (cfg.ConsoleShowCommands || activeFilter != null)
                                matchCommands.Add(item);
                        }
                        else if (item.Kind == ConsoleItemKind.Preset)
                        {
                            if (cfg.ConsoleShowPresets || activeFilter != null)
                                matchPresets.Add(item);
                        }
                        else if (item.Kind == ConsoleItemKind.Effect)
                        {
                            if (cfg.ConsoleShowEffects || activeFilter != null)
                                matchEffects.Add(item);
                        }
                    }
                }
            }

            int added = 0;
            const int MAX_ROWS = 28;

            if (matchingFilters.Count > 0 && added < MAX_ROWS)
            {
                AddCategoryHeader("Smart Filters (Press Enter or Click to Filter)", Color.FromRgb(0, 210, 255));
                foreach (var sf in matchingFilters)
                {
                    if (added++ >= MAX_ROWS) break;
                    AddItemRow(sf);
                }
            }

            if (activeFilter != null && activeFilter.Kind == ConsoleItemKind.Capture)
            {
                if (matchCaptures.Count > 0 && added < MAX_ROWS)
                {
                    AddCategoryHeader("Session Captures (" + matchCaptures.Count + ")", Color.FromRgb(0, 200, 255));
                    foreach (var cap in matchCaptures)
                    {
                        if (added++ >= MAX_ROWS) break;
                        AddItemRow(cap);
                    }
                }
                else if (matchCaptures.Count == 0 && added < MAX_ROWS)
                {
                    AddCategoryHeader("Session Captures (0 taken yet - Click 📷 to snapshot)", Color.FromRgb(255, 184, 0));
                }
            }

            if (matchRecent.Count > 0 && added < MAX_ROWS)
            {
                AddCategoryHeader("Recent Projects", Color.FromRgb(255, 184, 0));
                foreach (var proj in matchRecent)
                {
                    if (added++ >= MAX_ROWS) break;
                    AddItemRow(proj);
                }
            }

            if (matchLayers.Count > 0 && added < MAX_ROWS)
            {
                AddCategoryHeader("New Layer", Color.FromRgb(255, 107, 74));
                foreach (var lay in matchLayers)
                {
                    if (added++ >= MAX_ROWS) break;
                    AddItemRow(lay);
                }
            }

            if (matchEffects.Count > 0 && added < MAX_ROWS)
            {
                AddCategoryHeader("Effects", Color.FromRgb(0, 168, 255));
                foreach (var eff in matchEffects)
                {
                    if (added++ >= MAX_ROWS) break;
                    AddItemRow(eff);
                }
            }

            if (matchPresets.Count > 0 && added < MAX_ROWS)
            {
                AddCategoryHeader("Presets", Color.FromRgb(165, 94, 234));
                foreach (var pre in matchPresets)
                {
                    if (added++ >= MAX_ROWS) break;
                    AddItemRow(pre);
                }
            }

            if (matchCommands.Count > 0 && added < MAX_ROWS)
            {
                string cmdHeader = (activeFilter != null && !string.IsNullOrEmpty(activeFilter.Category)) ? activeFilter.Category : "Commands";
                AddCategoryHeader(cmdHeader, Color.FromRgb(46, 213, 115));
                foreach (var cmd in matchCommands)
                {
                    if (added++ >= MAX_ROWS) break;
                    AddItemRow(cmd);
                }
            }

            if (_currentRows.Count > 0)
            {
                _divider.Visibility = Visibility.Visible;
                _scrollViewer.Visibility = Visibility.Visible;

                for (int i = 0; i < _currentRows.Count; i++)
                {
                    if (!_currentRows[i].IsHeader)
                    {
                        SetSelectedIndex(i);
                        break;
                    }
                }
            }
            else
            {
                _divider.Visibility = Visibility.Collapsed;
                _scrollViewer.Visibility = Visibility.Collapsed;
            }
        }

        private void AddCategoryHeader(string name, Color dotColor)
        {
            var headerGrid = new Grid { Height = 24, Margin = new Thickness(14, 4, 14, 2) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(dotColor),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            Grid.SetColumn(dot, 0);
            headerGrid.Children.Add(dot);

            var label = new TextBlock
            {
                Text = name,
                Foreground = (_currentTheme == ConsoleTheme.Light)
                    ? new SolidColorBrush(Color.FromRgb(100, 100, 108))
                    : new SolidColorBrush(Color.FromRgb(140, 140, 140)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            Grid.SetColumn(label, 1);
            headerGrid.Children.Add(label);

            _resultsStack.Children.Add(headerGrid);
            _currentRows.Add(new DisplayRow { IsHeader = true, HeaderText = name, HeaderColor = dotColor });
        }

        private void AddItemRow(EffectItem item)
        {
            int rowIndex = _currentRows.Count;

            // Rich Visual Card for Session Screenshot Captures
            if (item.Kind == ConsoleItemKind.Capture && item.Capture != null)
            {
                var capBorder = new Border
                {
                    Height = 52,
                    Margin = new Thickness(4, 2, 4, 2),
                    CornerRadius = new CornerRadius(4),
                    Background = Brushes.Transparent,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var capGrid = new Grid();
                capGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
                capGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                capGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var thumbBorder = new Border
                {
                    Width = 72,
                    Height = 44,
                    CornerRadius = new CornerRadius(3),
                    BorderThickness = new Thickness(1),
                    BorderBrush = (_currentTheme == ConsoleTheme.Light)
                        ? new SolidColorBrush(Color.FromRgb(200, 200, 208))
                        : new SolidColorBrush(Color.FromRgb(50, 50, 55)),
                    Background = (_currentTheme == ConsoleTheme.Light)
                        ? new SolidColorBrush(Color.FromRgb(230, 230, 235))
                        : new SolidColorBrush(Color.FromRgb(15, 15, 15)),
                    ClipToBounds = true,
                    Margin = new Thickness(6, 0, 4, 0),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };

                if (item.Capture.Thumbnail != null)
                {
                    var img = new Image
                    {
                        Source = item.Capture.Thumbnail,
                        Stretch = Stretch.UniformToFill
                    };
                    thumbBorder.Child = img;
                }
                Grid.SetColumn(thumbBorder, 0);
                capGrid.Children.Add(thumbBorder);

                var textStack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 6, 0)
                };

                var capTitle = new TextBlock
                {
                    Text = item.Name,
                    Foreground = (_currentTheme == ConsoleTheme.Light)
                        ? new SolidColorBrush(Color.FromRgb(28, 28, 30))
                        : new SolidColorBrush(Color.FromRgb(235, 235, 235)),
                    FontSize = 13.0,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Segoe UI"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                textStack.Children.Add(capTitle);

                var capMeta = new TextBlock
                {
                    Text = item.Extra,
                    Foreground = (_currentTheme == ConsoleTheme.Light)
                        ? new SolidColorBrush(Color.FromRgb(100, 100, 108))
                        : new SolidColorBrush(Color.FromRgb(130, 130, 135)),
                    FontSize = 11.0,
                    FontFamily = new FontFamily("Segoe UI"),
                    Margin = new Thickness(0, 2, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                textStack.Children.Add(capMeta);

                Grid.SetColumn(textStack, 1);
                capGrid.Children.Add(textStack);

                var copyBadge = new TextBlock
                {
                    Text = "Click to Copy",
                    Foreground = (_currentTheme == ConsoleTheme.Light)
                        ? new SolidColorBrush(Color.FromRgb(0, 120, 215))
                        : new SolidColorBrush(Color.FromRgb(0, 168, 255)),
                    FontSize = 11.0,
                    FontFamily = new FontFamily("Segoe UI"),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                };
                Grid.SetColumn(copyBadge, 2);
                capGrid.Children.Add(copyBadge);

                capBorder.Child = capGrid;

                var capRowObj = new DisplayRow { IsHeader = false, Item = item, Container = capBorder, Label = capTitle, ExtraLabel = capMeta };

                capBorder.MouseEnter += delegate { SetSelectedIndex(rowIndex); };
                capBorder.MouseDown += delegate { ApplySelectedItem(capRowObj.Item); };

                _resultsStack.Children.Add(capBorder);
                _currentRows.Add(capRowObj);
                return;
            }

            var rowBorder = new Border
            {
                Height = 27,
                Margin = new Thickness(4, 1, 4, 1),
                CornerRadius = new CornerRadius(3),
                Background = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = item.Name,
                Foreground = (_currentTheme == ConsoleTheme.Light)
                    ? new SolidColorBrush(Color.FromRgb(28, 28, 30))
                    : new SolidColorBrush(Color.FromRgb(225, 225, 225)),
                FontSize = 13.0,
                FontFamily = new FontFamily("Segoe UI"),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(22, 0, 10, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(label, 0);
            rowGrid.Children.Add(label);

            TextBlock extraLabel = null;
            if (!string.IsNullOrEmpty(item.Extra))
            {
                extraLabel = new TextBlock
                {
                    Text = item.Extra,
                    Foreground = (_currentTheme == ConsoleTheme.Light)
                        ? new SolidColorBrush(Color.FromRgb(100, 100, 108))
                        : new SolidColorBrush(Color.FromRgb(125, 125, 130)),
                    FontSize = 11.0,
                    FontFamily = new FontFamily("Segoe UI"),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                };
                Grid.SetColumn(extraLabel, 1);
                rowGrid.Children.Add(extraLabel);
            }

            rowBorder.Child = rowGrid;

            var rowObj = new DisplayRow { IsHeader = false, Item = item, Container = rowBorder, Label = label, ExtraLabel = extraLabel };

            rowBorder.MouseEnter += delegate { SetSelectedIndex(rowIndex); };
            rowBorder.MouseDown += delegate { ApplySelectedItem(rowObj.Item); };

            _resultsStack.Children.Add(rowBorder);
            _currentRows.Add(rowObj);
        }

        private void SetSelectedIndex(int index)
        {
            if (_selectedIndex >= 0 && _selectedIndex < _currentRows.Count)
            {
                var oldRow = _currentRows[_selectedIndex];
                if (!oldRow.IsHeader && oldRow.Container != null)
                {
                    oldRow.Container.Background = Brushes.Transparent;
                    oldRow.Label.Foreground = (_currentTheme == ConsoleTheme.Light)
                        ? new SolidColorBrush(Color.FromRgb(28, 28, 30))
                        : new SolidColorBrush(Color.FromRgb(225, 225, 225));
                    oldRow.Label.FontWeight = FontWeights.Normal;
                    if (oldRow.ExtraLabel != null)
                    {
                        oldRow.ExtraLabel.Foreground = (_currentTheme == ConsoleTheme.Light)
                            ? new SolidColorBrush(Color.FromRgb(100, 100, 108))
                            : new SolidColorBrush(Color.FromRgb(125, 125, 130));
                    }
                }
            }

            _selectedIndex = index;

            if (_selectedIndex >= 0 && _selectedIndex < _currentRows.Count)
            {
                var newRow = _currentRows[_selectedIndex];
                if (!newRow.IsHeader && newRow.Container != null)
                {
                    newRow.Container.Background = (_currentTheme == ConsoleTheme.Light)
                        ? new SolidColorBrush(Color.FromRgb(0, 120, 215))
                        : new SolidColorBrush(Color.FromRgb(0, 122, 204));
                    newRow.Label.Foreground = Brushes.White;
                    newRow.Label.FontWeight = FontWeights.SemiBold;
                    if (newRow.ExtraLabel != null)
                    {
                        newRow.ExtraLabel.Foreground = new SolidColorBrush(Color.FromRgb(225, 240, 255));
                    }
                    newRow.Container.BringIntoView();
                }
            }
        }

        private void OnSearchPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideConsole();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Down)
            {
                MoveSelection(1);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up)
            {
                MoveSelection(-1);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter)
            {
                if (_selectedIndex >= 0 && _selectedIndex < _currentRows.Count)
                {
                    var row = _currentRows[_selectedIndex];
                    if (!row.IsHeader && row.Item != null)
                    {
                        ApplySelectedItem(row.Item);
                        e.Handled = true;
                    }
                }
            }
        }

        private void MoveSelection(int delta)
        {
            if (_currentRows.Count == 0) return;
            int next = _selectedIndex + delta;
            while (next >= 0 && next < _currentRows.Count)
            {
                if (!_currentRows[next].IsHeader)
                {
                    SetSelectedIndex(next);
                    return;
                }
                next += delta;
            }
        }

        private void ApplySelectedItem(EffectItem item)
        {
            if (item == null) return;

            if (item.Kind == ConsoleItemKind.Filter)
            {
                _searchBox.Text = item.Id + " ";
                _searchBox.CaretIndex = _searchBox.Text.Length;
                _searchBox.Focus();
                Keyboard.Focus(_searchBox);
                return;
            }

            if (item.Kind == ConsoleItemKind.Capture)
            {
                if (item.Capture != null && File.Exists(item.Capture.ImagePath))
                {
                    Dispatcher.Invoke(new Action(delegate {
                        try
                        {
                            using (var bmp = new System.Drawing.Bitmap(item.Capture.ImagePath))
                            {
                                System.Windows.Forms.Clipboard.SetImage(bmp);
                            }
                            ShowToast("Capture copied to clipboard!");
                            ThreadPool.QueueUserWorkItem(delegate {
                                Thread.Sleep(800);
                                Dispatcher.Invoke(new Action(delegate { HideConsole(); }));
                            });
                        }
                        catch (Exception ex)
                        {
                            ShowToast("Error copying capture: " + ex.Message);
                        }
                    }));
                }
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate {
                string json;
                if (item.Kind == ConsoleItemKind.Preset)
                {
                    Program.Log("Applying animation preset: " + item.Name + " (" + item.Id + ")");
                    json = "{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"layer.applyTextPreset\",\"params\":{\"preset\":\"" + item.Id + "\"}}}";
                }
                else if (item.Kind == ConsoleItemKind.Layer)
                {
                    Program.Log("Creating layer: " + item.Name + " (" + item.Id + ")");
                    if (item.Id == "layer.newSolid")
                    {
                        json = "{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"layer.newSolid\",\"params\":{\"color\":\"#ffffff\"}}}";
                    }
                    else
                    {
                        json = "{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"" + item.Id + "\",\"params\":{}}}";
                    }
                }
                else if (item.Kind == ConsoleItemKind.Command)
                {
                    Program.Log("Executing command: " + item.Name + " (" + item.Id + ")");
                    json = "{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"" + item.Id + "\",\"params\":{}}}";
                }
                else if (item.Kind == ConsoleItemKind.Project)
                {
                    Program.Log("Opening recent project: " + item.Id);
                    json = "{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"file.open\",\"params\":{\"path\":\"" + item.Id.Replace("\\", "\\\\") + "\"}}}";
                }
                else
                {
                    Program.Log("Applying effect: " + item.Name + " (" + item.Id + ")");
                    json = "{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"effect.apply\",\"params\":{\"effect\":\"" + item.Name + "\"}}}";
                }
                string res = Program.SendTcpCommand(json);
                Program.Log("Apply result: " + res);

                if (!string.IsNullOrEmpty(res) && res.IndexOf("no layer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Dispatcher.Invoke(new Action(delegate {
                        _statusText.Text = "⚠️ No layer selected in EffectCraft";
                        _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 74));
                        _statusText.Visibility = Visibility.Visible;
                    }));
                    return;
                }

                if (!string.IsNullOrEmpty(res) && (res.IndexOf("no comp", StringComparison.OrdinalIgnoreCase) >= 0 || res.IndexOf("no active comp", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    Dispatcher.Invoke(new Action(delegate {
                        _statusText.Text = "⚠️ No active composition in EffectCraft";
                        _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 184, 0));
                        _statusText.Visibility = Visibility.Visible;
                    }));
                    return;
                }

                Dispatcher.Invoke(new Action(delegate {
                    HideConsole();
                }));
            });
        }

        private void TakeSnapshot()
        {
            Program.Log("Taking snapshot...");
            ShowToast("Capturing snapshot...");

            ThreadPool.QueueUserWorkItem(delegate {
                string json = "{\"id\":20,\"method\":\"render.frame\",\"params\":{\"max_side\":0}}";
                string res = Program.SendTcpCommand(json);

                if (!string.IsNullOrEmpty(res) && res.Contains("\"path\":\""))
                {
                    try
                    {
                        int p1 = res.IndexOf("\"path\":\"") + 8;
                        int p2 = res.IndexOf("\"", p1);
                        string imgPath = res.Substring(p1, p2 - p1).Replace("\\\\", "\\");

                        if (File.Exists(imgPath))
                        {
                            int w = ExtractInt(res, "\"width\":");
                            int h = ExtractInt(res, "\"height\":");
                            double timeSec = ExtractDouble(res, "\"time\":");
                            string timecode = TimeSpan.FromSeconds(Math.Max(0, timeSec)).ToString(@"mm\:ss\.ff");

                            string compName = "Active Comp";
                            string projName = "Untitled";

                            try
                            {
                                string compRes = Program.SendTcpCommand("{\"id\":22,\"method\":\"engine.execute\",\"params\":{\"command\":\"comp.info\"}}");
                                if (!string.IsNullOrEmpty(compRes) && compRes.Contains("\"name\":\""))
                                {
                                    int cp1 = compRes.IndexOf("\"name\":\"") + 8;
                                    int cp2 = compRes.IndexOf("\"", cp1);
                                    if (cp2 > cp1) compName = compRes.Substring(cp1, cp2 - cp1);
                                }

                                string projRes = Program.SendTcpCommand("{\"id\":23,\"method\":\"engine.execute\",\"params\":{\"command\":\"project.summary\"}}");
                                if (!string.IsNullOrEmpty(projRes) && projRes.Contains("\"path\":\""))
                                {
                                    int pp1 = projRes.IndexOf("\"path\":\"") + 8;
                                    int pp2 = projRes.IndexOf("\"", pp1);
                                    if (pp2 > pp1)
                                    {
                                        string pPath = projRes.Substring(pp1, pp2 - pp1).Replace("\\\\", "\\");
                                        if (!string.IsNullOrEmpty(pPath)) projName = Path.GetFileName(pPath);
                                    }
                                }
                            }
                            catch { }

                            RecordSessionCapture(imgPath, compName, projName, timecode, w, h);

                            Dispatcher.Invoke(new Action(delegate {
                                try
                                {
                                    using (var bmp = new System.Drawing.Bitmap(imgPath))
                                    {
                                        System.Windows.Forms.Clipboard.SetImage(bmp);
                                    }
                                    ShowToast("Snapshot copied to clipboard!");
                                    ThreadPool.QueueUserWorkItem(delegate {
                                        Thread.Sleep(900);
                                        Dispatcher.Invoke(new Action(delegate { HideConsole(); }));
                                    });
                                }
                                catch (Exception ex)
                                {
                                    ShowToast("Error copying image: " + ex.Message);
                                }
                            }));
                            return;
                        }
                    }
                    catch { }
                }

                Dispatcher.Invoke(new Action(delegate {
                    _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 74));
                    if (string.IsNullOrEmpty(res))
                    {
                        ShowToast("⚠️ Could not connect to EffectCraft");
                    }
                    else if (res.Contains("no active") || res.Contains("no composition") || res.Contains("error"))
                    {
                        ShowToast("⚠️ No active composition in EffectCraft");
                    }
                    else
                    {
                        ShowToast("⚠️ Snapshot failed - check EffectCraft");
                    }
                }));
            });
        }

        private void SaveFramePng()
        {
            ShowToast("Exporting PNG...");
            ThreadPool.QueueUserWorkItem(delegate {
                string json = "{\"id\":21,\"method\":\"render.frame\",\"params\":{\"max_side\":0}}";
                string res = Program.SendTcpCommand(json);

                if (!string.IsNullOrEmpty(res) && res.Contains("\"path\":\""))
                {
                    try
                    {
                        int p1 = res.IndexOf("\"path\":\"") + 8;
                        int p2 = res.IndexOf("\"", p1);
                        string imgPath = res.Substring(p1, p2 - p1).Replace("\\\\", "\\");

                        if (File.Exists(imgPath))
                        {
                            int w = ExtractInt(res, "\"width\":");
                            int h = ExtractInt(res, "\"height\":");
                            double timeSec = ExtractDouble(res, "\"time\":");
                            string timecode = TimeSpan.FromSeconds(Math.Max(0, timeSec)).ToString(@"mm\:ss\.ff");

                            string compName = "Active Comp";
                            string projName = "Untitled";

                            try
                            {
                                string compRes = Program.SendTcpCommand("{\"id\":22,\"method\":\"engine.execute\",\"params\":{\"command\":\"comp.info\"}}");
                                if (!string.IsNullOrEmpty(compRes) && compRes.Contains("\"name\":\""))
                                {
                                    int cp1 = compRes.IndexOf("\"name\":\"") + 8;
                                    int cp2 = compRes.IndexOf("\"", cp1);
                                    if (cp2 > cp1) compName = compRes.Substring(cp1, cp2 - cp1);
                                }

                                string projRes = Program.SendTcpCommand("{\"id\":23,\"method\":\"engine.execute\",\"params\":{\"command\":\"project.summary\"}}");
                                if (!string.IsNullOrEmpty(projRes) && projRes.Contains("\"path\":\""))
                                {
                                    int pp1 = projRes.IndexOf("\"path\":\"") + 8;
                                    int pp2 = projRes.IndexOf("\"", pp1);
                                    if (pp2 > pp1)
                                    {
                                        string pPath = projRes.Substring(pp1, pp2 - pp1).Replace("\\\\", "\\");
                                        if (!string.IsNullOrEmpty(pPath)) projName = Path.GetFileName(pPath);
                                    }
                                }
                            }
                            catch { }

                            RecordSessionCapture(imgPath, compName, projName, timecode, w, h);

                            string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "EffectCraft_Frame_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                            try
                            {
                                File.Copy(imgPath, desktopPath, true);
                                Dispatcher.Invoke(new Action(delegate {
                                    ShowToast("Saved to Desktop: " + Path.GetFileName(desktopPath));
                                }));
                                return;
                            }
                            catch
                            {
                                Dispatcher.Invoke(new Action(delegate {
                                    ShowToast("Frame ready at: " + imgPath);
                                }));
                                return;
                            }
                        }
                    }
                    catch { }
                }

                Dispatcher.Invoke(new Action(delegate {
                    _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 74));
                    if (string.IsNullOrEmpty(res))
                    {
                        ShowToast("⚠️ Could not connect to EffectCraft");
                    }
                    else if (res.Contains("no active") || res.Contains("no composition") || res.Contains("error"))
                    {
                        ShowToast("⚠️ No active composition in EffectCraft");
                    }
                    else
                    {
                        ShowToast("⚠️ Frame export failed");
                    }
                }));
            });
        }

        private static int ExtractInt(string json, string key)
        {
            try
            {
                int p = json.IndexOf(key);
                if (p < 0) return 0;
                p += key.Length;
                while (p < json.Length && char.IsWhiteSpace(json[p])) p++;
                int end = p;
                while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
                if (end > p) return int.Parse(json.Substring(p, end - p));
            }
            catch { }
            return 0;
        }

        private static double ExtractDouble(string json, string key)
        {
            try
            {
                int p = json.IndexOf(key);
                if (p < 0) return 0;
                p += key.Length;
                while (p < json.Length && char.IsWhiteSpace(json[p])) p++;
                int end = p;
                while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-')) end++;
                if (end > p) return double.Parse(json.Substring(p, end - p), System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { }
            return 0;
        }

        private static void RecordSessionCapture(string path, string comp, string proj, string tc, int w, int h)
        {
            try
            {
                BitmapSource thumb = null;
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path);
                    bmp.DecodePixelWidth = 140;
                    bmp.EndInit();
                    bmp.Freeze();
                    thumb = bmp;
                }
                catch { }

                var item = new CaptureItem
                {
                    ImagePath = path,
                    CompName = string.IsNullOrEmpty(comp) ? "Composition" : comp,
                    ProjectName = string.IsNullOrEmpty(proj) ? "Untitled" : proj,
                    Timecode = tc,
                    CaptureTime = DateTime.Now,
                    Width = w,
                    Height = h,
                    Thumbnail = thumb
                };

                lock (_sessionCapturesLock)
                {
                    _sessionCaptures.Insert(0, item);
                    if (_sessionCaptures.Count > 20)
                    {
                        var oldest = _sessionCaptures[_sessionCaptures.Count - 1];
                        _sessionCaptures.RemoveAt(_sessionCaptures.Count - 1);
                        try
                        {
                            if (oldest.ImagePath != null && oldest.ImagePath.Contains(Path.GetTempPath()) && File.Exists(oldest.ImagePath))
                            {
                                File.Delete(oldest.ImagePath);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public static void PurgeSessionCaptures()
        {
            lock (_sessionCapturesLock)
            {
                foreach (var c in _sessionCaptures)
                {
                    try
                    {
                        if (c.ImagePath != null && c.ImagePath.Contains(Path.GetTempPath()) && File.Exists(c.ImagePath))
                        {
                            File.Delete(c.ImagePath);
                        }
                    }
                    catch { }
                }
                _sessionCaptures.Clear();
            }
        }

        private void ShowToast(string msg)
        {
            _statusText.Text = msg;
            _statusText.Visibility = Visibility.Visible;
        }

        private void InitializeCatalog()
        {
            AddEff("3D Channel Extract", "ec.3d.channelextract", "3D Channel");
            AddEff("Cryptomatte", "ec.3d.cryptomatte", "3D Channel");
            AddEff("Depth Matte", "ec.3d.depthmatte", "3D Channel");
            AddEff("Depth of Field", "ec.3d.depthoffield", "3D Channel");
            AddEff("EXtractoR", "ec.3d.extractor", "3D Channel");
            AddEff("Fog 3D", "ec.3d.fog3d", "3D Channel");
            AddEff("ID Matte", "ec.3d.idmatte", "3D Channel");
            AddEff("IDentifier", "ec.3d.identifier", "3D Channel");

            AddEff("Backwards", "ec.audio.backwards", "Audio");
            AddEff("Bass & Treble", "ec.audio.basstreble", "Audio");
            AddEff("Compressor", "ec.audio.compressor", "Audio");
            AddEff("Delay", "ec.audio.delay", "Audio");
            AddEff("Distortion", "ec.audio.distortion", "Audio");
            AddEff("Flange & Chorus", "ec.audio.flangechorus", "Audio");
            AddEff("Gate", "ec.audio.gate", "Audio");
            AddEff("High-Low Pass", "ec.audio.highlowpass", "Audio");
            AddEff("Modulator", "ec.audio.modulator", "Audio");
            AddEff("Parametric EQ", "ec.audio.parametriceq", "Audio");
            AddEff("Reverb", "ec.audio.reverb", "Audio");
            AddEff("Stereo Mixer", "ec.audio.stereomixer", "Audio");
            AddEff("Tone", "ec.audio.tone", "Audio");

            AddEff("Bilateral Blur", "ec.blur.bilateral", "Blur & Sharpen");
            AddEff("CC Cross Blur", "ec.blur.cccross", "Blur & Sharpen");
            AddEff("CC Radial Blur", "ec.blur.ccradial", "Blur & Sharpen");
            AddEff("CC Radial Fast Blur", "ec.blur.ccradialfast", "Blur & Sharpen");
            AddEff("CC Vector Blur", "ec.blur.ccvector", "Blur & Sharpen");
            AddEff("Camera Lens Blur", "ec.blur.cameralens", "Blur & Sharpen");
            AddEff("Camera-Shake Deblur", "ec.blur.camerashakedeblur", "Blur & Sharpen");
            AddEff("Channel Blur", "ec.blur.channel", "Blur & Sharpen");
            AddEff("Compound Blur", "ec.blur.compound", "Blur & Sharpen");
            AddEff("Directional Blur", "ec.blur.directional", "Blur & Sharpen");
            AddEff("Fast Box Blur", "ec.blur.fastbox", "Blur & Sharpen");
            AddEff("Gaussian Blur", "ec.blur.gaussian", "Blur & Sharpen");
            AddEff("Radial Blur", "ec.blur.radial", "Blur & Sharpen");
            AddEff("Sharpen", "ec.blur.sharpen", "Blur & Sharpen");
            AddEff("Smart Blur", "ec.blur.smart", "Blur & Sharpen");
            AddEff("Unsharp Mask", "ec.blur.unsharp", "Blur & Sharpen");

            AddEff("Arithmetic", "ec.channel.arithmetic", "Channel");
            AddEff("Blend", "ec.channel.blend", "Channel");
            AddEff("CC Composite", "ec.channel.cccomposite", "Channel");
            AddEff("Calculations", "ec.channel.calculations", "Channel");
            AddEff("Channel Combiner", "ec.channel.combiner", "Channel");
            AddEff("Compound Arithmetic", "ec.channel.compoundarithmetic", "Channel");
            AddEff("Invert", "ec.channel.invert", "Channel");
            AddEff("Minimax", "ec.channel.minimax", "Channel");
            AddEff("Remove Color Matting", "ec.channel.removecolormatting", "Channel");
            AddEff("Set Channels", "ec.channel.setchannels", "Channel");
            AddEff("Set Matte", "ec.channel.setmatte", "Channel");
            AddEff("Shift Channels", "ec.channel.shiftchannels", "Channel");
            AddEff("Solid Composite", "ec.channel.solidcomposite", "Channel");

            AddEff("Auto Color", "ec.color.autocolor", "Color Correction");
            AddEff("Auto Contrast", "ec.color.autocontrast", "Color Correction");
            AddEff("Auto Levels", "ec.color.autolevels", "Color Correction");
            AddEff("Black & White", "ec.color.blackwhite", "Color Correction");
            AddEff("Brightness & Contrast", "ec.color.brightnesscontrast", "Color Correction");
            AddEff("Broadcast Colors", "ec.color.broadcast", "Color Correction");
            AddEff("CC Color Neutralizer", "ec.color.cccolorneutralizer", "Color Correction");
            AddEff("CC Color Offset", "ec.color.cccoloroffset", "Color Correction");
            AddEff("CC Kernel", "ec.color.cckernel", "Color Correction");
            AddEff("CC Toner", "ec.color.cctoner", "Color Correction");
            AddEff("Change Color", "ec.color.changecolor", "Color Correction");
            AddEff("Change to Color", "ec.color.changetocolor", "Color Correction");
            AddEff("Channel Mixer", "ec.color.channelmixer", "Color Correction");
            AddEff("Color Balance", "ec.color.colorbalance", "Color Correction");
            AddEff("Color Balance (HLS)", "ec.color.colorbalancehls", "Color Correction");
            AddEff("Color Link", "ec.color.colorlink", "Color Correction");
            AddEff("Color Stabilizer", "ec.color.colorstabilizer", "Color Correction");
            AddEff("Colorama", "ec.color.colorama", "Color Correction");
            AddEff("Curves", "ec.color.curves", "Color Correction");
            AddEff("Equalize", "ec.color.equalize", "Color Correction");
            AddEff("Exposure", "ec.color.exposure", "Color Correction");
            AddEff("Glow", "ec.stylize.glow", "Stylize");
            AddEff("Drop Shadow", "ec.perspective.dropshadow", "Perspective");
            AddEff("Hue/Saturation", "ec.color.huesaturation", "Color Correction");
            AddEff("Levels", "ec.color.levels", "Color Correction");
            AddEff("Levels (Individual Controls)", "ec.color.levelsindiv", "Color Correction");
            AddEff("Lumetri Color", "ec.color.lumetri", "Color Correction");
            AddEff("Photo Filter", "ec.color.photofilter", "Color Correction");
            AddEff("Selective Color", "ec.color.selectivecolor", "Color Correction");
            AddEff("Shadow/Highlight", "ec.color.shadowhighlight", "Color Correction");
            AddEff("Tint", "ec.color.tint", "Color Correction");
            AddEff("Tritone", "ec.color.tritone", "Color Correction");
            AddEff("Vibrance", "ec.color.vibrance", "Color Correction");

            AddEff("Bezier Warp", "ec.distort.bezierwarp", "Distort");
            AddEff("Bulge", "ec.distort.bulge", "Distort");
            AddEff("Corner Pin", "ec.distort.cornerpin", "Distort");
            AddEff("Displacement Map", "ec.distort.displacementmap", "Distort");
            AddEff("Liquify", "ec.distort.liquify", "Distort");
            AddEff("Magnify", "ec.distort.magnify", "Distort");
            AddEff("Mesh Warp", "ec.distort.meshwarp", "Distort");
            AddEff("Mirror", "ec.distort.mirror", "Distort");
            AddEff("Offset", "ec.distort.offset", "Distort");
            AddEff("Optics Compensation", "ec.distort.opticscompensation", "Distort");
            AddEff("Polar Coordinates", "ec.distort.polarcoordinates", "Distort");
            AddEff("Reshape", "ec.distort.reshape", "Distort");
            AddEff("Ripple", "ec.distort.ripple", "Distort");
            AddEff("Spherize", "ec.distort.spherize", "Distort");
            AddEff("Transform", "ec.distort.transform", "Distort");
            AddEff("Turbulent Displace", "ec.distort.turbulentdisplace", "Distort");
            AddEff("Twirl", "ec.distort.twirl", "Distort");
            AddEff("Warp", "ec.distort.warp", "Distort");
            AddEff("Wave Warp", "ec.distort.wavewarp", "Distort");

            AddEff("Advanced Lightning", "ec.generate.advancedlightning", "Generate");
            AddEff("Audio Spectrum", "ec.generate.audiospectrum", "Generate");
            AddEff("Audio Waveform", "ec.generate.audiowaveform", "Generate");
            AddEff("Beam", "ec.generate.beam", "Generate");
            AddEff("Cell Pattern", "ec.generate.cellpattern", "Generate");
            AddEff("Checkerboard", "ec.generate.checkerboard", "Generate");
            AddEff("Circle", "ec.generate.circle", "Generate");
            AddEff("Ellipse", "ec.generate.ellipse", "Generate");
            AddEff("Eyedropper Fill", "ec.generate.eyedropperfill", "Generate");
            AddEff("Fill", "ec.generate.fill", "Generate");
            AddEff("Fractal", "ec.generate.fractal", "Generate");
            AddEff("Gradient Ramp", "ec.generate.gradientramp", "Generate");
            AddEff("Grid", "ec.generate.grid", "Generate");
            AddEff("Lens Flare", "ec.generate.lensflare", "Generate");
            AddEff("Paint Bucket", "ec.generate.paintbucket", "Generate");
            AddEff("Radio Waves", "ec.generate.radiowaves", "Generate");
            AddEff("Stroke", "ec.generate.stroke", "Generate");
            AddEff("Vegas", "ec.generate.vegas", "Generate");
            AddEff("Write-on", "ec.generate.writeon", "Generate");

            AddEff("Linear Color Key", "ec.keying.linearcolorkey", "Keying");
            AddEff("Color Difference Key", "ec.keying.colordifferencekey", "Keying");
            AddEff("Color Range", "ec.keying.colorrange", "Keying");
            AddEff("Difference Matte", "ec.keying.differencematte", "Keying");
            AddEff("Extract", "ec.keying.extract", "Keying");
            AddEff("Key Cleaner", "ec.keying.keycleaner", "Keying");
            AddEff("Key Light", "ec.keying.keylight", "Keying");
            AddEff("Spill Suppressor", "ec.keying.spillsuppressor", "Keying");

            AddEff("Motion Tile", "ec.stylize.motiontile", "Stylize");
            AddEff("Posterize", "ec.stylize.posterize", "Stylize");
            AddEff("Rough Edges", "ec.stylize.roughedges", "Stylize");
            AddEff("Scatter", "ec.stylize.scatter", "Stylize");
            AddEff("Strobe Light", "ec.stylize.strobelight", "Stylize");
            AddEff("Threshold", "ec.stylize.threshold", "Stylize");

            AddPre("Typewriter", "typewriter", "Text Animation");
            AddPre("Fade Up Characters", "fadeUpCharacters", "Text Animation");
            AddPre("Bounce In Words", "bounceInWords", "Text Animation");
            AddPre("Tracking In", "trackingIn", "Text Animation");
            AddPre("Scramble", "scramble", "Text Animation");
            AddPre("Blur In", "blurIn", "Text Animation");
            AddPre("Jitter", "jitter", "Text Animation");
            AddPre("Drop In Lines", "dropInLines", "Text Animation");

            // Quick Layer Generators
            AddLayer("New Solid...", "layer.newSolid", "Layer", "Ctrl+Y");
            AddLayer("New Null Object", "layer.newNull", "Layer", "Ctrl+Alt+Shift+Y");
            AddLayer("New Adjustment Layer", "layer.newAdjustment", "Layer", "Ctrl+Alt+Y");
            AddLayer("New Text Layer", "layer.newText", "Layer", "Ctrl+Alt+Shift+T");
            AddLayer("New Shape Layer", "layer.newShape", "Layer");
            AddLayer("New Camera...", "layer.newCamera", "Layer", "Ctrl+Alt+Shift+C");
            AddLayer("New Light...", "layer.newLight", "Layer", "Ctrl+Alt+Shift+L");

            // Built-in Command Palette Actions
            AddCommand("New Composition...", "comp.new", "Composition", "Ctrl+N");
            AddCommand("Composition Settings...", "comp.settings", "Composition", "Ctrl+K");
            AddCommand("Duplicate", "edit.duplicate", "Edit", "Ctrl+D");
            AddCommand("Split Layer", "edit.splitLayer", "Edit", "Ctrl+Shift+D");
            AddCommand("Pre-compose...", "layer.precompose", "Layer", "Ctrl+Shift+C");
            AddCommand("Easy Ease", "keys.easyEase", "Animation", "F9");
            AddCommand("Easy Ease In", "keys.easyEaseIn", "Animation", "Shift+F9");
            AddCommand("Easy Ease Out", "keys.easyEaseOut", "Animation", "Ctrl+Shift+F9");
            AddCommand("Trim Comp to Work Area", "comp.trimWorkArea", "Composition", "Ctrl+Shift+X");
            AddCommand("Add to Render Queue", "renderQueue.add", "Composition", "Ctrl+M");
            AddCommand("Purge All Memory & Disk Cache", "edit.purge", "Edit");
            AddCommand("Preferences...", "app.settings", "Edit", "Ctrl+Alt+;");
        }

        private void AddEff(string name, string id, string cat)
        {
            _catalog.Add(new EffectItem { Name = name, Id = id, Category = cat, IsPreset = false, Kind = ConsoleItemKind.Effect });
        }

        private void AddPre(string name, string id, string cat)
        {
            _catalog.Add(new EffectItem { Name = name, Id = id, Category = cat, IsPreset = true, Kind = ConsoleItemKind.Preset });
        }

        private void AddLayer(string name, string id, string cat, string shortcut = null)
        {
            _catalog.Add(new EffectItem { Name = name, Id = id, Category = cat, IsPreset = false, Kind = ConsoleItemKind.Layer, Extra = shortcut });
        }

        private void AddCommand(string name, string id, string cat, string shortcut = null)
        {
            _catalog.Add(new EffectItem { Name = name, Id = id, Category = cat, IsPreset = false, Kind = ConsoleItemKind.Command, Extra = shortcut });
        }

        private void SyncLiveCommandsFromEngine()
        {
            ThreadPool.QueueUserWorkItem(delegate {
                try
                {
                    string json = "{\"id\":5,\"method\":\"ui.menu.list\",\"params\":{}}";
                    string res = Program.SendTcpCommand(json);
                    if (string.IsNullOrEmpty(res) || !res.Contains("\"result\":")) return;

                    var discovered = ParseMenuItems(res);
                    if (discovered != null && discovered.Count > 0)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate {
                            foreach (var newItem in discovered)
                            {
                                int existingIdx = _catalog.FindIndex(x => x.Id == newItem.Id);
                                if (existingIdx >= 0)
                                {
                                    _catalog[existingIdx].Name = newItem.Name;
                                    if (!string.IsNullOrEmpty(newItem.Extra))
                                        _catalog[existingIdx].Extra = newItem.Extra;
                                }
                                else
                                {
                                    _catalog.Add(newItem);
                                }
                            }
                            _commandsSynced = true;
                            Program.Log("Synced " + discovered.Count + " live commands from EffectCraft menu palette");
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Program.Log("SyncLiveCommands error: " + ex.Message);
                }
            });
        }

        private List<EffectItem> ParseMenuItems(string res)
        {
            var list = new List<EffectItem>();
            try
            {
                int arrStart = res.IndexOf("\"result\":[");
                if (arrStart < 0) arrStart = res.IndexOf("\"result\": [");
                if (arrStart < 0) return list;
                arrStart = res.IndexOf('[', arrStart);
                if (arrStart < 0) return list;

                int arrEnd = res.LastIndexOf(']');
                if (arrEnd <= arrStart) return list;

                int depth = 0;
                int objStart = -1;
                bool inString = false;
                bool escape = false;

                for (int i = arrStart; i <= arrEnd; i++)
                {
                    char c = res[i];
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == '"') { inString = !inString; continue; }
                    if (inString) continue;

                    if (c == '{')
                    {
                        if (depth == 0) objStart = i;
                        depth++;
                    }
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0 && objStart >= 0)
                        {
                            string obj = res.Substring(objStart, i - objStart + 1);
                            objStart = -1;

                            string id = ExtractJsonString(obj, "id");
                            string label = ExtractJsonString(obj, "label");
                            string shortcut = ExtractJsonString(obj, "shortcut");
                            List<string> path = ExtractJsonArray(obj, "path");

                            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(label)) continue;
                            if (label.StartsWith("---") || label == "-" || id == "edit.separator") continue;
                            if (id.StartsWith("file.recent") || label.Contains("Recent Projects")) continue;

                            ConsoleItemKind kind = ConsoleItemKind.Command;
                            string category = (path.Count > 0) ? path[0] : "Command";

                            if (id.StartsWith("layer.new") || (path.Count > 0 && path[0] == "Layer" && (path.Contains("New") || label.StartsWith("New "))))
                            {
                                kind = ConsoleItemKind.Layer;
                                category = "Layer";
                            }
                            else if (id.StartsWith("layer.applyTextPreset") || id.StartsWith("preset."))
                            {
                                kind = ConsoleItemKind.Preset;
                                category = "Text Animation";
                            }
                            else if (id.StartsWith("effect.apply") || (path.Count > 0 && path[0] == "Effect"))
                            {
                                kind = ConsoleItemKind.Effect;
                                category = (path.Count > 1) ? path[1] : "Effects";
                            }
                            else
                            {
                                kind = ConsoleItemKind.Command;
                            }

                            string extra = shortcut;
                            if (string.IsNullOrEmpty(extra) && path.Count > 1)
                            {
                                extra = string.Join(" ▸ ", path.ToArray());
                            }

                            list.Add(new EffectItem {
                                Name = label,
                                Id = id,
                                Category = category,
                                Kind = kind,
                                Extra = extra
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log("ParseMenuItems error: " + ex.Message);
            }
            return list;
        }

        private static string ExtractJsonString(string obj, string key)
        {
            string pattern = "\"" + key + "\":\"";
            int idx = obj.IndexOf(pattern);
            if (idx < 0) return null;
            int start = idx + pattern.Length;
            var sb = new StringBuilder();
            for (int i = start; i < obj.Length; i++)
            {
                char c = obj[i];
                if (c == '\\')
                {
                    if (i + 1 < obj.Length) { sb.Append(obj[++i]); }
                }
                else if (c == '"')
                {
                    break;
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static List<string> ExtractJsonArray(string obj, string key)
        {
            var list = new List<string>();
            string pattern = "\"" + key + "\":[";
            int idx = obj.IndexOf(pattern);
            if (idx < 0)
            {
                pattern = "\"" + key + "\": [";
                idx = obj.IndexOf(pattern);
            }
            if (idx < 0) return list;
            int start = idx + pattern.Length;
            int end = obj.IndexOf(']', start);
            if (end < 0) return list;
            string sub = obj.Substring(start, end - start);
            string[] parts = sub.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in parts)
            {
                string s = p.Trim().Trim('"', ' ');
                if (!string.IsNullOrEmpty(s)) list.Add(s);
            }
            return list;
        }
    }
    public class FirstLaunchSetupWindow : Window
    {
        private TextBox _txtExePath;
        private TextBlock _lblStatus;
        private System.Windows.Controls.CheckBox _chkDesktopShortcut;
        private System.Windows.Controls.CheckBox _chkStartMenuShortcut;
        private System.Windows.Controls.CheckBox _chkInstallSplash;

        public FirstLaunchSetupWindow(string initialPath)
        {
            this.Title = "Craft Console — Initial Setup & Link";
            this.Width = 620;
            this.Height = 490;
            this.MinWidth = 560;
            this.MinHeight = 460;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Background = new SolidColorBrush(Color.FromRgb(20, 20, 24));
            this.Foreground = Brushes.White;
            this.FontFamily = new FontFamily("Segoe UI");

            var rootGrid = new Grid { Margin = new Thickness(24) };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var headerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            var topBrandRow = new StackPanel { Orientation = Orientation.Horizontal };

            var badgeBorder = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(15, 35, 56)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 210, 255)),
                BorderThickness = new Thickness(1.5),
                VerticalAlignment = VerticalAlignment.Center
            };
            var badgeMark = new WpfPath
            {
                Data = Geometry.Parse(Program.ARTCRAFT_MARK_PATH),
                Fill = new SolidColorBrush(Color.FromRgb(0, 210, 255)),
                Width = 20,
                Height = 20,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            badgeBorder.Child = badgeMark;
            topBrandRow.Children.Add(badgeBorder);

            var titleStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var titleText = new TextBlock
            {
                Text = "Craft Console Setup",
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            titleStack.Children.Add(titleText);
            var subtitleText = new TextBlock
            {
                Text = "Companion HUD, Splash Launcher & Supercharged Workflow for EffectCraft",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 160)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            titleStack.Children.Add(subtitleText);
            topBrandRow.Children.Add(titleStack);
            headerStack.Children.Add(topBrandRow);

            var sep1 = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                Margin = new Thickness(0, 14, 0, 0)
            };
            headerStack.Children.Add(sep1);
            Grid.SetRow(headerStack, 0);
            rootGrid.Children.Add(headerStack);

            // Body
            var bodyStack = new StackPanel { VerticalAlignment = VerticalAlignment.Top };

            var pathDesc = new TextBlock
            {
                Text = "Select your EffectCraft installation folder or effectcraft.exe executable:",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 230)),
                Margin = new Thickness(0, 4, 0, 8)
            };
            bodyStack.Children.Add(pathDesc);

            var pathRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtExePath = new TextBox
            {
                Height = 34,
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 65)),
                Padding = new Thickness(8, 7, 8, 7),
                Text = !string.IsNullOrEmpty(initialPath) ? initialPath : ""
            };
            Grid.SetColumn(_txtExePath, 0);
            pathRow.Children.Add(_txtExePath);

            var btnBrowse = new Button
            {
                Content = "Browse...",
                Width = 90,
                Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(65, 65, 78)),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnBrowse.Click += delegate {
                var ofd = new System.Windows.Forms.OpenFileDialog
                {
                    Title = "Locate effectcraft.exe",
                    Filter = "EffectCraft Executable (effectcraft.exe)|effectcraft.exe|Executable (*.exe)|*.exe|All Files (*.*)|*.*",
                    InitialDirectory = !string.IsNullOrEmpty(_txtExePath.Text) && Directory.Exists(Path.GetDirectoryName(_txtExePath.Text))
                        ? Path.GetDirectoryName(_txtExePath.Text)
                        : Program.AppDir
                };
                if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _txtExePath.Text = ofd.FileName;
                }
            };
            Grid.SetColumn(btnBrowse, 2);
            pathRow.Children.Add(btnBrowse);
            bodyStack.Children.Add(pathRow);

            _lblStatus = new TextBlock
            {
                FontSize = 11,
                Margin = new Thickness(2, 0, 0, 18)
            };
            bodyStack.Children.Add(_lblStatus);

            _txtExePath.TextChanged += delegate { UpdateStatusLabel(); };
            UpdateStatusLabel();

            var optCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 50)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var optStack = new StackPanel();

            var optTitle = new TextBlock
            {
                Text = "Installation & Setup Preferences",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(190, 190, 205)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            optStack.Children.Add(optTitle);

            _chkDesktopShortcut = new System.Windows.Controls.CheckBox
            {
                Content = "Create Desktop Shortcut (Craft Console)",
                IsChecked = true,
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            };
            optStack.Children.Add(_chkDesktopShortcut);

            _chkStartMenuShortcut = new System.Windows.Controls.CheckBox
            {
                Content = "Create Start Menu Shortcut",
                IsChecked = true,
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            };
            optStack.Children.Add(_chkStartMenuShortcut);

            _chkInstallSplash = new System.Windows.Controls.CheckBox
            {
                Content = "Install Default Neon Unicorn Splash Screen into EffectCraft",
                IsChecked = true,
                Foreground = Brushes.White,
                FontSize = 12
            };
            optStack.Children.Add(_chkInstallSplash);

            optCard.Child = optStack;
            bodyStack.Children.Add(optCard);

            Grid.SetRow(bodyStack, 1);
            rootGrid.Children.Add(bodyStack);

            // Footer
            var footerRow = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            footerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var hintText = new TextBlock
            {
                Text = "Settings will be remembered across launches.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 130)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(hintText, 0);
            footerRow.Children.Add(hintText);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal };

            var btnCancel = new Button
            {
                Content = "Exit",
                Width = 85,
                Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(190, 190, 200)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 65)),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCancel.Click += delegate {
                this.DialogResult = false;
                this.Close();
            };
            btnStack.Children.Add(btnCancel);

            var btnLaunch = new Button
            {
                Content = "Save & Launch",
                Width = 140,
                Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 150, 255)),
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnLaunch.Click += delegate {
                string chosen = (_txtExePath.Text ?? "").Trim();
                if (Directory.Exists(chosen))
                {
                    chosen = Path.Combine(chosen, "effectcraft.exe");
                }
                if (string.IsNullOrEmpty(chosen) || !File.Exists(chosen))
                {
                    MessageBox.Show(
                        "Please select a valid effectcraft.exe before continuing.",
                        "Craft Console Setup",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                Program.SaveGlobalSetting("effectcraftexe", chosen);
                Program.EffectCraftExePath = chosen;

                string ownExe = Process.GetCurrentProcess().MainModule.FileName;

                if (_chkDesktopShortcut.IsChecked == true)
                {
                    try
                    {
                        string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        string lnk = Path.Combine(desk, "Craft Console.lnk");
                        Program.CreateShortcut(lnk, ownExe, "Craft Console — EffectCraft HUD Companion");
                    }
                    catch { }
                }

                if (_chkStartMenuShortcut.IsChecked == true)
                {
                    try
                    {
                        string progs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                        string lnk = Path.Combine(progs, "Craft Console.lnk");
                        Program.CreateShortcut(lnk, ownExe, "Craft Console — EffectCraft HUD Companion");
                    }
                    catch { }
                }

                if (_chkInstallSplash.IsChecked == true)
                {
                    string ecDir = Path.GetDirectoryName(chosen);
                    Program.EnsureSplashFiles(ecDir);
                    Program.EnsureSplashFiles(Program.AppDir);
                }

                this.DialogResult = true;
                this.Close();
            };
            btnStack.Children.Add(btnLaunch);

            Grid.SetColumn(btnStack, 1);
            footerRow.Children.Add(btnStack);

            Grid.SetRow(footerRow, 2);
            rootGrid.Children.Add(footerRow);

            this.Content = rootGrid;
        }

        private void UpdateStatusLabel()
        {
            string p = (_txtExePath.Text ?? "").Trim();
            if (Directory.Exists(p))
            {
                p = Path.Combine(p, "effectcraft.exe");
            }

            if (!string.IsNullOrEmpty(p) && File.Exists(p))
            {
                _lblStatus.Text = "✓ Ready: Valid effectcraft.exe located";
                _lblStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
            }
            else
            {
                _lblStatus.Text = "⚠ Please specify the path to effectcraft.exe";
                _lblStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 167, 38));
            }
        }
    }

    public class SettingsWindow : Window
    {
        private SplashConfig _config;

        // Visual Preview Controls
        private Border _previewCard;
        private Border _previewBadgeBorder;
        private WpfPath _previewBadgeMark;
        private TextBlock _previewPublisher;
        private TextBlock _previewTitle;
        private TextBlock _previewSubtitle;
        private TextBlock _previewArtist;
        private TextBlock _previewStatusText;
        private Border _previewProgressTrack;
        private Border _previewProgressBar;
        private StackPanel _previewFootnote;
        private WpfPath _previewBrandIcon;
        private Border _previewArtPortal;

        // Form Controls
        private TextBox _txtTitle;
        private TextBox _txtSubtitle;
        private TextBox _txtArtist;
        private TextBox _txtAccent;
        private System.Windows.Controls.RadioButton _rbDark;
        private System.Windows.Controls.RadioButton _rbLight;
        private TextBlock _lblImagePath;

        private Slider _sliderTitleSize;
        private TextBlock _lblTitleSizeVal;
        private Slider _sliderSubtitleSize;
        private TextBlock _lblSubtitleSizeVal;
        private Slider _sliderArtistSize;
        private TextBlock _lblArtistSizeVal;
        private Slider _sliderCornerRadius;
        private TextBlock _lblCornerRadiusVal;
        private Slider _sliderSplashDuration;
        private TextBlock _lblSplashDurationVal;

        private System.Windows.Controls.CheckBox _cbLoadingBar;
        private System.Windows.Controls.CheckBox _cbLoadingText;
        private System.Windows.Controls.CheckBox _cbFooter;

        // Extensions / Plugins Controls
        private Button _btnShortcut;
        private bool _isRecordingShortcut = false;

        // Tabs
        private Button _tabBtnVisual;
        private Button _tabBtnPlugins;
        private Grid _viewVisual;
        private Grid _viewPlugins;

        public SettingsWindow()
        {
            _config = SplashConfig.Load(Program.AppDir);

            this.Title = "EffectCraft Studio Customizer & Preferences";
            this.Width = 1040;
            this.Height = 750;
            this.MinWidth = 920;
            this.MinHeight = 660;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Background = new SolidColorBrush(Color.FromRgb(20, 20, 24));
            this.Foreground = Brushes.White;
            this.FontFamily = new FontFamily("Segoe UI");

            // Sleek dark scrollbar style across all ScrollViewers in the window
            try
            {
                string scrollStyleXaml = @"
                <ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                    <Style TargetType=""{x:Type ScrollBar}"">
                        <Setter Property=""Background"" Value=""Transparent""/>
                        <Setter Property=""BorderThickness"" Value=""0""/>
                        <Setter Property=""Width"" Value=""8""/>
                        <Setter Property=""Template"">
                            <Setter.Value>
                                <ControlTemplate TargetType=""{x:Type ScrollBar}"">
                                    <Grid Background=""Transparent"">
                                        <Track x:Name=""PART_Track"" IsDirectionReversed=""true"">
                                            <Track.Thumb>
                                                <Thumb>
                                                    <Thumb.Template>
                                                        <ControlTemplate TargetType=""{x:Type Thumb}"">
                                                            <Border Background=""#404048"" CornerRadius=""4"" Margin=""1,0,1,0""/>
                                                        </ControlTemplate>
                                                    </Thumb.Template>
                                                </Thumb>
                                            </Track.Thumb>
                                        </Track>
                                    </Grid>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>
                </ResourceDictionary>";
                var dict = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(scrollStyleXaml);
                this.Resources.MergedDictionaries.Add(dict);
            }
            catch { }

            var rootGrid = new Grid { Margin = new Thickness(24) };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Top Header & Tabs
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: Active Tab Body
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Bottom Action Bar

            // ROW 0: Header & Modern Navigation Tabs
            var topPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleBlock = new StackPanel { VerticalAlignment = System.Windows.VerticalAlignment.Center };
            var headerTitle = new TextBlock
            {
                Text = "EffectCraft Studio & Preferences",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            var headerSub = new TextBlock
            {
                Text = "Customize startup aesthetics, artwork and video, typography, and extension shortcuts.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 155)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            titleBlock.Children.Add(headerTitle);
            titleBlock.Children.Add(headerSub);
            Grid.SetColumn(titleBlock, 0);
            headerRow.Children.Add(titleBlock);

            // Modern Segmented Tab Buttons (never cut off)
            var tabSegment = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 33)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(3),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            var tabStack = new StackPanel { Orientation = Orientation.Horizontal };
            _tabBtnVisual = CreateTabButton("Splash & Visuals", true);
            _tabBtnPlugins = CreateTabButton("Extensions & Plug-ins", false);

            _tabBtnVisual.Click += delegate { SwitchTab(true); };
            _tabBtnPlugins.Click += delegate { SwitchTab(false); };

            tabStack.Children.Add(_tabBtnVisual);
            tabStack.Children.Add(_tabBtnPlugins);
            tabSegment.Child = tabStack;
            Grid.SetColumn(tabSegment, 1);
            headerRow.Children.Add(tabSegment);

            topPanel.Children.Add(headerRow);
            Grid.SetRow(topPanel, 0);
            rootGrid.Children.Add(topPanel);

            // ROW 1: TAB CONTENTS
            // View 1: Visual & Splash Customizer (Book-split layout: Left=Controls, Right=Scaled Stage)
            _viewVisual = BuildVisualBookView();
            Grid.SetRow(_viewVisual, 1);
            rootGrid.Children.Add(_viewVisual);

            // View 2: Extensions & Plug-ins
            _viewPlugins = BuildPluginsView();
            _viewPlugins.Visibility = Visibility.Collapsed;
            Grid.SetRow(_viewPlugins, 1);
            rootGrid.Children.Add(_viewPlugins);

            // ROW 2: BOTTOM ACTION BAR
            var bottomBar = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            bottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var footNote = new TextBlock
            {
                Text = "Configuration is saved to splash.ini • Changes take effect immediately",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 135)),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            Grid.SetColumn(footNote, 0);
            bottomBar.Children.Add(footNote);

            var rightActions = new StackPanel { Orientation = Orientation.Horizontal };
            var btnClose = new Button
            {
                Content = "Close",
                Padding = new Thickness(16, 8, 16, 8),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 205)),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnClose.Click += delegate { this.Close(); };
            rightActions.Children.Add(btnClose);

            var btnSave = new Button
            {
                Content = "Save & Apply Changes",
                Padding = new Thickness(20, 8, 20, 8),
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnSave.Click += delegate {
                Program.CurrentConfig = _config;
                Program.ApplyShortcut(_config.Shortcut);
                _config.Save(Program.AppDir);
                if (!string.IsNullOrEmpty(Program.EffectCraftExePath))
                {
                    string ecDir = Path.GetDirectoryName(Program.EffectCraftExePath);
                    if (!string.Equals(ecDir, Program.AppDir, StringComparison.OrdinalIgnoreCase))
                    {
                        try { _config.Save(ecDir); } catch { }
                    }
                }
                MessageBox.Show("Settings, visuals, and shortcut configurations saved and applied!", "EffectCraft Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            };
            rightActions.Children.Add(btnSave);

            Grid.SetColumn(rightActions, 1);
            bottomBar.Children.Add(rightActions);

            Grid.SetRow(bottomBar, 2);
            rootGrid.Children.Add(bottomBar);

            this.Content = rootGrid;
        }

        private Button CreateTabButton(string text, bool isActive)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = 12,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(165, 165, 172)),
                Background = isActive ? new SolidColorBrush(Color.FromRgb(46, 46, 56)) : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16, 7, 16, 7),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            return btn;
        }

        private void SwitchTab(bool showVisual)
        {
            _viewVisual.Visibility = showVisual ? Visibility.Visible : Visibility.Collapsed;
            _viewPlugins.Visibility = showVisual ? Visibility.Collapsed : Visibility.Visible;

            _tabBtnVisual.Background = showVisual ? new SolidColorBrush(Color.FromRgb(46, 46, 56)) : Brushes.Transparent;
            _tabBtnVisual.Foreground = showVisual ? Brushes.White : new SolidColorBrush(Color.FromRgb(165, 165, 172));
            _tabBtnVisual.FontWeight = showVisual ? FontWeights.SemiBold : FontWeights.Normal;

            _tabBtnPlugins.Background = !showVisual ? new SolidColorBrush(Color.FromRgb(46, 46, 56)) : Brushes.Transparent;
            _tabBtnPlugins.Foreground = !showVisual ? Brushes.White : new SolidColorBrush(Color.FromRgb(165, 165, 172));
            _tabBtnPlugins.FontWeight = !showVisual ? FontWeights.SemiBold : FontWeights.Normal;
        }

        private Grid BuildVisualBookView()
        {
            var bookGrid = new Grid();
            bookGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(450) }); // Left Page: Inspector Controls
            bookGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Gutter
            bookGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Right Page: Scaled Preview Stage

            // LEFT PAGE: Scrollable Inspector Controls
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 0, 10, 0)
            };
            var controlsStack = new StackPanel();

            // 1. Media Artwork (Image & Video)
            controlsStack.Children.Add(CreateFormLabel("Featured Artwork (Image or Video)"));
            var mediaRow = new Grid { Margin = new Thickness(0, 2, 0, 14) };
            mediaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mediaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _lblImagePath = new TextBlock
            {
                Text = Path.GetFileName(_config.ArtImage) + (SplashConfig.IsVideoFile(_config.ArtImage) ? " [VIDEO]" : ""),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 185)),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(_lblImagePath, 0);
            mediaRow.Children.Add(_lblImagePath);

            var btnBrowse = new Button
            {
                Content = "Browse Media...",
                ToolTip = "Choose Image (.png, .jpg) or Video (.mp4, .wmv, .avi, .mov)",
                Padding = new Thickness(14, 6, 14, 6),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnBrowse.Click += OnBrowseMedia;
            Grid.SetColumn(btnBrowse, 1);
            mediaRow.Children.Add(btnBrowse);
            controlsStack.Children.Add(mediaRow);

            // 2. Theme Mode
            controlsStack.Children.Add(CreateFormLabel("Theme Mode"));
            var themeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 14) };
            _rbDark = new System.Windows.Controls.RadioButton
            {
                Content = "Dark Mode",
                Foreground = Brushes.White,
                IsChecked = !_config.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase),
                Margin = new Thickness(0, 0, 20, 0)
            };
            _rbDark.Checked += delegate { _config.Theme = "Dark"; RefreshPreview(); };
            themeRow.Children.Add(_rbDark);

            _rbLight = new System.Windows.Controls.RadioButton
            {
                Content = "Light Mode",
                Foreground = Brushes.White,
                IsChecked = _config.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase)
            };
            _rbLight.Checked += delegate { _config.Theme = "Light"; RefreshPreview(); };
            themeRow.Children.Add(_rbLight);
            controlsStack.Children.Add(themeRow);

            // 3. Accent Palette & Custom Color Picker
            controlsStack.Children.Add(CreateFormLabel("Accent Color Palette"));
            var paletteGrid = new Grid { Margin = new Thickness(0, 4, 0, 14) };
            paletteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paletteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var swatchStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = System.Windows.VerticalAlignment.Center };
            string[] colors = new string[] { "#00D2FF", "#4E7BFB", "#9D4EDD", "#FF4757", "#00E676", "#FFAA00" };
            foreach (var hex in colors)
            {
                var swatch = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(12),
                    Background = (SolidColorBrush)new BrushConverter().ConvertFromString(hex),
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    BorderThickness = new Thickness(1.5),
                    BorderBrush = Brushes.White
                };
                string currHex = hex;
                swatch.MouseDown += delegate {
                    _config.AccentColor = currHex;
                    _txtAccent.Text = currHex;
                    RefreshPreview();
                };
                swatchStack.Children.Add(swatch);
            }
            Grid.SetColumn(swatchStack, 0);
            paletteGrid.Children.Add(swatchStack);

            var btnCustomColor = new Button
            {
                Content = "Custom Color...",
                Padding = new Thickness(10, 5, 10, 5),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCustomColor.Click += delegate {
                using (var cd = new System.Windows.Forms.ColorDialog())
                {
                    cd.AllowFullOpen = true;
                    cd.AnyColor = true;
                    if (cd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string hex = string.Format("#{0:X2}{1:X2}{2:X2}", cd.Color.R, cd.Color.G, cd.Color.B);
                        _config.AccentColor = hex;
                        _txtAccent.Text = hex;
                        RefreshPreview();
                    }
                }
            };
            Grid.SetColumn(btnCustomColor, 1);
            paletteGrid.Children.Add(btnCustomColor);
            controlsStack.Children.Add(paletteGrid);

            _txtAccent = CreateStyledTextBox(_config.AccentColor);
            _txtAccent.TextChanged += delegate {
                _config.AccentColor = _txtAccent.Text.Trim();
                RefreshPreview();
            };
            controlsStack.Children.Add(_txtAccent);

            // 4. Typography & Font Size Sliders
            controlsStack.Children.Add(CreateFormLabel("Application Title & Font Size"));
            _txtTitle = CreateStyledTextBox(_config.Title);
            _txtTitle.TextChanged += delegate { _config.Title = _txtTitle.Text.Trim(); RefreshPreview(); };
            controlsStack.Children.Add(_txtTitle);

            var titleSliderRow = CreateSliderRow(16, 42, _config.TitleFontSize, out _sliderTitleSize, out _lblTitleSizeVal, "px", delegate(double v) {
                _config.TitleFontSize = v;
                RefreshPreview();
            });
            controlsStack.Children.Add(titleSliderRow);

            controlsStack.Children.Add(CreateFormLabel("Subtitle / Edition Tag & Font Size"));
            _txtSubtitle = CreateStyledTextBox(_config.Subtitle);
            _txtSubtitle.TextChanged += delegate { _config.Subtitle = _txtSubtitle.Text.Trim(); RefreshPreview(); };
            controlsStack.Children.Add(_txtSubtitle);

            var subSliderRow = CreateSliderRow(8, 20, _config.SubtitleFontSize, out _sliderSubtitleSize, out _lblSubtitleSizeVal, "px", delegate(double v) {
                _config.SubtitleFontSize = v;
                RefreshPreview();
            });
            controlsStack.Children.Add(subSliderRow);

            controlsStack.Children.Add(CreateFormLabel("Artist / Credit Line & Font Size"));
            _txtArtist = CreateStyledTextBox(_config.ArtistCredit);
            _txtArtist.TextChanged += delegate { _config.ArtistCredit = _txtArtist.Text.Trim(); RefreshPreview(); };
            controlsStack.Children.Add(_txtArtist);

            var artistSliderRow = CreateSliderRow(7, 16, _config.ArtistFontSize, out _sliderArtistSize, out _lblArtistSizeVal, "px", delegate(double v) {
                _config.ArtistFontSize = v;
                RefreshPreview();
            });
            controlsStack.Children.Add(artistSliderRow);

            // 5. Card Appearance (Corner Roundness & Toggles)
            controlsStack.Children.Add(CreateFormLabel("Card Corner Roundness"));
            var cornerSliderRow = CreateSliderRow(0, 32, _config.CornerRadius, out _sliderCornerRadius, out _lblCornerRadiusVal, "px", delegate(double v) {
                _config.CornerRadius = v;
                RefreshPreview();
            });
            controlsStack.Children.Add(cornerSliderRow);

            controlsStack.Children.Add(CreateFormLabel("Visual Elements Toggles"));
            var togglesStack = new StackPanel { Margin = new Thickness(0, 2, 0, 16) };
            _cbLoadingBar = new System.Windows.Controls.CheckBox
            {
                Content = "Show Startup Progress Bar",
                Foreground = Brushes.White,
                IsChecked = _config.ShowLoadingBar,
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _cbLoadingBar.Checked += delegate { _config.ShowLoadingBar = true; RefreshPreview(); };
            _cbLoadingBar.Unchecked += delegate { _config.ShowLoadingBar = false; RefreshPreview(); };
            togglesStack.Children.Add(_cbLoadingBar);

            _cbLoadingText = new System.Windows.Controls.CheckBox
            {
                Content = "Show Startup Loading Status Text",
                Foreground = Brushes.White,
                IsChecked = _config.ShowLoadingText,
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _cbLoadingText.Checked += delegate { _config.ShowLoadingText = true; RefreshPreview(); };
            _cbLoadingText.Unchecked += delegate { _config.ShowLoadingText = false; RefreshPreview(); };
            togglesStack.Children.Add(_cbLoadingText);

            _cbFooter = new System.Windows.Controls.CheckBox
            {
                Content = "Show ArtCraft Suite Footnote",
                Foreground = Brushes.White,
                IsChecked = _config.ShowFooter,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _cbFooter.Checked += delegate { _config.ShowFooter = true; RefreshPreview(); };
            _cbFooter.Unchecked += delegate { _config.ShowFooter = false; RefreshPreview(); };
            togglesStack.Children.Add(_cbFooter);
            controlsStack.Children.Add(togglesStack);

            // 6. Startup Splash Duration / Launch Delay Slider
            controlsStack.Children.Add(CreateFormLabel("Startup Splash Duration / Launch Delay"));
            var durationSliderRow = CreateSliderRow(1.5, 8.0, _config.SplashDuration, out _sliderSplashDuration, out _lblSplashDurationVal, "s", delegate(double v) {
                _config.SplashDuration = v;
            });
            controlsStack.Children.Add(durationSliderRow);

            // 7. Reset Visuals to Factory Defaults Button
            var btnResetVisuals = new Button
            {
                Content = "↺  Reset All Visuals to Factory Defaults",
                Padding = new Thickness(14, 9, 14, 9),
                Background = new SolidColorBrush(Color.FromRgb(36, 36, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 225)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 62)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 4, 0, 16)
            };
            btnResetVisuals.Click += delegate {
                _config.Theme = "Dark";
                _config.AccentColor = "#00D2FF";
                _config.Title = "EffectCraft";
                _config.Subtitle = "Motion & Visual Effects";
                _config.ArtistCredit = "Cosmic Horizon Edition";
                _config.TitleFontSize = 32.0;
                _config.SubtitleFontSize = 11.5;
                _config.ArtistFontSize = 10.0;
                _config.CornerRadius = 18.0;
                _config.ShowLoadingBar = true;
                _config.ShowLoadingText = true;
                _config.ShowFooter = true;
                _config.SplashDuration = 3.5;
                _config.ArtImage = Path.Combine(Program.AppDir, "splash", "art.png");

                _txtAccent.Text = _config.AccentColor;
                _txtTitle.Text = _config.Title;
                _txtSubtitle.Text = _config.Subtitle;
                _txtArtist.Text = _config.ArtistCredit;
                if (_sliderTitleSize != null) _sliderTitleSize.Value = _config.TitleFontSize;
                if (_sliderSubtitleSize != null) _sliderSubtitleSize.Value = _config.SubtitleFontSize;
                if (_sliderArtistSize != null) _sliderArtistSize.Value = _config.ArtistFontSize;
                if (_sliderCornerRadius != null) _sliderCornerRadius.Value = _config.CornerRadius;
                if (_sliderSplashDuration != null) _sliderSplashDuration.Value = _config.SplashDuration;
                if (_rbDark != null) _rbDark.IsChecked = true;
                if (_rbLight != null) _rbLight.IsChecked = false;
                if (_cbLoadingBar != null) _cbLoadingBar.IsChecked = true;
                if (_cbLoadingText != null) _cbLoadingText.IsChecked = true;
                if (_cbFooter != null) _cbFooter.IsChecked = true;
                if (_lblImagePath != null) _lblImagePath.Text = Path.GetFileName(_config.ArtImage);
                RefreshPreview();
            };
            controlsStack.Children.Add(btnResetVisuals);

            scrollViewer.Content = controlsStack;
            Grid.SetColumn(scrollViewer, 0);
            bookGrid.Children.Add(scrollViewer);

            // RIGHT PAGE: Live Scaled Stage
            var rightStage = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 15, 17)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16)
            };

            var stageStack = new Grid();
            stageStack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Stage Header
            stageStack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Scaled Preview
            stageStack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bottom Desktop Runner

            var stageHeader = new TextBlock
            {
                Text = "LIVE PROPORTIONAL PREVIEW",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 125)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(stageHeader, 0);
            stageStack.Children.Add(stageHeader);

            var previewBox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(4)
            };
            _previewCard = BuildMiniPreviewCard();
            previewBox.Child = _previewCard;
            Grid.SetRow(previewBox, 1);
            stageStack.Children.Add(previewBox);

            var btnTestSplash = new Button
            {
                Content = "Preview Full Splash Screen (Desktop)",
                Padding = new Thickness(16, 10, 16, 10),
                Background = new SolidColorBrush(Color.FromRgb(32, 32, 38)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 64)),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            };
            btnTestSplash.Click += delegate {
                var testWin = new SplashWindow(_config);
                testWin.Show();
                ThreadPool.QueueUserWorkItem(delegate {
                    Thread.Sleep((int)(Math.Max(1.0, _config.SplashDuration) * 1000.0));
                    testWin.Dispatcher.BeginInvoke(new Action(delegate { testWin.DismissSplash(); }));
                });
            };
            Grid.SetRow(btnTestSplash, 2);
            stageStack.Children.Add(btnTestSplash);

            rightStage.Child = stageStack;
            Grid.SetColumn(rightStage, 2);
            bookGrid.Children.Add(rightStage);

            return bookGrid;
        }

        private Grid BuildPluginsView()
        {
            var pGrid = new Grid();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var mainStack = new StackPanel { Margin = new Thickness(0, 4, 12, 0) };

            // Panel 1: EC Console Extension
            var fxCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 18)
            };
            var fxStack = new StackPanel();

            var fxTitleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var fxIcon = new WpfPath
            {
                Data = Geometry.Parse(Program.ARTCRAFT_MARK_PATH),
                Fill = new SolidColorBrush(Color.FromRgb(0, 210, 255)),
                Width = 18,
                Height = 18,
                Stretch = Stretch.Uniform,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            fxTitleRow.Children.Add(fxIcon);

            var fxTitle = new TextBlock
            {
                Text = "EC Console (Command Palette & Fast Effect Launcher)",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            fxTitleRow.Children.Add(fxTitle);
            fxStack.Children.Add(fxTitleRow);

            var fxDesc = new TextBlock
            {
                Text = "EffectCraft floating command bar. Instant filter and search across effects, layer generators, menu commands, animation presets, and recent projects.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 165)),
                Margin = new Thickness(0, 0, 0, 14),
                TextWrapping = TextWrapping.Wrap
            };
            fxStack.Children.Add(fxDesc);

            fxStack.Children.Add(CreateFormLabel("Activation Keyboard Shortcut"));
            var shortcutGrid = new Grid { Margin = new Thickness(0, 4, 0, 10) };
            shortcutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            shortcutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            shortcutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _btnShortcut = new Button
            {
                Content = _config.Shortcut,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(36, 36, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 62)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Focusable = true
            };

            _btnShortcut.Click += delegate {
                _isRecordingShortcut = true;
                _btnShortcut.Content = "Press any key combo...";
                _btnShortcut.Background = new SolidColorBrush(Color.FromRgb(20, 50, 75));
                _btnShortcut.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 210, 255));
                _btnShortcut.Focus();
            };

            _btnShortcut.PreviewKeyDown += delegate(object s, KeyEventArgs e) {
                if (!_isRecordingShortcut) return;
                e.Handled = true;

                Key k = (e.Key == Key.System) ? e.SystemKey : e.Key;
                if (k == Key.LeftCtrl || k == Key.RightCtrl || k == Key.LeftAlt || k == Key.RightAlt || k == Key.LeftShift || k == Key.RightShift)
                {
                    return;
                }

                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

                var parts = new List<string>();
                if (ctrl) parts.Add("Ctrl");
                if (alt) parts.Add("Alt");
                if (shift) parts.Add("Shift");
                parts.Add(k.ToString());

                string combo = string.Join(" + ", parts.ToArray());
                _config.Shortcut = combo;
                _btnShortcut.Content = combo;
                _btnShortcut.Background = new SolidColorBrush(Color.FromRgb(36, 36, 42));
                _btnShortcut.BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 62));
                _isRecordingShortcut = false;
            };

            _btnShortcut.LostFocus += delegate {
                if (_isRecordingShortcut)
                {
                    _isRecordingShortcut = false;
                    _btnShortcut.Content = _config.Shortcut;
                    _btnShortcut.Background = new SolidColorBrush(Color.FromRgb(36, 36, 42));
                    _btnShortcut.BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 62));
                }
            };

            Grid.SetColumn(_btnShortcut, 0);
            shortcutGrid.Children.Add(_btnShortcut);

            var btnReset = new Button
            {
                Content = "Reset to Default (Ctrl + Space)",
                Padding = new Thickness(14, 0, 14, 0),
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnReset.Click += delegate {
                _config.Shortcut = "Ctrl + Space";
                _btnShortcut.Content = "Ctrl + Space";
                _btnShortcut.Background = new SolidColorBrush(Color.FromRgb(36, 36, 42));
                _btnShortcut.BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 62));
                _isRecordingShortcut = false;
            };
            Grid.SetColumn(btnReset, 2);
            shortcutGrid.Children.Add(btnReset);
            fxStack.Children.Add(shortcutGrid);

            // 1. Search Catalog Sections Toggles
            fxStack.Children.Add(CreateFormLabel("Search Catalog Sections"));
            var sectionsGrid = new Grid { Margin = new Thickness(0, 4, 0, 14) };
            sectionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sectionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sectionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sectionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sectionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Func<string, bool, Action<bool>, System.Windows.Controls.CheckBox> makeSectionCb = delegate(string txt, bool val, Action<bool> onChg) {
                var cb = new System.Windows.Controls.CheckBox
                {
                    Content = txt,
                    IsChecked = val,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    Margin = new Thickness(0, 3, 10, 5),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                cb.Checked += delegate { onChg(true); };
                cb.Unchecked += delegate { onChg(false); };
                return cb;
            };

            var cbEffects = makeSectionCb("Effects Catalog (306 Built-in)", _config.ConsoleShowEffects, delegate(bool b) { _config.ConsoleShowEffects = b; });
            Grid.SetRow(cbEffects, 0); Grid.SetColumn(cbEffects, 0);
            sectionsGrid.Children.Add(cbEffects);

            var cbPresets = makeSectionCb("Animation Presets", _config.ConsoleShowPresets, delegate(bool b) { _config.ConsoleShowPresets = b; });
            Grid.SetRow(cbPresets, 0); Grid.SetColumn(cbPresets, 1);
            sectionsGrid.Children.Add(cbPresets);

            var cbLayers = makeSectionCb("Quick Layer Generators (Solid, Null, etc.)", _config.ConsoleShowLayers, delegate(bool b) { _config.ConsoleShowLayers = b; });
            Grid.SetRow(cbLayers, 1); Grid.SetColumn(cbLayers, 0);
            sectionsGrid.Children.Add(cbLayers);

            var cbCommands = makeSectionCb("Command Palette Actions (Menu & Engine)", _config.ConsoleShowCommands, delegate(bool b) { _config.ConsoleShowCommands = b; });
            Grid.SetRow(cbCommands, 1); Grid.SetColumn(cbCommands, 1);
            sectionsGrid.Children.Add(cbCommands);

            var cbRecent = makeSectionCb("Recent Projects List (.ecproj)", _config.ConsoleShowRecent, delegate(bool b) { _config.ConsoleShowRecent = b; });
            Grid.SetRow(cbRecent, 2); Grid.SetColumn(cbRecent, 0);
            sectionsGrid.Children.Add(cbRecent);

            fxStack.Children.Add(sectionsGrid);

            // 2. Custom Smart Filtering Keyword Prefixes
            fxStack.Children.Add(CreateFormLabel("Custom Smart Filtering Keyword Prefixes"));
            var prefixGrid = new Grid { Margin = new Thickness(0, 4, 0, 14) };
            prefixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            prefixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            prefixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            prefixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            prefixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Action<string, string, Action<string>, int, int> addPrefixField = delegate(string lblText, string currVal, Action<string> onTxtChg, int row, int col) {
                var pPanel = new StackPanel { Margin = new Thickness(0, 0, 14, 8) };
                var pLabel = new TextBlock
                {
                    Text = lblText,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 155)),
                    Margin = new Thickness(0, 0, 0, 2)
                };
                pPanel.Children.Add(pLabel);

                var pTxt = CreateStyledTextBox(currVal);
                pTxt.Height = 30;
                pTxt.FontSize = 12;
                pTxt.TextChanged += delegate { onTxtChg(pTxt.Text.Trim()); };
                pPanel.Children.Add(pTxt);

                Grid.SetRow(pPanel, row);
                Grid.SetColumn(pPanel, col);
                prefixGrid.Children.Add(pPanel);
            };

            addPrefixField("Effects Prefix", _config.PrefixEffects, delegate(string s) { _config.PrefixEffects = s; }, 0, 0);
            addPrefixField("Presets Prefix", _config.PrefixPresets, delegate(string s) { _config.PrefixPresets = s; }, 0, 1);
            addPrefixField("Layers Prefix", _config.PrefixLayers, delegate(string s) { _config.PrefixLayers = s; }, 0, 2);
            addPrefixField("Commands Prefix", _config.PrefixCommands, delegate(string s) { _config.PrefixCommands = s; }, 1, 0);
            addPrefixField("Recent Projects Prefix", _config.PrefixRecent, delegate(string s) { _config.PrefixRecent = s; }, 1, 1);

            fxStack.Children.Add(prefixGrid);

            var fxStatus = new TextBlock
            {
                Text = "✓ 306 Built-in Effects, Quick Layer Generators, Engine Commands & Recent Projects Indexed  •  Spawn at Mouse Cursor",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 190, 120)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            fxStack.Children.Add(fxStatus);

            fxCard.Child = fxStack;
            mainStack.Children.Add(fxCard);

            // Panel 2: ScriptUI & Panels
            var scriptCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 18)
            };
            var scriptStack = new StackPanel();
            var scriptTitle = new TextBlock
            {
                Text = "Installed ScriptUI Panels",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8)
            };
            scriptStack.Children.Add(scriptTitle);

            var panelItem = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 40)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var pRow = new Grid();
            pRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var pName = new TextBlock { Text = "Quick Console.jsx", FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
            var pStatus = new TextBlock { Text = "Active (Integrated)", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 255)) };
            Grid.SetColumn(pName, 0);
            Grid.SetColumn(pStatus, 1);
            pRow.Children.Add(pName);
            pRow.Children.Add(pStatus);
            panelItem.Child = pRow;
            scriptStack.Children.Add(panelItem);

            scriptCard.Child = scriptStack;
            mainStack.Children.Add(scriptCard);

            // Panel 3: Effect Plug-ins Directory
            var pluginsDirCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20)
            };
            var dirStack = new StackPanel();
            var dirTitle = new TextBlock
            {
                Text = "Native & WebAssembly Plug-ins Directory",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8)
            };
            dirStack.Children.Add(dirTitle);

            string pluginsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EffectCraft", "Plug-ins");
            var dirDesc = new TextBlock
            {
                Text = "EffectCraft scans this directory on startup for .wasm plugin modules:\n" + pluginsPath,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 165)),
                Margin = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.Wrap
            };
            dirStack.Children.Add(dirDesc);

            var btnOpenPlugins = new Button
            {
                Content = "Open Plug-ins Folder in Explorer",
                Padding = new Thickness(14, 7, 14, 7),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            btnOpenPlugins.Click += delegate {
                try
                {
                    if (!Directory.Exists(pluginsPath)) Directory.CreateDirectory(pluginsPath);
                    Process.Start("explorer.exe", pluginsPath);
                }
                catch { }
            };
            dirStack.Children.Add(btnOpenPlugins);

            pluginsDirCard.Child = dirStack;
            mainStack.Children.Add(pluginsDirCard);

            // Panel: Installation, Shortcuts & EffectCraft Location
            var installCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 18)
            };
            var installStack = new StackPanel();

            var installTitle = new TextBlock
            {
                Text = "Installation, Shortcuts & EffectCraft Link",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 6)
            };
            installStack.Children.Add(installTitle);

            var installDesc = new TextBlock
            {
                Text = "Manage your desktop shortcuts and linked EffectCraft executable path.",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 145)),
                Margin = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.Wrap
            };
            installStack.Children.Add(installDesc);

            string currentLinked = Program.EffectCraftExePath ?? "Not configured (Local directory bundle)";
            var pathLabel = new TextBlock
            {
                Text = "Linked Executable: " + currentLinked,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 255)),
                Margin = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.Wrap
            };
            installStack.Children.Add(pathLabel);

            var actionButtonsRow = new WrapPanel { Orientation = Orientation.Horizontal };

            var btnCreateDesk = new Button
            {
                Content = "Create Desktop Shortcut",
                Padding = new Thickness(14, 6, 14, 6),
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(65, 65, 78)),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 8)
            };
            btnCreateDesk.Click += delegate {
                try
                {
                    string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string lnk = Path.Combine(desk, "Craft Console.lnk");
                    string ownExe = Process.GetCurrentProcess().MainModule.FileName;
                    Program.CreateShortcut(lnk, ownExe, "Craft Console — EffectCraft HUD Companion");
                    MessageBox.Show("Desktop shortcut created successfully!", "Craft Console", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error creating shortcut: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            actionButtonsRow.Children.Add(btnCreateDesk);

            var btnChangeEcPath = new Button
            {
                Content = "Locate EffectCraft...",
                Padding = new Thickness(14, 6, 14, 6),
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(65, 65, 78)),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 8)
            };
            btnChangeEcPath.Click += delegate {
                var setup = new FirstLaunchSetupWindow(Program.EffectCraftExePath);
                if (setup.ShowDialog() == true)
                {
                    pathLabel.Text = "Linked Executable: " + (Program.EffectCraftExePath ?? "");
                }
            };
            actionButtonsRow.Children.Add(btnChangeEcPath);

            var btnReinstallSplash = new Button
            {
                Content = "Reinstall Neon Unicorn Splash",
                Padding = new Thickness(14, 6, 14, 6),
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(65, 65, 78)),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 8)
            };
            btnReinstallSplash.Click += delegate {
                try
                {
                    Program.EnsureSplashFiles(Program.AppDir);
                    if (!string.IsNullOrEmpty(Program.EffectCraftExePath))
                    {
                        Program.EnsureSplashFiles(Path.GetDirectoryName(Program.EffectCraftExePath));
                    }
                    MessageBox.Show("Neon Unicorn splash screen files installed successfully!", "Craft Console", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            actionButtonsRow.Children.Add(btnReinstallSplash);

            installStack.Children.Add(actionButtonsRow);
            installCard.Child = installStack;
            mainStack.Children.Add(installCard);

            scroll.Content = mainStack;
            pGrid.Children.Add(scroll);
            return pGrid;
        }

        private Grid CreateSliderRow(double min, double max, double val, out Slider slider, out TextBlock valLabel, string unit, Action<double> onChange)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = val,
                SmallChange = 1,
                LargeChange = 2,
                IsSnapToTickEnabled = true,
                TickFrequency = 0.5,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            Grid.SetColumn(slider, 0);
            row.Children.Add(slider);

            valLabel = new TextBlock
            {
                Text = val.ToString("0.0") + unit,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 185)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            Grid.SetColumn(valLabel, 1);
            row.Children.Add(valLabel);

            var sRef = slider;
            var lRef = valLabel;
            slider.ValueChanged += delegate(object s, RoutedPropertyChangedEventArgs<double> e) {
                lRef.Text = e.NewValue.ToString("0.0") + unit;
                if (onChange != null) onChange(e.NewValue);
            };

            return row;
        }

        private TextBlock CreateFormLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 175)),
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        private TextBox CreateStyledTextBox(string text)
        {
            return new TextBox
            {
                Text = text,
                FontSize = 13,
                Padding = new Thickness(8, 6, 8, 6),
                Background = new SolidColorBrush(Color.FromRgb(36, 36, 42)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 62)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        private void OnBrowseMedia(object sender, RoutedEventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Select Splash Screen Artwork or Video";
                dlg.Filter = "Media Files (*.png;*.jpg;*.jpeg;*.bmp;*.mp4;*.wmv;*.avi;*.mov)|*.png;*.jpg;*.jpeg;*.bmp;*.mp4;*.wmv;*.avi;*.mov|Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Videos (*.mp4;*.wmv;*.avi;*.mov)|*.mp4;*.wmv;*.avi;*.mov|All Files (*.*)|*.*";
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        // 1. Fully release any media playback lock in preview before copying
                        if (_previewArtPortal != null)
                        {
                            var oldMedia = _previewArtPortal.Child as System.Windows.Controls.MediaElement;
                            if (oldMedia != null)
                            {
                                oldMedia.Stop();
                                oldMedia.Close();
                                oldMedia.Source = null;
                            }
                            _previewArtPortal.Child = null;
                        }
                        GC.Collect();
                        GC.WaitForPendingFinalizers();

                        string targetDir = Path.Combine(Program.AppDir, "splash");
                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                        string ext = Path.GetExtension(dlg.FileName);

                        // Save with timestamped unique name to completely avoid file lock collisions
                        string uniqueName = "art_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ext;
                        string destPath = Path.Combine(targetDir, uniqueName);

                        File.Copy(dlg.FileName, destPath, true);
                        _config.ArtImage = destPath;
                        _lblImagePath.Text = Path.GetFileName(destPath) + (SplashConfig.IsVideoFile(destPath) ? " [VIDEO]" : "");
                        RefreshPreview();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to copy media file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private Border BuildMiniPreviewCard()
        {
            bool isLight = _config.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase);
            Color accent = _config.GetAccentColor();
            Color badgeBg = _config.GetBadgeBackground();

            // Native 730x450 canvas: Exact 1:1 match with real SplashWindow, scaled smoothly by Viewbox!
            var card = new Border
            {
                CornerRadius = new CornerRadius(_config.CornerRadius),
                BorderThickness = new Thickness(1),
                Background = isLight ? Brushes.White : new SolidColorBrush(Color.FromRgb(20, 20, 22)),
                BorderBrush = isLight ? new SolidColorBrush(Color.FromRgb(225, 225, 230)) : new SolidColorBrush(Color.FromRgb(46, 46, 50)),
                Width = 730,
                Height = 450,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 38,
                    ShadowDepth = 6,
                    Opacity = isLight ? 0.35 : 0.75
                }
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left Column
            var left = new Grid { Margin = new Thickness(34, 30, 20, 26) };
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Top Brand Badge
            var topBrandRow = new StackPanel { Orientation = Orientation.Horizontal };
            _previewBadgeBorder = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(Math.Max(6, _config.CornerRadius * 0.55)),
                Background = new SolidColorBrush(badgeBg),
                BorderBrush = new SolidColorBrush(accent),
                BorderThickness = new Thickness(1.6)
            };
            _previewBadgeMark = new WpfPath
            {
                Data = Geometry.Parse(Program.ARTCRAFT_MARK_PATH),
                Fill = new SolidColorBrush(accent),
                Width = 26,
                Height = 26,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            _previewBadgeBorder.Child = _previewBadgeMark;
            topBrandRow.Children.Add(_previewBadgeBorder);

            var metaStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(12, 2, 0, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            var publisherPill = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Background = isLight ? new SolidColorBrush(Color.FromRgb(240, 240, 244)) : new SolidColorBrush(Color.FromRgb(38, 38, 42)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            _previewPublisher = new TextBlock
            {
                Text = "ArtCraft",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(100, 100, 105)) : new SolidColorBrush(Color.FromRgb(160, 160, 165))
            };
            publisherPill.Child = _previewPublisher;
            metaStack.Children.Add(publisherPill);

            _previewSubtitle = new TextBlock
            {
                Text = _config.Subtitle,
                FontSize = _config.SubtitleFontSize,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(115, 115, 120)) : new SolidColorBrush(Color.FromRgb(140, 140, 145)),
                Margin = new Thickness(0, 3, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            metaStack.Children.Add(_previewSubtitle);
            topBrandRow.Children.Add(metaStack);

            Grid.SetRow(topBrandRow, 0);
            left.Children.Add(topBrandRow);

            // Middle: Big Typography & Artwork Credits
            var mid = new StackPanel
            {
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(0, 14, 0, 0)
            };
            _previewTitle = new TextBlock
            {
                Text = _config.Title,
                FontSize = _config.TitleFontSize,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI, Arial"),
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(25, 25, 28)) : Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            mid.Children.Add(_previewTitle);

            var copyright = new TextBlock
            {
                Text = "© 2026 EffectCraft Open Engine. All rights reserved.",
                FontSize = 9.5,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(140, 140, 145)) : new SolidColorBrush(Color.FromRgb(105, 105, 110)),
                Margin = new Thickness(0, 10, 0, 0)
            };
            mid.Children.Add(copyright);

            _previewArtist = new TextBlock
            {
                Text = "Artwork: " + _config.ArtistCredit,
                FontSize = _config.ArtistFontSize,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(120, 120, 125)) : new SolidColorBrush(Color.FromRgb(135, 135, 140)),
                Margin = new Thickness(0, 3, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            mid.Children.Add(_previewArtist);

            Grid.SetRow(mid, 1);
            left.Children.Add(mid);

            // Bottom: Progress & Footnote
            var btm = new StackPanel { VerticalAlignment = System.Windows.VerticalAlignment.Bottom };
            _previewStatusText = new TextBlock
            {
                Text = "Initializing Engine Core...",
                FontSize = 11.5,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(90, 90, 95)) : new SolidColorBrush(Color.FromRgb(180, 180, 185)),
                Margin = new Thickness(0, 0, 0, _config.ShowLoadingBar ? 8 : 0),
                Visibility = _config.ShowLoadingText ? Visibility.Visible : Visibility.Collapsed
            };
            btm.Children.Add(_previewStatusText);

            _previewProgressTrack = new Border
            {
                Height = 3.5,
                CornerRadius = new CornerRadius(2),
                Background = isLight ? new SolidColorBrush(Color.FromRgb(230, 230, 235)) : new SolidColorBrush(Color.FromRgb(40, 40, 45)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                Visibility = _config.ShowLoadingBar ? Visibility.Visible : Visibility.Collapsed
            };
            _previewProgressBar = new Border
            {
                Height = 3.5,
                Width = 110,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(accent),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            _previewProgressTrack.Child = _previewProgressBar;
            btm.Children.Add(_previewProgressTrack);

            _previewFootnote = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 12, 0, 0),
                Visibility = _config.ShowFooter ? Visibility.Visible : Visibility.Collapsed
            };
            _previewBrandIcon = new WpfPath
            {
                Data = Geometry.Parse(Program.ARTCRAFT_MARK_PATH),
                Fill = new SolidColorBrush(accent),
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            _previewFootnote.Children.Add(_previewBrandIcon);

            var btmLabel = new TextBlock
            {
                Text = "ArtCraft Motion Creative Suite",
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Foreground = isLight ? new SolidColorBrush(Color.FromRgb(120, 120, 125)) : new SolidColorBrush(Color.FromRgb(110, 110, 115)),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            _previewFootnote.Children.Add(btmLabel);
            btm.Children.Add(_previewFootnote);

            Grid.SetRow(btm, 2);
            left.Children.Add(btm);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            // Right Artwork / Video Portal (Supports Video & Images)
            _previewArtPortal = new Border
            {
                CornerRadius = new CornerRadius(Math.Max(4, _config.CornerRadius - 4)),
                ClipToBounds = true,
                Margin = new Thickness(8, 14, 14, 14),
                Background = new SolidColorBrush(Color.FromRgb(15, 15, 20))
            };
            SplashConfig.PopulateMediaPortal(_previewArtPortal, _config.ArtImage);
            Grid.SetColumn(_previewArtPortal, 1);
            grid.Children.Add(_previewArtPortal);

            card.Child = grid;
            return card;
        }

        private void RefreshPreview()
        {
            if (_previewCard == null) return;

            bool isLight = _config.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase);
            Color accent = _config.GetAccentColor();
            Color badgeBg = _config.GetBadgeBackground();

            _previewCard.CornerRadius = new CornerRadius(_config.CornerRadius);
            _previewCard.Background = isLight ? Brushes.White : new SolidColorBrush(Color.FromRgb(20, 20, 22));
            _previewCard.BorderBrush = isLight ? new SolidColorBrush(Color.FromRgb(225, 225, 230)) : new SolidColorBrush(Color.FromRgb(46, 46, 50));

            _previewBadgeBorder.CornerRadius = new CornerRadius(Math.Max(6, _config.CornerRadius * 0.55));
            _previewBadgeBorder.Background = new SolidColorBrush(badgeBg);
            _previewBadgeBorder.BorderBrush = new SolidColorBrush(accent);
            _previewBadgeMark.Fill = new SolidColorBrush(accent);

            _previewTitle.Text = _config.Title;
            _previewTitle.FontSize = _config.TitleFontSize;
            _previewTitle.Foreground = isLight ? new SolidColorBrush(Color.FromRgb(25, 25, 28)) : Brushes.White;

            _previewSubtitle.Text = _config.Subtitle;
            _previewSubtitle.FontSize = _config.SubtitleFontSize;

            _previewArtist.Text = "Artwork: " + _config.ArtistCredit;
            _previewArtist.FontSize = _config.ArtistFontSize;

            _previewStatusText.Visibility = _config.ShowLoadingText ? Visibility.Visible : Visibility.Collapsed;
            _previewStatusText.Margin = new Thickness(0, 0, 0, _config.ShowLoadingBar ? 8 : 0);

            _previewProgressTrack.Visibility = _config.ShowLoadingBar ? Visibility.Visible : Visibility.Collapsed;
            _previewProgressBar.Background = new SolidColorBrush(accent);

            _previewFootnote.Visibility = _config.ShowFooter ? Visibility.Visible : Visibility.Collapsed;
            _previewBrandIcon.Fill = new SolidColorBrush(accent);

            _previewArtPortal.CornerRadius = new CornerRadius(Math.Max(4, _config.CornerRadius - 4));
            SplashConfig.PopulateMediaPortal(_previewArtPortal, _config.ArtImage);
        }
    }
}
