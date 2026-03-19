/* Copyright (c) 2024 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.XPath;
using static SAM.Picker.InvariantShorthand;
using APITypes = SAM.API.Types;

namespace SAM.Picker
{
    public partial class MainWindow : Window
    {
        private readonly API.Client _SteamClient;
        private readonly ConcurrentDictionary<uint, GameInfo> _Games = new();
        private readonly List<GameInfo> _FilteredGames = new();
        private readonly BackgroundWorker _ListWorker = new();
        private readonly API.Callbacks.AppDataChanged _AppDataChangedCallback;
        private readonly DispatcherTimer _CallbackTimer;
        private HashSet<uint> _FavoriteAppIds = null;

        public MainWindow(API.Client client = null)
        {
            if (client != null)
            {
                this._SteamClient = client;
            }
            else
            {
                this._SteamClient = new API.Client();
            }

            this.InitializeComponent();

            try
            {
                var uri = new Uri("pack://application:,,,/SAM.ico", UriKind.Absolute);
                this.Icon = new System.Windows.Media.Imaging.BitmapImage(uri);
                this.ImgAppIcon.Source = this.Icon;
            }
            catch { }

            this._ListWorker.DoWork += this.DoDownloadList;
            this._ListWorker.RunWorkerCompleted += this.OnDownloadList;

            this._AppDataChangedCallback = this._SteamClient.CreateAndRegisterCallback<API.Callbacks.AppDataChanged>();
            this._AppDataChangedCallback.OnRun += this.OnAppDataChanged;

            this._CallbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            this._CallbackTimer.Tick += (s, e) => this._SteamClient.RunCallbacks(false);
            this._CallbackTimer.Start();

            this.InitializeLanguageDropdown();
            this.ApplyLocalization();

            if (client == null)
            {
                try
                {
                    this._SteamClient.Initialize(0);
                }
                catch (API.ClientInitializeException ex)
                {
                    MessageBox.Show(this, ex.Message, API.Localization.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }
            }

            this.AddGames();
        }

        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
            this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void OnCloseClick(object sender, RoutedEventArgs e) => this.Close();

        private void InitializeLanguageDropdown()
        {
            this.MenuLanguage.Items.Clear();
            foreach (var lang in API.LanguageManager.SupportedLanguages)
            {
                var item = new MenuItem
                {
                    Header = lang.NativeName,
                    Tag = lang.Code,
                    IsCheckable = true,
                    StaysOpenOnClick = true
                };
                item.Click += (s, e) =>
                {
                    API.LanguageManager.CurrentLanguage = lang.Code;
                    this.UpdateLanguageDropdownState();
                    this.ApplyLocalization();
                    this.RefreshGames();
                };
                this.MenuLanguage.Items.Add(item);
            }
            this.UpdateLanguageDropdownState();
        }

        private void UpdateLanguageDropdownState()
        {
            string current = API.LanguageManager.CurrentLanguage;
            foreach (MenuItem item in this.MenuLanguage.Items)
            {
                item.IsChecked = string.Equals((string)item.Tag, current, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void ApplyLocalization()
        {
            this.TxtTitle.Text = API.Localization.PickerTitle;
            this.Title = API.Localization.PickerTitle;

            this.TxtBtnRefresh.Text = API.Localization.RefreshGames;
            this.TxtBtnAddGame.Text = API.Localization.AddGame;
            this.TxtSearchPlaceholder.Text = API.Localization.Get("SearchPlaceholder", "Поиск игры...");

            this.MenuFilter.Header = "📺 " + API.Localization.GameFiltering;
            this.MenuFilterGames.Header = API.Localization.ShowGames.Replace("&", "");
            this.MenuFilterFavorites.Header = "⭐ " + API.Localization.Get("ShowFavorites", "Только избранное");
            this.MenuFilterTools.Header = API.Localization.Get("ShowTools", "Показывать программы");
            this.MenuFilterDLCs.Header = API.Localization.Get("ShowDLCs", "Показывать DLC");
            this.MenuFilterDemos.Header = API.Localization.ShowDemos.Replace("&", "");
            this.MenuFilterMods.Header = API.Localization.ShowMods.Replace("&", "");
            this.MenuFilterJunk.Header = API.Localization.ShowJunk.Replace("&", "");

            this.MenuLanguage.Header = "🌐 " + API.Localization.Language;

            if (this._Games.Count > 0)
            {
                this.TxtStatus.Text = API.Localization.DisplayingGames(this._FilteredGames.Count, this._Games.Count);
            }
        }

        private void OnAppDataChanged(APITypes.AppDataChanged param)
        {
            if (param.Result == false) return;
            if (this._Games.TryGetValue(param.Id, out var game) == false) return;

            lock (API.Steam.SteamLock)
            {
                game.Name = this._SteamClient.SteamApps001.GetAppData(game.Id, "name");
                game.ImageUrl = GetGameImageUrl(game.Id);
            }
            this.RefreshGames();
        }

        private void AddGames()
        {
            if (this._ListWorker.IsBusy) return;
            this._Games.Clear();
            this.RefreshGames();
            this.BtnRefresh.IsEnabled = false;
            this.TxtStatus.Text = API.Localization.DownloadingGameList;
            this._ListWorker.RunWorkerAsync();
        }

        private void DoDownloadList(object sender, DoWorkEventArgs e)
        {
            try
            {
                this.Dispatcher.Invoke(() => this.TxtStatus.Text = API.Localization.DownloadingGameList);

                byte[] bytes;
                using (WebClient downloader = new())
                {
                    bytes = downloader.DownloadData(new Uri("https://gib.me/sam/games.xml"));
                }

                List<KeyValuePair<uint, string>> pairs = new();
                using (MemoryStream stream = new(bytes, false))
                {
                    XPathDocument document = new(stream);
                    var navigator = document.CreateNavigator();
                    var nodes = navigator.Select("/games/game");
                    while (nodes.MoveNext())
                    {
                        string type = nodes.Current.GetAttribute("type", "");
                        if (string.IsNullOrEmpty(type)) type = "normal";
                        pairs.Add(new((uint)nodes.Current.ValueAsLong, type));
                    }
                }

                this.Dispatcher.Invoke(() => this.TxtStatus.Text = API.Localization.CheckingGameOwnership);
                foreach (var kv in pairs)
                {
                    this.AddGame(kv.Key, kv.Value);
                }
                this.Dispatcher.Invoke(() => this.RefreshGames());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void OnDownloadList(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (e.Error != null || e.Cancelled || this._Games.IsEmpty)
                    {
                        this.AddDefaultGames();
                    }

                    this.RefreshGames();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
                finally
                {
                    this.BtnRefresh.IsEnabled = true;
                }
            });
        }

        private void AddDefaultGames()
        {
            this.AddGame(480, "normal"); // Spacewar
        }

        private bool OwnsGame(uint id)
        {
            lock (API.Steam.SteamLock)
            {
                return this._SteamClient.SteamApps008.IsSubscribedApp(id);
            }
        }

        private void AddGame(uint id, string type)
        {
            if (this._Games.ContainsKey(id)) return;
            try
            {
                if (this.OwnsGame(id) == false) return;

                if (this._FavoriteAppIds == null)
                {
                    this._FavoriteAppIds = API.SteamLibraryFavourites.GetFavoriteAppIds();
                }

                string name;
                string appType;
                lock (API.Steam.SteamLock)
                {
                    name = this._SteamClient.SteamApps001.GetAppData(id, "name");
                    appType = this._SteamClient.SteamApps001.GetAppData(id, "type");
                }

                if (!string.IsNullOrEmpty(appType))
                {
                    string lowerType = appType.ToLowerInvariant();
                    if (lowerType is "dlc")
                    {
                        type = "dlc";
                    }
                    else if (lowerType is "tool" or "application")
                    {
                        type = "tool";
                    }
                    else if (lowerType is "demo")
                    {
                        type = "demo";
                    }
                    else if (lowerType is "mod")
                    {
                        type = "mod";
                    }
                    else if (lowerType is "config" or "hardware" or "media" or "video" or "series" or "junk")
                    {
                        type = "junk";
                    }
                }

                GameInfo info = new(id, type)
                {
                    Name = name,
                    IsFavorite = this._FavoriteAppIds.Contains(id)
                };

                lock (API.Steam.SteamLock)
                {
                    info.ImageUrl = GetGameImageUrl(info.Id);
                }

                this._Games.TryAdd(id, info);
            }
            catch { }
        }

        private void RefreshGames()
        {
            var nameSearch = this.TxtSearchGame.Text.Length > 0 ? this.TxtSearchGame.Text : null;
            bool wantNormals = this.MenuFilterGames.IsChecked;
            bool wantFavoritesOnly = this.MenuFilterFavorites.IsChecked;
            bool wantTools = this.MenuFilterTools.IsChecked;
            bool wantDLCs = this.MenuFilterDLCs.IsChecked;
            bool wantDemos = this.MenuFilterDemos.IsChecked;
            bool wantMods = this.MenuFilterMods.IsChecked;
            bool wantJunk = this.MenuFilterJunk.IsChecked;

            this._FilteredGames.Clear();
            foreach (var info in this._Games.Values.OrderBy(gi => gi.Name))
            {
                if (nameSearch != null && info.Name.IndexOf(nameSearch, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (wantFavoritesOnly && info.IsFavorite == false)
                {
                    continue;
                }

                bool wanted = info.Type switch
                {
                    "normal" => wantNormals,
                    "tool" => wantTools,
                    "dlc" => wantDLCs,
                    "demo" => wantDemos,
                    "mod" => wantMods,
                    "junk" => wantJunk,
                    _ => true
                };

                if (wanted == false) continue;
                this._FilteredGames.Add(info);
            }

            this.ItemsGameCards.ItemsSource = null;
            this.ItemsGameCards.ItemsSource = this._FilteredGames;
            this.TxtStatus.Text = API.Localization.DisplayingGames(this._FilteredGames.Count, this._Games.Count);
        }

        private void OnSearchFilterChanged(object sender, TextChangedEventArgs e)
        {
            this.TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(this.TxtSearchGame.Text) ? Visibility.Visible : Visibility.Collapsed;
            this.RefreshGames();
        }

        private void OnFilterMenuClick(object sender, RoutedEventArgs e) => this.RefreshGames();

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            this.TxtSearchGame.Text = "";
            this.TxtAddGameId.Text = "";
            this.AddGames();
        }

        private void OnAddGameClick(object sender, RoutedEventArgs e)
        {
            if (uint.TryParse(this.TxtAddGameId.Text, out uint id) == false)
            {
                MessageBox.Show(this, API.Localization.PleaseEnterValidGameId, API.Localization.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (this.OwnsGame(id) == false)
            {
                MessageBox.Show(this, API.Localization.DontOwnGame, API.Localization.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.TxtAddGameId.Text = "";
            this._Games.Clear();
            this.AddGame(id, "normal");
            this.MenuFilterGames.IsChecked = true;
            this.RefreshGames();
        }

        private string GetGameImageUrl(uint id)
        {
            string candidate;
            var activeLanguage = API.LanguageManager.CurrentLanguage;

            candidate = this._SteamClient.SteamApps001.GetAppData(id, _($"small_capsule/{activeLanguage}"));
            if (string.IsNullOrEmpty(candidate) == false)
            {
                return _($"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{id}/{candidate}");
            }

            if (activeLanguage != "english")
            {
                candidate = this._SteamClient.SteamApps001.GetAppData(id, "small_capsule/english");
                if (string.IsNullOrEmpty(candidate) == false)
                {
                    return _($"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{id}/{candidate}");
                }
            }

            candidate = this._SteamClient.SteamApps001.GetAppData(id, "logo");
            if (string.IsNullOrEmpty(candidate) == false)
            {
                return _($"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{id}/{candidate}.jpg");
            }

            return null;
        }

        private void OnMenuOpenManagerClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is GameInfo info)
            {
                this.LaunchGameManager(info.Id, false);
            }
        }

        private void OnMenuStartIdleClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is GameInfo info)
            {
                this.LaunchGameManager(info.Id, true);
            }
        }

        private Point _startPointViewport;
        private double _startScrollOffset;
        private bool _isDraggingSelection = false;

        private void OnScrollViewerPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point mousePos = e.GetPosition(this.GameScrollViewer);

            // 1. If click is on the vertical scrollbar track/thumb on the right:
            if (mousePos.X >= this.GameScrollViewer.ViewportWidth)
            {
                return;
            }

            // 2. If click is on a Card, CheckBox, Button, or TextBox:
            var hit = e.OriginalSource as DependencyObject;
            while (hit != null && hit != this.GameScrollViewer)
            {
                if (hit is CheckBox || hit is Button || hit is TextBox || hit is Border border && border.Name == "CardBorder")
                {
                    return;
                }
                hit = VisualTreeHelper.GetParent(hit);
            }

            _startPointViewport = mousePos;
            _startScrollOffset = this.GameScrollViewer.VerticalOffset;
            _isDraggingSelection = true;
            this.GameScrollViewer.CaptureMouse();

            Canvas.SetLeft(this.SelectionBox, _startPointViewport.X);
            Canvas.SetTop(this.SelectionBox, _startPointViewport.Y);
            this.SelectionBox.Width = 0;
            this.SelectionBox.Height = 0;
            this.SelectionBox.Visibility = Visibility.Visible;
        }

        private void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_isDraggingSelection)
            {
                double newOffset = Math.Max(0, this.GameScrollViewer.VerticalOffset - e.Delta * 0.5);
                this.GameScrollViewer.ScrollToVerticalOffset(newOffset);
                UpdateSelectionBoxAndCards(e.GetPosition(this.GameScrollViewer));
                e.Handled = true;
            }
        }

        private void OnScrollViewerPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingSelection) return;

            Point currentPointViewport = e.GetPosition(this.GameScrollViewer);
            UpdateSelectionBoxAndCards(currentPointViewport);
        }

        private void UpdateSelectionBoxAndCards(Point currentPointViewport)
        {
            double scrollDelta = this.GameScrollViewer.VerticalOffset - _startScrollOffset;
            double anchoredStartY = _startPointViewport.Y - scrollDelta;

            double left = Math.Min(_startPointViewport.X, currentPointViewport.X);
            double top = Math.Min(anchoredStartY, currentPointViewport.Y);
            double width = Math.Abs(currentPointViewport.X - _startPointViewport.X);
            double height = Math.Abs(currentPointViewport.Y - anchoredStartY);

            Canvas.SetLeft(this.SelectionBox, left);
            Canvas.SetTop(this.SelectionBox, top);
            this.SelectionBox.Width = width;
            this.SelectionBox.Height = height;

            Rect selectionRect = new Rect(left, top, width, height);
            UpdateCardSelectionByRect(selectionRect);
        }

        private void OnScrollViewerPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingSelection)
            {
                _isDraggingSelection = false;
                this.GameScrollViewer.ReleaseMouseCapture();
                this.SelectionBox.Visibility = Visibility.Collapsed;
                this.UpdateSelectedCount();
            }
        }

        private void UpdateCardSelectionByRect(Rect selectionRect)
        {
            int currentCount = 0;
            const int maxLimit = 32;

            foreach (var item in this.ItemsGameCards.Items)
            {
                if (item is GameInfo info)
                {
                    var container = this.ItemsGameCards.ItemContainerGenerator.ContainerFromItem(item) as UIElement;
                    if (container != null)
                    {
                        try
                        {
                            Point cardPos = container.TransformToAncestor(this.GameScrollViewer).Transform(new Point(0, 0));
                            Rect cardRect = new Rect(cardPos, container.RenderSize);

                            if (selectionRect.IntersectsWith(cardRect))
                            {
                                if (currentCount < maxLimit)
                                {
                                    info.IsSelected = true;
                                    currentCount++;
                                }
                                else
                                {
                                    info.IsSelected = false;
                                }
                            }
                            else
                            {
                                info.IsSelected = false;
                            }
                        }
                        catch { }
                    }
                }
            }
            this.UpdateSelectedCount();
        }

        private void OnGameSelectionChanged(object sender, RoutedEventArgs e)
        {
            int selectedCount = this._Games.Values.Count(g => g.IsSelected);
            if (selectedCount > 32)
            {
                if (sender is CheckBox cb && cb.DataContext is GameInfo info)
                {
                    info.IsSelected = false;
                    string msg = API.Localization.Get("MaxSelectionLimitWarning", "Максимально можно выбрать до 32 игр одновременно!");
                    MessageBox.Show(this, msg, API.Localization.Information, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            this.UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            int selectedCount = this._Games.Values.Count(g => g.IsSelected);
            if (selectedCount > 0)
            {
                this.BtnFarmSelected.Visibility = Visibility.Visible;
                this.TxtBtnFarmSelected.Text = $"Фармить выбранные ({selectedCount}/32)";
            }
            else
            {
                this.BtnFarmSelected.Visibility = Visibility.Collapsed;
            }
        }

        private void OnFarmSelectedClick(object sender, RoutedEventArgs e)
        {
            var selectedGames = this._Games.Values.Where(g => g.IsSelected).ToList();
            if (selectedGames.Count == 0) return;

            var idleWindow = new IdleWindow(selectedGames)
            {
                Owner = this
            };
            idleWindow.ShowDialog();
        }

        private void OnGameCardClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is GameInfo info)
            {
                this.LaunchGameManager(info.Id, false);
            }
        }

        private void LaunchGameManager(uint appId, bool idleMode)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string exePath = Path.Combine(baseDir, "SAM.Game.exe");
                if (File.Exists(exePath) == false)
                {
                    exePath = Path.GetFullPath(Path.Combine(baseDir, "..", "SAM.Game.exe"));
                }

                string lang = API.LanguageManager.CurrentLanguage;
                string args = appId.ToString(CultureInfo.InvariantCulture) + (idleMode ? " -idle" : "") + $" -lang {lang}";

                ProcessStartInfo psi = new()
                {
                    FileName = File.Exists(exePath) ? exePath : "SAM.Game.exe",
                    Arguments = args,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(File.Exists(exePath) ? exePath : baseDir)
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, API.Localization.FailedToStartGameExe + "\n\n" + ex.Message, API.Localization.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
