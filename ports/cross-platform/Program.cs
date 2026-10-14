using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace CraftConsole.CrossPlatform
{
    public class Program
    {
        public const int IpcPort = 9879;
        private static TcpListener? _ipcListener;
        private static ConsoleWindow? _consoleWindow;

        [STAThread]
        public static void Main(string[] args)
        {
            // 1. Handle --uninstall CLI
            if (args.Contains("--uninstall"))
            {
                bool purge = args.Contains("--purge");
                Console.WriteLine("Uninstalling Craft Console...");
                var (removed, purged) = LinuxDesktopIntegration.Uninstall(purge);
                Console.WriteLine($"Successfully removed {removed} launcher shortcuts.");
                if (purged) Console.WriteLine("Removed user configuration directory (~/.config/effectcraft).");
                return;
            }

            // 2. Handle --setup CLI
            if (args.Contains("--setup"))
            {
                Console.WriteLine("Generating user-space desktop shortcuts...");
                bool ok = LinuxDesktopIntegration.CreateDesktopEntries(true, true);
                Console.WriteLine(ok ? "Shortcuts generated successfully." : "Failed to generate shortcuts.");
                FirstLaunchWindow.MarkFirstLaunchDone();
                return;
            }

            // 3. Handle --toggle CLI
            if (args.Contains("--toggle"))
            {
                if (SignalRunningInstance())
                {
                    // Running instance toggled successfully
                    return;
                }
            }

            // Check if another instance is already running
            if (SignalRunningInstance())
            {
                Console.WriteLine("Craft Console instance already running. Toggling visibility.");
                return;
            }

            // Start Avalonia App
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .AfterSetup(builder =>
                {
                    if (builder.Instance?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        SetupAppFlow(desktop);
                    }
                });

        private static TrayIcon? _trayIcon;

        private static void EnsureConsoleWindow()
        {
            if (_consoleWindow == null)
            {
                _consoleWindow = new ConsoleWindow();
            }
        }

        private static void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                _trayIcon = new TrayIcon
                {
                    ToolTipText = "Craft Console",
                    IsVisible = true
                };

                try
                {
                    var iconUri = new Uri("avares://CraftConsole/assets/icon.png");
                    if (Avalonia.Platform.AssetLoader.Exists(iconUri))
                    {
                        using var stream = Avalonia.Platform.AssetLoader.Open(iconUri);
                        _trayIcon.Icon = new WindowIcon(stream);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Could not load tray icon from assets: " + ex.Message);
                }

                var menu = new NativeMenu();

                var toggleItem = new NativeMenuItem("Show / Hide Console");
                toggleItem.Click += (s, e) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        EnsureConsoleWindow();
                        _consoleWindow?.ToggleVisibility();
                    });
                };
                menu.Items.Add(toggleItem);

                var settingsItem = new NativeMenuItem("Settings...");
                settingsItem.Click += (s, e) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        var win = new SettingsWindow();
                        win.Show();
                    });
                };
                menu.Items.Add(settingsItem);

                menu.Items.Add(new NativeMenuItemSeparator());

                var quitItem = new NativeMenuItem("Quit Craft Console");
                quitItem.Click += (s, e) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_consoleWindow != null) _consoleWindow.IsTerminating = true;
                        desktop.Shutdown();
                    });
                };
                menu.Items.Add(quitItem);

                _trayIcon.Menu = menu;
                _trayIcon.Clicked += (s, e) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        EnsureConsoleWindow();
                        _consoleWindow?.ToggleVisibility();
                    });
                };

                var icons = TrayIcon.GetIcons(Application.Current!);
                if (icons == null)
                {
                    icons = new TrayIcons();
                    TrayIcon.SetIcons(Application.Current!, icons);
                }
                icons.Add(_trayIcon);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not initialize tray icon: " + ex.Message);
            }
        }

        private static void SetupAppFlow(IClassicDesktopStyleApplicationLifetime desktop)
        {
            // CRITICAL: Prevent Avalonia from closing the daemon when the splash or console window closes/hides
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Start background IPC server immediately to handle hotkey/CLI --toggle triggers
            StartIpcServer(() =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (LinuxDesktopIntegration.IsEffectCraftForeground())
                    {
                        EnsureConsoleWindow();
                        _consoleWindow?.ToggleVisibility();
                    }
                });
            });

            // Initialize system tray icon
            SetupTrayIcon(desktop);

            var config = SplashConfig.Load();

            // Configure non-intrusive In-App shortcut inside EffectCraft
            LinuxDesktopIntegration.ConfigureInAppShortcut(config.Shortcut);

            // Start universal scoped shortcut listener (active exclusively when EffectCraft is foreground/running)
            LinuxDesktopIntegration.EvdevShortcutListener.TargetShortcut = config.Shortcut;
            LinuxDesktopIntegration.EvdevShortcutListener.Start(() =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    EnsureConsoleWindow();
                    _consoleWindow?.ToggleVisibility();
                });
            });

            void ShowMainWindow()
            {
                Dispatcher.UIThread.Post(() =>
                {
                    EnsureConsoleWindow();
                    desktop.MainWindow = _consoleWindow;
                    _consoleWindow!.Show();
                    _consoleWindow!.Activate();
                });
            }

            // Check first launch
            if (FirstLaunchWindow.IsFirstLaunch())
            {
                var setupWin = new FirstLaunchWindow(() =>
                {
                    var refreshedConfig = SplashConfig.Load();
                    LaunchSplashOrConsole(refreshedConfig, ShowMainWindow, desktop);
                });
                desktop.MainWindow = setupWin;
                setupWin.Show();
            }
            else
            {
                LaunchSplashOrConsole(config, ShowMainWindow, desktop);
            }
        }

        private static void LaunchSplashOrConsole(SplashConfig config, Action showMain, IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (config.InstantLaunch)
            {
                showMain();
            }
            else
            {
                SplashWindow? splash = null;
                splash = new SplashWindow(config, () =>
                {
                    showMain();

                    // If splash launched EffectCraft, attach exit monitor
                    if (splash?.EngineProcess != null)
                    {
                        var proc = splash.EngineProcess;
                        Task.Run(async () =>
                        {
                            try
                            {
                                await proc.WaitForExitAsync();
                                // EffectCraft closed, terminate Console too
                                Dispatcher.UIThread.Post(() =>
                                {
                                    if (_consoleWindow != null) _consoleWindow.IsTerminating = true;
                                    desktop.Shutdown();
                                });
                            }
                            catch { }
                        });
                    }
                });

                desktop.MainWindow = splash;
                splash.Show();
            }
        }

        private static bool SignalRunningInstance()
        {
            try
            {
                using var client = new TcpClient();
                var ar = client.BeginConnect(IPAddress.Loopback, IpcPort, null, null);
                if (ar.AsyncWaitHandle.WaitOne(300))
                {
                    client.EndConnect(ar);
                    using var stream = client.GetStream();
                    using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                    writer.WriteLine("toggle");
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static void StartIpcServer(Action onToggleRequest)
        {
            try
            {
                _ipcListener = new TcpListener(IPAddress.Loopback, IpcPort);
                _ipcListener.Start();
                Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            var client = await _ipcListener.AcceptTcpClientAsync();
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    using (client)
                                    using (var stream = client.GetStream())
                                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                                    {
                                        string? line = await reader.ReadLineAsync();
                                        if (line == "toggle")
                                        {
                                            onToggleRequest();
                                        }
                                    }
                                }
                                catch { }
                            });
                        }
                        catch { }
                    }
                });
            }
            catch { }
        }
    }
}
