using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace CraftConsole.CrossPlatform
{
    public partial class ConsoleWindow : Window
    {
        private TextBox _searchBox;
        private Border _divider;
        private ScrollViewer _resultsScrollViewer;
        private StackPanel _resultsStack;
        private TextBlock _statusTextBlock;

        private Button _btnSnapshot;
        private Button _btnPresets;
        private Button _btnExport;
        private Button _btnSettings;
        private Button _btnBrand;

        private List<EffectItem> _catalog = new List<EffectItem>();
        private List<DisplayRow> _currentRows = new List<DisplayRow>();
        private int _selectedIndex = -1;
        private DispatcherTimer _debounceTimer;

        public bool IsTerminating { get; set; } = false;
        private bool _isOpening = false;

        private static readonly SolidColorBrush _brushFavStar = new SolidColorBrush(Color.FromRgb(255, 215, 0));
        private static readonly SolidColorBrush _brushUnfavStar = new SolidColorBrush(Color.FromRgb(95, 95, 105));
        private static readonly SolidColorBrush _brushTextDark = new SolidColorBrush(Color.FromRgb(225, 225, 225));
        private static readonly SolidColorBrush _brushExtraDark = new SolidColorBrush(Color.FromRgb(125, 125, 130));
        private static readonly SolidColorBrush _brushSelDark = new SolidColorBrush(Color.FromRgb(0, 122, 204));
        public static ConsoleWindow? CurrentInstance { get; private set; }

        public ConsoleWindow()
        {
            CurrentInstance = this;
            InitializeComponent();

            _searchBox = this.FindControl<TextBox>("SearchBox")!;
            _divider = this.FindControl<Border>("Divider")!;
            _resultsScrollViewer = this.FindControl<ScrollViewer>("ResultsScrollViewer")!;
            _resultsStack = this.FindControl<StackPanel>("ResultsStack")!;
            _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock")!;

            _btnSnapshot = this.FindControl<Button>("BtnSnapshot")!;
            _btnPresets = this.FindControl<Button>("BtnPresets")!;
            _btnExport = this.FindControl<Button>("BtnExport")!;
            _btnSettings = this.FindControl<Button>("BtnSettings")!;
            _btnBrand = this.FindControl<Button>("BtnBrand")!;

            _catalog = Catalog.GetDefaultCatalog();
            RefreshFavoritesCache();

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                PerformSearch();
            };

            _searchBox.TextChanged += (s, e) =>
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };
            _searchBox.KeyDown += OnSearchBoxKeyDown;

            _btnSnapshot.Click += (s, e) => ExecuteSnapshot();
            _btnPresets.Click += (s, e) =>
            {
                _searchBox.Text = "@preset ";
                _searchBox.CaretIndex = _searchBox.Text.Length;
                _searchBox.Focus();
            };
            _btnExport.Click += (s, e) => ExecuteExportPng();
            _btnSettings.Click += (s, e) => OpenSettings();
            _btnBrand.Click += (s, e) =>
            {
                try { EngineClient.SendTcpCommand("{\"id\":99,\"method\":\"ui.focus\",\"params\":{}}"); } catch { }
            };

            Closing += (s, e) =>
            {
                if (!IsTerminating)
                {
                    e.Cancel = true;
                    Hide();
                }
            };

            KeyDown += OnWindowKeyDown;
            Deactivated += (s, e) =>
            {
                if (_isOpening) return;
                Hide();
            };

            Loaded += (s, e) =>
            {
                _searchBox.Focus();
                SyncLiveCommands();
            };

            this.AddHandler(InputElement.PointerPressedEvent, (s, e) =>
            {
                var pt = e.GetCurrentPoint(this);
                if (pt.Properties.IsLeftButtonPressed && pt.Position.Y < 44)
                {
                    BeginMoveDrag(e);
                }
            }, RoutingStrategies.Tunnel);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void ToggleVisibility()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (IsVisible)
                {
                    Hide();
                }
                else
                {
                    _isOpening = true;
                    PositionAtCursorOrActiveScreen();
                    Show();
                    Activate();
                    _searchBox.Text = "";
                    _statusTextBlock.IsVisible = false;
                    PerformSearch();
                    _searchBox.Focus();
                    _isOpening = false;
                }
            });
        }

        public void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        public void PositionAtCursorOrActiveScreen()
        {
            var screen = LinuxDesktopIntegration.GetActiveScreenGeometry();
            bool hasCur = LinuxDesktopIntegration.TryGetCursorPosition(out int curX, out int curY);

            double targetLeft;
            double targetTop;

            if (hasCur)
            {
                // Exact formula matching Windows src/Launcher.cs:L2309-2323
                targetLeft = curX - (this.Width / 2.0);
                targetTop = curY - 22; // Align search input box with cursor height
            }
            else
            {
                // Center precisely on the active monitor where mouse/window is focused
                targetLeft = screen.X + (screen.Width - this.Width) / 2.0;
                targetTop = screen.Y + 220;
            }

            // Clamp within active monitor boundaries so it never overflows off-screen
            if (targetLeft < screen.X + 10) targetLeft = screen.X + 10;
            if (targetLeft + this.Width > screen.X + screen.Width - 10) targetLeft = screen.X + screen.Width - this.Width - 10;
            if (targetTop < screen.Y + 10) targetTop = screen.Y + 10;
            if (targetTop + 330 > screen.Y + screen.Height - 10) targetTop = screen.Y + screen.Height - 330 - 10;

            this.Position = new PixelPoint((int)targetLeft, (int)targetTop);
        }

        private void RefreshFavoritesCache()
        {
            FavoritesStorage.EnsureLoaded();
            foreach (var item in _catalog)
            {
                item.IsFavorite = FavoritesStorage.IsFavorite(item.Name, item.Id);
            }
        }

        private void PerformSearch()
        {
            string query = (_searchBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(query))
            {
                var cfg = SplashConfig.Load();
                if (cfg.ShowFavoritesOnOpen)
                {
                    ShowFavoritesList();
                    return;
                }

                _divider.IsVisible = false;
                _resultsScrollViewer.IsVisible = false;
                _resultsStack.Children.Clear();
                _currentRows.Clear();
                _selectedIndex = -1;
                return;
            }

            UpdateFilter(query);
        }

        private void ShowFavoritesList()
        {
            _resultsStack.Children.Clear();
            _currentRows.Clear();
            _selectedIndex = -1;

            var favMatches = _catalog.Where(x => x.IsFavorite).ToList();
            if (favMatches.Count > 0)
            {
                AddCategoryHeader("★ Favorites & Quick Access (Enter to Apply • Click ★ or Ctrl+D to unpin)", Color.FromRgb(255, 215, 0));
                foreach (var fav in favMatches)
                {
                    AddItemRow(fav);
                }

                _divider.IsVisible = true;
                _resultsScrollViewer.IsVisible = true;

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
                _divider.IsVisible = false;
                _resultsScrollViewer.IsVisible = false;
            }
        }

        private void UpdateFilter(string query)
        {
            _resultsStack.Children.Clear();
            _currentRows.Clear();
            _selectedIndex = -1;

            string cleanLower = query.ToLowerInvariant();
            var matches = new List<EffectItem>();

            // Prefix routing
            if (cleanLower.StartsWith("@preset"))
            {
                string sub = cleanLower.Substring(Math.Min(7, cleanLower.Length)).Trim();
                matches = _catalog.Where(x => x.Kind == ConsoleItemKind.Preset && (string.IsNullOrEmpty(sub) || x.LowerName.Contains(sub))).ToList();
                AddCategoryHeader("Animation Presets (" + matches.Count + ")", Color.FromRgb(0, 210, 255));
            }
            else if (cleanLower.StartsWith("@fav"))
            {
                string sub = cleanLower.Substring(Math.Min(4, cleanLower.Length)).Trim();
                matches = _catalog.Where(x => x.IsFavorite && (string.IsNullOrEmpty(sub) || x.LowerName.Contains(sub))).ToList();
                AddCategoryHeader("★ Favorites (" + matches.Count + ")", Color.FromRgb(255, 215, 0));
            }
            else if (cleanLower.StartsWith("@layer"))
            {
                string sub = cleanLower.Substring(Math.Min(6, cleanLower.Length)).Trim();
                matches = _catalog.Where(x => x.Kind == ConsoleItemKind.Layer && (string.IsNullOrEmpty(sub) || x.LowerName.Contains(sub))).ToList();
                AddCategoryHeader("New Layers (" + matches.Count + ")", Color.FromRgb(255, 107, 74));
            }
            else if (cleanLower.StartsWith("@cmd"))
            {
                string sub = cleanLower.Substring(Math.Min(4, cleanLower.Length)).Trim();
                matches = _catalog.Where(x => x.Kind == ConsoleItemKind.Command && (string.IsNullOrEmpty(sub) || x.LowerName.Contains(sub))).ToList();
                AddCategoryHeader("Commands (" + matches.Count + ")", Color.FromRgb(78, 123, 251));
            }
            else
            {
                // General smart search: partitioned favorites first, then effects & commands
                var favs = _catalog.Where(x => x.IsFavorite && (x.LowerName.Contains(cleanLower) || x.LowerKeywords.Contains(cleanLower))).ToList();
                var others = _catalog.Where(x => !x.IsFavorite && (x.LowerName.Contains(cleanLower) || x.LowerKeywords.Contains(cleanLower))).ToList();
                matches.AddRange(favs);
                matches.AddRange(others);
            }

            int added = 0;
            const int MAX_ROWS = 28;

            foreach (var item in matches)
            {
                if (added++ >= MAX_ROWS) break;
                AddItemRow(item);
            }

            if (_currentRows.Count > 0)
            {
                _divider.IsVisible = true;
                _resultsScrollViewer.IsVisible = true;
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
                _divider.IsVisible = false;
                _resultsScrollViewer.IsVisible = false;
            }
        }

        private void AddCategoryHeader(string text, Color color)
        {
            var headerBorder = new Border
            {
                Height = 22,
                Margin = new Thickness(14, 4, 14, 2),
                Background = Brushes.Transparent
            };
            var headerText = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(color),
                FontSize = 10.5,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            _resultsStack.Children.Add(headerBorder);
            _currentRows.Add(new DisplayRow { IsHeader = true, Container = headerBorder, Label = headerText });
        }

        private void AddItemRow(EffectItem item)
        {
            int rowIndex = _currentRows.Count;

            var rowBorder = new Border
            {
                Height = 27,
                Margin = new Thickness(4, 1, 4, 1),
                CornerRadius = new CornerRadius(3),
                Background = Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(28, GridUnitType.Pixel));

            var label = new TextBlock
            {
                Text = item.Name,
                Foreground = _brushTextDark,
                FontSize = 13.0,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 0, 10, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(label, 0);
            rowGrid.Children.Add(label);

            TextBlock? extraLabel = null;
            if (!string.IsNullOrEmpty(item.Extra) || !string.IsNullOrEmpty(item.Category))
            {
                extraLabel = new TextBlock
                {
                    Text = !string.IsNullOrEmpty(item.Extra) ? item.Extra : item.Category,
                    Foreground = _brushExtraDark,
                    FontSize = 11.0,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                };
                Grid.SetColumn(extraLabel, 1);
                rowGrid.Children.Add(extraLabel);
            }

            var starBtn = new TextBlock
            {
                Text = item.IsFavorite ? "★" : "☆",
                Foreground = item.IsFavorite ? _brushFavStar : _brushUnfavStar,
                FontSize = 13.0,
                FontWeight = item.IsFavorite ? FontWeight.Bold : FontWeight.Normal,
                Width = 24,
                Height = 24,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            ToolTip.SetTip(starBtn, item.IsFavorite ? "★ Favorited (Click to unpin)" : "☆ Click to pin to Favorites (or Ctrl+D)");

            starBtn.PointerPressed += (s, e) =>
            {
                e.Handled = true;
                ToggleItemFavorite(item, starBtn);
            };
            Grid.SetColumn(starBtn, 2);
            rowGrid.Children.Add(starBtn);

            rowBorder.Child = rowGrid;

            var rowObj = new DisplayRow
            {
                IsHeader = false,
                Item = item,
                Container = rowBorder,
                Label = label,
                ExtraLabel = extraLabel
            };

            rowBorder.PointerEntered += (s, e) => SetSelectedIndex(rowIndex);
            rowBorder.PointerPressed += (s, e) => ApplySelectedItem(rowObj.Item);

            _resultsStack.Children.Add(rowBorder);
            _currentRows.Add(rowObj);
        }

        private void ToggleItemFavorite(EffectItem item, TextBlock starBtn)
        {
            FavoritesStorage.ToggleFavorite(item.Name, item.Id);
            item.IsFavorite = FavoritesStorage.IsFavorite(item.Name, item.Id);

            starBtn.Text = item.IsFavorite ? "★" : "☆";
            starBtn.Foreground = item.IsFavorite ? _brushFavStar : _brushUnfavStar;
            starBtn.FontWeight = item.IsFavorite ? FontWeight.Bold : FontWeight.Normal;
            ToolTip.SetTip(starBtn, item.IsFavorite ? "★ Favorited (Click to unpin)" : "☆ Click to pin to Favorites (or Ctrl+D)");

            ShowToast(item.IsFavorite ? $"★ Added '{item.Name}' to Favorites" : $"☆ Removed '{item.Name}' from Favorites");

            if (string.IsNullOrEmpty((_searchBox.Text ?? "").Trim()))
            {
                ShowFavoritesList();
            }
        }

        private void SetSelectedIndex(int index)
        {
            if (_selectedIndex >= 0 && _selectedIndex < _currentRows.Count)
            {
                var oldRow = _currentRows[_selectedIndex];
                if (!oldRow.IsHeader && oldRow.Container is Border b)
                {
                    b.Background = Brushes.Transparent;
                    if (oldRow.Label != null)
                    {
                        oldRow.Label.Foreground = _brushTextDark;
                        oldRow.Label.FontWeight = FontWeight.Normal;
                    }
                }
            }

            _selectedIndex = index;

            if (_selectedIndex >= 0 && _selectedIndex < _currentRows.Count)
            {
                var newRow = _currentRows[_selectedIndex];
                if (!newRow.IsHeader && newRow.Container is Border b)
                {
                    b.Background = _brushSelDark;
                    if (newRow.Label != null)
                    {
                        newRow.Label.Foreground = Brushes.White;
                        newRow.Label.FontWeight = FontWeight.SemiBold;
                    }
                }
            }
        }

        private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                e.Handled = true;
                MoveSelection(1);
            }
            else if (e.Key == Key.Up)
            {
                e.Handled = true;
                MoveSelection(-1);
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (_selectedIndex >= 0 && _selectedIndex < _currentRows.Count)
                {
                    ApplySelectedItem(_currentRows[_selectedIndex].Item);
                }
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Hide();
            }
            else if (e.Key == Key.D && (e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                e.Handled = true;
                if (_selectedIndex >= 0 && _selectedIndex < _currentRows.Count && _currentRows[_selectedIndex].Item != null)
                {
                    var item = _currentRows[_selectedIndex].Item!;
                    FavoritesStorage.ToggleFavorite(item.Name, item.Id);
                    item.IsFavorite = FavoritesStorage.IsFavorite(item.Name, item.Id);
                    ShowToast(item.IsFavorite ? $"★ Added '{item.Name}' to Favorites" : $"☆ Removed '{item.Name}' from Favorites");
                    PerformSearch();
                }
            }
        }

        private void OnWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
            }
        }

        private void MoveSelection(int delta)
        {
            if (_currentRows.Count == 0) return;

            int target = _selectedIndex + delta;
            while (target >= 0 && target < _currentRows.Count && _currentRows[target].IsHeader)
            {
                target += delta;
            }

            if (target >= 0 && target < _currentRows.Count)
            {
                SetSelectedIndex(target);
            }
        }

        private async void ApplySelectedItem(EffectItem? item)
        {
            if (item == null) return;

            var (success, msg) = await EngineClient.ApplyItemAsync(item);
            if (success)
            {
                Hide();
            }
            else
            {
                ShowToast(msg);
            }
        }

        private void ExecuteSnapshot()
        {
            string? res = EngineClient.SendTcpCommand("{\"id\":20,\"method\":\"viewport.snapshot\",\"params\":{}}");
            ShowToast(!string.IsNullOrEmpty(res) ? "Snapshot captured and copied to clipboard" : "EffectCraft engine not responding");
        }

        private void ExecuteExportPng()
        {
            string? res = EngineClient.SendTcpCommand("{\"id\":21,\"method\":\"render.export_png\",\"params\":{}}");
            ShowToast(!string.IsNullOrEmpty(res) ? "Current frame rendered to PNG" : "EffectCraft engine not responding");
        }

        private void OpenSettings()
        {
            Hide();
            var settings = new SettingsWindow();
            settings.Show();
        }

        private void ShowToast(string message)
        {
            _statusTextBlock.Text = message;
            _statusTextBlock.IsVisible = true;
            Task.Delay(3000).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() => _statusTextBlock.IsVisible = false);
            });
        }

        private void SyncLiveCommands()
        {
            try
            {
                EngineClient.SyncLiveMenuItems(liveItems =>
                {
                    if (liveItems != null && liveItems.Count > 0)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            foreach (var it in liveItems)
                            {
                                if (!_catalog.Any(c => c.Id == it.Id))
                                {
                                    _catalog.Add(it);
                                }
                            }
                            RefreshFavoritesCache();
                        });
                    }
                });
            }
            catch { }
        }
    }
}
