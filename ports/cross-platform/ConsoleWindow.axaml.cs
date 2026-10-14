using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace CraftConsole.CrossPlatform
{
    public partial class ConsoleWindow : Window
    {
        private TextBox _searchBox;
        private ListBox _resultsListBox;
        private Border _divider;
        private TextBlock _statusTextBlock;
        private Button _btnSnapshot;
        private Button _btnPresets;
        private Button _btnExport;
        private Button _btnSettings;
        private Button _btnBrand;

        private List<EffectItem> _catalog = new List<EffectItem>();
        private ObservableCollection<EffectItem> _filtered = new ObservableCollection<EffectItem>();
        public bool IsTerminating { get; set; } = false;

        public ConsoleWindow()
        {
            InitializeComponent();

            _searchBox = this.FindControl<TextBox>("SearchBox")!;
            _resultsListBox = this.FindControl<ListBox>("ResultsListBox")!;
            _divider = this.FindControl<Border>("Divider")!;
            _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock")!;

            _btnSnapshot = this.FindControl<Button>("BtnSnapshot")!;
            _btnPresets = this.FindControl<Button>("BtnPresets")!;
            _btnExport = this.FindControl<Button>("BtnExport")!;
            _btnSettings = this.FindControl<Button>("BtnSettings")!;
            _btnBrand = this.FindControl<Button>("BtnBrand")!;

            _resultsListBox.ItemsSource = _filtered;

            // Initialize catalog
            _catalog = Catalog.GetDefaultCatalog();
            RefreshFavoritesState();

            // Wire event handlers
            _searchBox.TextChanged += (s, e) => FilterItems();
            _searchBox.KeyDown += OnSearchBoxKeyDown;

            _resultsListBox.DoubleTapped += (s, e) => ApplySelectedItem();
            _resultsListBox.KeyDown += OnListBoxKeyDown;

            // Action buttons
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

            // Intercept star clicks inside listbox
            AddHandler(Button.ClickEvent, OnItemStarClicked, RoutingStrategies.Bubble);

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
                Hide();
            };

            Loaded += (s, e) =>
            {
                _searchBox.Focus();
                SyncLiveCommands();
            };
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
                    Show();
                    Activate();
                    _searchBox.Text = "";
                    FilterItems();
                    _searchBox.Focus();
                }
            });
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void RefreshFavoritesState()
        {
            FavoritesStorage.EnsureLoaded();
            foreach (var item in _catalog)
            {
                item.IsFavorite = FavoritesStorage.IsFavorite(item.Name, item.Id);
            }
        }

        private void FilterItems()
        {
            string query = (_searchBox.Text ?? "").Trim();

            if (string.IsNullOrEmpty(query))
            {
                _filtered.Clear();
                _divider.IsVisible = false;
                _resultsListBox.IsVisible = false;
                return;
            }

            var results = new List<EffectItem>();
            string lowerQuery = query.ToLowerInvariant();

            // Prefix filters matching Windows stable build
            if (lowerQuery.StartsWith("@preset"))
            {
                string sub = lowerQuery.Substring(Math.Min(7, lowerQuery.Length)).Trim();
                results.AddRange(_catalog.Where(x => x.Kind == ConsoleItemKind.Preset && (string.IsNullOrEmpty(sub) || x.LowerName.Contains(sub))));
            }
            else if (lowerQuery.StartsWith("@fav"))
            {
                string sub = lowerQuery.Substring(Math.Min(4, lowerQuery.Length)).Trim();
                results.AddRange(_catalog.Where(x => x.IsFavorite && (string.IsNullOrEmpty(sub) || x.LowerName.Contains(sub))));
            }
            else if (lowerQuery.StartsWith("@layer"))
            {
                string sub = lowerQuery.Substring(Math.Min(6, lowerQuery.Length)).Trim();
                results.AddRange(_catalog.Where(x => x.Kind == ConsoleItemKind.Layer && (string.IsNullOrEmpty(sub) || x.LowerName.Contains(sub))));
            }
            else if (lowerQuery.StartsWith("@cmd"))
            {
                string sub = lowerQuery.Substring(Math.Min(4, lowerQuery.Length)).Trim();
                results.AddRange(_catalog.Where(x => x.Kind == ConsoleItemKind.Command && (string.IsNullOrEmpty(sub) || x.LowerName.Contains(sub))));
            }
            else
            {
                // General smart search
                foreach (var item in _catalog)
                {
                    if (item.LowerName.Contains(lowerQuery) ||
                        item.LowerCategory.Contains(lowerQuery) ||
                        (!string.IsNullOrEmpty(item.LowerKeywords) && item.LowerKeywords.Contains(lowerQuery)))
                    {
                        results.Add(item);
                    }
                }

                // Rank favorites and exact matches first
                results = results.OrderByDescending(x => x.LowerName.StartsWith(lowerQuery))
                                 .ThenByDescending(x => x.IsFavorite)
                                 .Take(60)
                                 .ToList();
            }

            _filtered.Clear();
            foreach (var r in results) _filtered.Add(r);

            bool hasResults = _filtered.Count > 0;
            _divider.IsVisible = hasResults;
            _resultsListBox.IsVisible = hasResults;

            if (hasResults)
            {
                _resultsListBox.SelectedIndex = 0;
            }
        }

        private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                if (_filtered.Count > 0)
                {
                    _resultsListBox.Focus();
                    if (_resultsListBox.SelectedIndex < _filtered.Count - 1)
                        _resultsListBox.SelectedIndex++;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (_filtered.Count > 0)
                {
                    _resultsListBox.Focus();
                    if (_resultsListBox.SelectedIndex > 0)
                        _resultsListBox.SelectedIndex--;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                ApplySelectedItem();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape || (e.Key == Key.Space && (e.KeyModifiers & KeyModifiers.Control) != 0))
            {
                Hide();
                e.Handled = true;
            }
        }

        private void OnListBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplySelectedItem();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && _resultsListBox.SelectedIndex == 0)
            {
                _searchBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape || (e.Key == Key.Space && (e.KeyModifiers & KeyModifiers.Control) != 0))
            {
                Hide();
                e.Handled = true;
            }
            else if (e.Key == Key.F && (e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                ToggleSelectedFavorite();
                e.Handled = true;
            }
        }

        private void OnWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || (e.Key == Key.Space && (e.KeyModifiers & KeyModifiers.Control) != 0))
            {
                Hide();
                e.Handled = true;
            }
            else if (e.Key == Key.F && (e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                ToggleSelectedFavorite();
                e.Handled = true;
            }
        }

        private void OnItemStarClicked(object? sender, RoutedEventArgs e)
        {
            if (e.Source is Button btn && btn.Classes.Contains("fav-star") && btn.Tag is EffectItem item)
            {
                FavoritesStorage.ToggleFavorite(item.Name, item.Id);
                item.IsFavorite = !item.IsFavorite;

                int idx = _filtered.IndexOf(item);
                if (idx >= 0)
                {
                    _filtered[idx] = item;
                }

                ShowToast(item.IsFavorite ? $"★ Added {item.Name} to favorites" : $"Removed {item.Name} from favorites");
                e.Handled = true;
            }
        }

        private void ToggleSelectedFavorite()
        {
            if (_resultsListBox.SelectedItem is EffectItem item)
            {
                FavoritesStorage.ToggleFavorite(item.Name, item.Id);
                item.IsFavorite = !item.IsFavorite;

                int idx = _filtered.IndexOf(item);
                if (idx >= 0)
                {
                    _filtered[idx] = item;
                }

                ShowToast(item.IsFavorite ? $"★ Added {item.Name} to favorites" : $"Removed {item.Name} from favorites");
            }
        }

        private async void ApplySelectedItem()
        {
            if (_resultsListBox.SelectedItem is not EffectItem item) return;

            ShowToast($"Applying {item.Name}...", "#00D2FF");

            var (success, message) = await EngineClient.ApplyItemAsync(item);

            if (success)
            {
                ShowToast($"✓ {item.Name} applied", "#10B981");
                await Task.Delay(250);
                Hide();
            }
            else
            {
                ShowToast($"⚠️ {message}", "#F59E0B");
            }
        }

        private void ShowToast(string message, string hexColor = "#00C8FF")
        {
            _statusTextBlock.Text = message;
            _statusTextBlock.Foreground = new SolidColorBrush(Color.Parse(hexColor));
            _statusTextBlock.IsVisible = true;
        }

        private void ExecuteSnapshot()
        {
            ShowToast("Capturing viewport frame...", "#00D2FF");
            Task.Run(() =>
            {
                try
                {
                    string? res = EngineClient.SendTcpCommand("{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"edit.copyMerged\",\"params\":{}}}");
                    Dispatcher.UIThread.Post(() => ShowToast("✓ Frame copied to clipboard", "#10B981"));
                }
                catch
                {
                    Dispatcher.UIThread.Post(() => ShowToast("⚠️ Could not capture snapshot", "#F59E0B"));
                }
            });
        }

        private void ExecuteExportPng()
        {
            ShowToast("Exporting PNG frame...", "#00D2FF");
            Task.Run(() =>
            {
                try
                {
                    EngineClient.SendTcpCommand("{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"composition.saveFrameAs\",\"params\":{}}}");
                    Dispatcher.UIThread.Post(() => ShowToast("✓ Export dialog opened", "#10B981"));
                }
                catch
                {
                    Dispatcher.UIThread.Post(() => ShowToast("⚠️ Could not export frame", "#F59E0B"));
                }
            });
        }

        private void OpenSettings()
        {
            Hide();
            var win = new SettingsWindow();
            win.Show();
        }

        private void SyncLiveCommands()
        {
            EngineClient.SyncLiveMenuItems(discovered =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    int addedCount = 0;
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
                            addedCount++;
                        }
                    }

                    if (addedCount > 0 && !string.IsNullOrEmpty(_searchBox.Text))
                    {
                        FilterItems();
                    }
                });
            });
        }
    }
}
