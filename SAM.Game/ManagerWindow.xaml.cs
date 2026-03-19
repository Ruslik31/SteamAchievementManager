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
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static SAM.Game.InvariantShorthand;
using APITypes = SAM.API.Types;

namespace SAM.Game
{
    public class AchievementViewModel : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string IconNormal { get; set; }
        public string IconLocked { get; set; }
        public bool IsHidden { get; set; }
        public int Permission { get; set; }
        public Visibility HiddenBadgeVisibility => this.IsHidden ? Visibility.Visible : Visibility.Collapsed;

        private float _globalPercentage = -1.0f;
        public float GlobalPercentage
        {
            get => this._globalPercentage;
            set
            {
                if (Math.Abs(this._globalPercentage - value) > 0.01f)
                {
                    this._globalPercentage = value;
                    OnPropertyChanged(nameof(GlobalPercentage));
                    OnPropertyChanged(nameof(RarityText));
                    OnPropertyChanged(nameof(RarityColor));
                    OnPropertyChanged(nameof(RarityBadgeVisibility));
                }
            }
        }

        public string RarityText => this.GlobalPercentage >= 0.0f ? $"{this.GlobalPercentage:0.0}%" : "";
        public string RarityColor => this.GlobalPercentage switch
        {
            <= 10.0f => "#f2c94c",
            <= 25.0f => "#bb6bd9",
            _ => "#8f98a0"
        };
        public Visibility RarityBadgeVisibility => this.GlobalPercentage >= 0.0f ? Visibility.Visible : Visibility.Collapsed;

        public bool OriginalIsAchieved { get; set; }
        public bool IsModified => this.IsAchieved != this.OriginalIsAchieved;

        private bool _isAchieved;
        public bool IsAchieved
        {
            get => this._isAchieved;
            set
            {
                if (this._isAchieved != value)
                {
                    this._isAchieved = value;
                    OnPropertyChanged(nameof(IsAchieved));
                    OnPropertyChanged(nameof(DisplayIcon));
                }
            }
        }

        public DateTime? UnlockTime { get; set; }
        public string UnlockTimeText => this.UnlockTime.HasValue ? this.UnlockTime.Value.ToString("g") : "";

        private BitmapImage _normalBitmap;
        public BitmapImage NormalBitmap
        {
            get => this._normalBitmap;
            set
            {
                if (this._normalBitmap != value)
                {
                    this._normalBitmap = value;
                    OnPropertyChanged(nameof(NormalBitmap));
                    OnPropertyChanged(nameof(DisplayIcon));
                }
            }
        }

        private BitmapImage _lockedBitmap;
        public BitmapImage LockedBitmap
        {
            get => this._lockedBitmap;
            set
            {
                if (this._lockedBitmap != value)
                {
                    this._lockedBitmap = value;
                    OnPropertyChanged(nameof(LockedBitmap));
                    OnPropertyChanged(nameof(DisplayIcon));
                }
            }
        }

        public BitmapImage DisplayIcon => this.IsAchieved ? (this.NormalBitmap ?? this.LockedBitmap) : (this.LockedBitmap ?? this.NormalBitmap);

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class ManagerWindow : Window
    {
        private readonly long _GameId;
        private readonly string _GameTitleName;
        private readonly API.Client _SteamClient;
        private readonly List<Stats.AchievementDefinition> _AchievementDefinitions = new();
        private readonly List<Stats.StatDefinition> _StatDefinitions = new();
        private readonly List<Stats.StatInfo> _Statistics = new();
        private readonly List<AchievementViewModel> _AchievementsList = new();
        private readonly List<AchievementViewModel> _FilteredAchievements = new();

        private readonly API.Callbacks.UserStatsReceived _UserStatsReceivedCallback;
        private readonly DispatcherTimer _CallbackTimer;
        private readonly List<AchievementViewModel> _IconQueue = new();
        private readonly WebClient _IconDownloader = new();

        private bool _sortNameAscending = true;
        private bool _sortRarityAscending = true;
        private bool _sortUnlockedAscending = true;

        private enum SortMode { Name, Rarity, UnlockedState, LockedState }
        private SortMode _currentSortMode = SortMode.Name;

        public ManagerWindow(long gameId, API.Client client, string language = null)
        {
            if (!string.IsNullOrEmpty(language))
            {
                API.LanguageManager.CurrentLanguage = language;
            }

            this._GameId = gameId;
            this._SteamClient = client;

            this.InitializeComponent();

            try
            {
                var uri = new Uri("pack://application:,,,/Blank.ico", UriKind.Absolute);
                this.Icon = new BitmapImage(uri);
                this.ImgAppIcon.Source = this.Icon;
            }
            catch { }

            this._IconDownloader.DownloadDataCompleted += this.OnIconDownloadCompleted;

            string name = this._SteamClient.SteamApps001.GetAppData((uint)this._GameId, "name");
            this._GameTitleName = name ?? this._GameId.ToString(CultureInfo.InvariantCulture);

            this._UserStatsReceivedCallback = client.CreateAndRegisterCallback<API.Callbacks.UserStatsReceived>();
            this._UserStatsReceivedCallback.OnRun += this.OnUserStatsReceived;

            this._CallbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            this._CallbackTimer.Tick += (s, e) => this._SteamClient.RunCallbacks(false);
            this._CallbackTimer.Start();

            this.InitializeLanguageDropdown();
            this.ApplyLocalization();

            this.RefreshStats();
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
                    this.LoadUserGameStatsSchema();
                    this.GetAchievements();
                    this.GetStatistics();
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
            string title = API.Localization.GameTitle(this._GameTitleName);
            this.TxtTitle.Text = title;
            this.Title = title;

            this.TxtBtnSave.Text = API.Localization.CommitChanges;
            this.BtnSave.ToolTip = API.Localization.CommitChangesToolTip;
            this.TxtBtnRefresh.Text = API.Localization.Refresh;
            this.BtnRefresh.ToolTip = API.Localization.RefreshToolTip;
            this.TxtBtnReset.Text = API.Localization.Reset;
            this.BtnReset.ToolTip = API.Localization.ResetToolTip;

            this.TabAchievements.Header = API.Localization.AchievementsTab;
            this.TabStatistics.Header = API.Localization.StatisticsTab;

            this.TxtLockAll.Text = "🔒 " + API.Localization.LockAll;
            this.TxtInvertAll.Text = "🔄 " + API.Localization.InvertAll;
            this.TxtUnlockAll.Text = "🔓 " + API.Localization.UnlockAll;

            this.MenuFilterAchievements.Header = "👁️ " + API.Localization.AchievementView;
            this.MenuFilterUnlocked.Header = API.Localization.ShowUnlocked;
            this.MenuFilterLocked.Header = API.Localization.ShowLocked;
            this.MenuFilterHidden.Header = API.Localization.ShowHidden;

            this.MenuSortAchievements.Header = "↕️ " + API.Localization.Get("SortHeader", "Сортировка");
            UpdateSortHeaders();

            this.ColStatName.Header = API.Localization.HeaderName;
            this.ColStatValue.Header = API.Localization.HeaderValue;
            this.ColStatExtra.Header = API.Localization.HeaderExtra;

            this.TxtEnableStats.Text = API.Localization.EnableStatsEditing;
            this.MenuLanguage.Header = "🌐 " + API.Localization.Language;
        }

        private void UpdateSortHeaders()
        {
            this.MenuSortName.Header = _sortNameAscending ? "По названию 🔼" : "По названию 🔽";
            this.MenuSortRarity.Header = _sortRarityAscending ? "По редкости 🔼" : "По редкости 🔽";
            this.MenuSortUnlockedState.Header = _sortUnlockedAscending ? "По состоянию (сначала полученные) 🔼" : "По состоянию (сначала полученные) 🔽";
            this.MenuSortLockedState.Header = "По состоянию (сначала заблокированные)";
        }

        private bool LoadUserGameStatsSchema()
        {
            string path;
            try
            {
                string fileName = _($"UserGameStatsSchema_{this._GameId}.bin");
                path = API.Steam.GetInstallPath();
                path = Path.Combine(path, "appcache", "stats", fileName);
                if (File.Exists(path) == false)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            var kv = KeyValue.LoadAsBinary(path);
            if (kv == null)
            {
                return false;
            }

            var currentLanguage = API.LanguageManager.CurrentLanguage;

            this._AchievementDefinitions.Clear();
            this._StatDefinitions.Clear();

            var statsNode = kv[this._GameId.ToString(CultureInfo.InvariantCulture)];
            if (statsNode == null || statsNode["stats"] == null || statsNode["stats"].Children == null)
            {
                return false;
            }

            var stats = statsNode["stats"];
            foreach (var stat in stats.Children)
            {
                if (stat == null)
                {
                    continue;
                }

                APITypes.UserStatType type;
                var typeNode = stat["type"];
                if (typeNode != null && typeNode.Value != null)
                {
                    if (Enum.TryParse(typeNode.Value.ToString(), true, out type) == false)
                    {
                        type = APITypes.UserStatType.Invalid;
                    }
                }
                else
                {
                    type = APITypes.UserStatType.Invalid;
                }

                if (type == APITypes.UserStatType.Invalid)
                {
                    var typeIntNode = stat["type_int"];
                    var rawType = typeIntNode != null && typeIntNode.Value != null ? int.TryParse(typeIntNode.Value.ToString(), out int parsed) ? parsed : 0 : 0;
                    type = (APITypes.UserStatType)rawType;
                }

                switch (type)
                {
                    case APITypes.UserStatType.Integer:
                        {
                            var id = stat["name"].AsString("");
                            string name = Stats.AchievementDefinition.GetLocalizedString(stat["display"]["name"], currentLanguage, id);

                            this._StatDefinitions.Add(new Stats.IntegerStatDefinition()
                            {
                                Id = stat["name"].AsString(""),
                                DisplayName = name,
                                MinValue = stat["min"].AsInteger(int.MinValue),
                                MaxValue = stat["max"].AsInteger(int.MaxValue),
                                DefaultValue = stat["default"].AsInteger(0),
                                IncrementOnly = stat["incrementonly"].AsBoolean(false),
                                Permission = stat["permission"].AsInteger(0),
                            });
                            break;
                        }

                    case APITypes.UserStatType.Float:
                    case APITypes.UserStatType.AverageRate:
                        {
                            var id = stat["name"].AsString("");
                            string name = Stats.AchievementDefinition.GetLocalizedString(stat["display"]["name"], currentLanguage, id);

                            this._StatDefinitions.Add(new Stats.FloatStatDefinition()
                            {
                                Id = stat["name"].AsString(""),
                                DisplayName = name,
                                MinValue = stat["min"].AsFloat(float.MinValue),
                                MaxValue = stat["max"].AsFloat(float.MaxValue),
                                DefaultValue = stat["default"].AsFloat(0.0f),
                                IncrementOnly = stat["incrementonly"].AsBoolean(false),
                                Permission = stat["permission"].AsInteger(0),
                            });
                            break;
                        }

                    case APITypes.UserStatType.Achievements:
                    case APITypes.UserStatType.GroupAchievements:
                        {
                            if (stat.Children != null)
                            {
                                foreach (var bits in stat.Children.Where(
                                    b => string.Compare(b.Name, "bits", StringComparison.InvariantCultureIgnoreCase) == 0))
                                {
                                    if (bits.Children == null)
                                    {
                                        continue;
                                    }

                                    foreach (var bit in bits.Children)
                                    {
                                        string id = bit["name"].AsString("");
                                        string name = Stats.AchievementDefinition.GetLocalizedString(bit["display"]["name"], currentLanguage, id);
                                        string desc = Stats.AchievementDefinition.GetLocalizedString(bit["display"]["desc"], currentLanguage, "");

                                        this._AchievementDefinitions.Add(new()
                                        {
                                            Id = id,
                                            Name = name,
                                            Description = desc,
                                            IconNormal = bit["display"]["icon"].AsString(""),
                                            IconLocked = bit["display"]["icon_gray"].AsString(""),
                                            IsHidden = bit["display"]["hidden"].AsBoolean(false),
                                            Permission = bit["permission"].AsInteger(0),
                                        });
                                    }
                                }
                            }
                            break;
                        }
                }
            }

            return true;
        }

        private void OnUserStatsReceived(APITypes.UserStatsReceived param)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (param.Result != 1)
                {
                    this.TxtStatus.Text = API.Localization.ErrorRetrievingStats(param.Result.ToString(CultureInfo.InvariantCulture));
                    return;
                }

                if (this.LoadUserGameStatsSchema() == false)
                {
                    this.TxtStatus.Text = API.Localization.FailedToLoadSchema;
                    return;
                }

                this.GetAchievements();
                this.GetStatistics();

                this.BtnSave.IsEnabled = true;
                this.TxtStatus.Text = API.Localization.RetrievedAchievementsAndStats(this._AchievementsList.Count, this._Statistics.Count);
            });
        }

        private void RefreshStats()
        {
            this._AchievementsList.Clear();
            this._Statistics.Clear();
            this.ItemsAchievements.ItemsSource = null;
            this.GridStatistics.ItemsSource = null;

            var steamId = this._SteamClient.SteamUser.GetSteamId();
            if (this._SteamClient.SteamUserStats.RequestUserStats(steamId) == API.CallHandle.Invalid)
            {
                MessageBox.Show(this, API.Localization.Error, API.Localization.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.TxtStatus.Text = API.Localization.RetrievingStatInfo;
        }

        private void GetAchievements()
        {
            this._AchievementsList.Clear();
            this._IconQueue.Clear();

            foreach (var def in this._AchievementDefinitions)
            {
                if (string.IsNullOrEmpty(def.Id)) continue;
                if (this._SteamClient.SteamUserStats.GetAchievementAndUnlockTime(def.Id, out bool isAchieved, out var unlockTime) == false)
                {
                    continue;
                }

                var vm = new AchievementViewModel
                {
                    Id = def.Id,
                    Name = def.Name,
                    Description = def.Description,
                    IconNormal = string.IsNullOrEmpty(def.IconNormal) ? null : def.IconNormal,
                    IconLocked = string.IsNullOrEmpty(def.IconLocked) ? def.IconNormal : def.IconLocked,
                    IsHidden = def.IsHidden,
                    Permission = def.Permission,
                    IsAchieved = isAchieved,
                    OriginalIsAchieved = isAchieved,
                    UnlockTime = isAchieved && unlockTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(unlockTime).LocalDateTime : null
                };

                this._AchievementsList.Add(vm);
                this._IconQueue.Add(vm);
            }

            this.UpdateAchievementCounters();
            this.FilterAchievements();
            this.DownloadNextIcon();
            this.FetchGlobalPercentages();
        }

        private void FetchGlobalPercentages()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using WebClient client = new();
                client.Headers[HttpRequestHeader.UserAgent] = "Valve/Steam";
                string url = _($"https://api.steampowered.com/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v0002/?gameid={this._GameId}");
                client.DownloadStringCompleted += (s, e) =>
                {
                    if (e.Error != null || string.IsNullOrEmpty(e.Result)) return;
                    try
                    {
                        var json = e.Result;
                        var dict = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

                        var matches = System.Text.RegularExpressions.Regex.Matches(
                            json,
                            @"""name""\s*:\s*""(?<name>[^""]+)""\s*,\s*""percent""\s*:\s*""?(?<percent>[\d\.\-]+)""?",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            string name = match.Groups["name"].Value;
                            string pctStr = match.Groups["percent"].Value;
                            if (float.TryParse(pctStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float pct))
                            {
                                dict[name] = pct;
                            }
                        }

                        this.Dispatcher.Invoke(() =>
                        {
                            foreach (var ach in this._AchievementsList)
                            {
                                if (dict.TryGetValue(ach.Id, out float pct))
                                {
                                    ach.GlobalPercentage = pct;
                                }
                            }
                            this.FilterAchievements();
                        });
                    }
                    catch { }
                };
                client.DownloadStringAsync(new Uri(url));
            }
            catch { }
        }

        private void UpdateAchievementCounters()
        {
            int hiddenCount = this._AchievementsList.Count(a => a.IsHidden);
            int normalCount = this._AchievementsList.Count - hiddenCount;

            this.TxtNormalCount.Text = normalCount.ToString(CultureInfo.InvariantCulture);
            this.TxtHiddenCount.Text = hiddenCount.ToString(CultureInfo.InvariantCulture);
        }

        private void GetStatistics()
        {
            this._Statistics.Clear();
            foreach (var stat in this._StatDefinitions)
            {
                if (string.IsNullOrEmpty(stat.Id) == true)
                {
                    continue;
                }

                if (stat is Stats.IntegerStatDefinition intStat)
                {
                    if (this._SteamClient.SteamUserStats.GetStatValue(intStat.Id, out int value) == false)
                    {
                        continue;
                    }
                    this._Statistics.Add(new Stats.IntStatInfo()
                    {
                        Id = intStat.Id,
                        DisplayName = intStat.DisplayName,
                        IntValue = value,
                        OriginalValue = value,
                        Permission = intStat.Permission,
                    });
                }
                else if (stat is Stats.FloatStatDefinition floatStat)
                {
                    if (this._SteamClient.SteamUserStats.GetStatValue(floatStat.Id, out float value) == false)
                    {
                        continue;
                    }
                    this._Statistics.Add(new Stats.FloatStatInfo()
                    {
                        Id = floatStat.Id,
                        DisplayName = floatStat.DisplayName,
                        FloatValue = value,
                        OriginalValue = value,
                        Permission = floatStat.Permission,
                    });
                }
            }
            this.GridStatistics.ItemsSource = null;
            this.GridStatistics.ItemsSource = this._Statistics;
        }

        private void FilterAchievements()
        {
            string search = this.TxtSearchAchievement.Text.Length > 0 ? this.TxtSearchAchievement.Text : null;
            bool wantUnlocked = this.MenuFilterUnlocked.IsChecked;
            bool wantLocked = this.MenuFilterLocked.IsChecked;
            bool wantHidden = this.MenuFilterHidden.IsChecked;

            var list = new List<AchievementViewModel>();
            foreach (var ach in this._AchievementsList)
            {
                bool showAchievement = false;

                if (ach.IsHidden)
                {
                    if (wantHidden)
                    {
                        if ((wantUnlocked && wantLocked) || (wantUnlocked && ach.IsAchieved) || (wantLocked && !ach.IsAchieved) || (!wantUnlocked && !wantLocked))
                        {
                            showAchievement = true;
                        }
                    }
                }
                else
                {
                    if (ach.IsAchieved && wantUnlocked) showAchievement = true;
                    if (!ach.IsAchieved && wantLocked) showAchievement = true;
                }

                if (!showAchievement) continue;

                if (search != null)
                {
                    if ((ach.Name?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) < 0 &&
                        (ach.Description?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                    {
                        continue;
                    }
                }
                list.Add(ach);
            }

            if (this.MenuSortName != null && this.MenuSortName.IsChecked)
            {
                list = _sortNameAscending
                    ? list.OrderBy(a => a.Name).ToList()
                    : list.OrderByDescending(a => a.Name).ToList();
            }
            else if (this.MenuSortRarity != null && this.MenuSortRarity.IsChecked)
            {
                list = _sortRarityAscending
                    ? list.OrderBy(a => a.GlobalPercentage < 0 ? 999.0f : a.GlobalPercentage).ThenBy(a => a.Name).ToList()
                    : list.OrderByDescending(a => a.GlobalPercentage >= 0 ? a.GlobalPercentage : -1.0f).ThenBy(a => a.Name).ToList();
            }
            else if (this.MenuSortUnlockedState != null && this.MenuSortUnlockedState.IsChecked)
            {
                // Unlocked first, sorted by unlock time (ascending or descending)
                list = _sortUnlockedAscending
                    ? list.OrderByDescending(a => a.IsAchieved).ThenBy(a => a.UnlockTime ?? DateTime.MinValue).ThenBy(a => a.Name).ToList()
                    : list.OrderByDescending(a => a.IsAchieved).ThenByDescending(a => a.UnlockTime ?? DateTime.MinValue).ThenBy(a => a.Name).ToList();
            }
            else if (this.MenuSortLockedState != null && this.MenuSortLockedState.IsChecked)
            {
                // Locked achievements first
                list = list.OrderBy(a => a.IsAchieved ? 1 : 0).ThenBy(a => a.Name).ToList();
            }
            else
            {
                list = list.OrderBy(a => a.Name).ToList();
            }

            this._FilteredAchievements.Clear();
            this._FilteredAchievements.AddRange(list);

            this.ItemsAchievements.ItemsSource = null;
            this.ItemsAchievements.ItemsSource = this._FilteredAchievements;
        }

        private void OnAchievementSearchChanged(object sender, TextChangedEventArgs e)
        {
            this.TxtSearchAchPlaceholder.Visibility = string.IsNullOrEmpty(this.TxtSearchAchievement.Text) ? Visibility.Visible : Visibility.Collapsed;
            this.FilterAchievements();
        }

        private void OnAchievementFilterMenuClick(object sender, RoutedEventArgs e) => this.FilterAchievements();

        private void OnSortMenuClick(object sender, RoutedEventArgs e)
        {
            if (sender == this.MenuSortName)
            {
                if (_currentSortMode == SortMode.Name)
                {
                    _sortNameAscending = !_sortNameAscending;
                }
                else
                {
                    _currentSortMode = SortMode.Name;
                    _sortNameAscending = true;
                }
                this.MenuSortName.IsChecked = true;
                this.MenuSortRarity.IsChecked = false;
                this.MenuSortUnlockedState.IsChecked = false;
                this.MenuSortLockedState.IsChecked = false;
            }
            else if (sender == this.MenuSortRarity)
            {
                if (_currentSortMode == SortMode.Rarity)
                {
                    _sortRarityAscending = !_sortRarityAscending;
                }
                else
                {
                    _currentSortMode = SortMode.Rarity;
                    _sortRarityAscending = true;
                }
                this.MenuSortName.IsChecked = false;
                this.MenuSortRarity.IsChecked = true;
                this.MenuSortUnlockedState.IsChecked = false;
                this.MenuSortLockedState.IsChecked = false;
            }
            else if (sender == this.MenuSortUnlockedState)
            {
                if (_currentSortMode == SortMode.UnlockedState)
                {
                    _sortUnlockedAscending = !_sortUnlockedAscending;
                }
                else
                {
                    _currentSortMode = SortMode.UnlockedState;
                    _sortUnlockedAscending = true;
                }
                this.MenuSortName.IsChecked = false;
                this.MenuSortRarity.IsChecked = false;
                this.MenuSortUnlockedState.IsChecked = true;
                this.MenuSortLockedState.IsChecked = false;
            }
            else if (sender == this.MenuSortLockedState)
            {
                _currentSortMode = SortMode.LockedState;
                this.MenuSortName.IsChecked = false;
                this.MenuSortRarity.IsChecked = false;
                this.MenuSortUnlockedState.IsChecked = false;
                this.MenuSortLockedState.IsChecked = true;
            }

            UpdateSortHeaders();
            this.FilterAchievements();
        }

        private void OnLockAllClick(object sender, RoutedEventArgs e)
        {
            foreach (var ach in this._AchievementsList) ach.IsAchieved = false;
        }

        private void OnInvertAllClick(object sender, RoutedEventArgs e)
        {
            foreach (var ach in this._AchievementsList) ach.IsAchieved = !ach.IsAchieved;
        }

        private void OnUnlockAllClick(object sender, RoutedEventArgs e)
        {
            foreach (var ach in this._AchievementsList) ach.IsAchieved = true;
        }

        private void OnAchievementCheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is AchievementViewModel vm)
            {
                if ((vm.Permission & 3) != 0)
                {
                    MessageBox.Show(this, API.Localization.ProtectedAchievementNotice, API.Localization.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    vm.IsAchieved = !vm.IsAchieved;
                }
            }
        }

        private void DownloadNextIcon()
        {
            if (this._IconQueue.Count == 0)
            {
                this.TxtDownloadStatus.Text = "";
                return;
            }

            var vm = this._IconQueue[0];
            this._IconQueue.RemoveAt(0);

            string iconName = vm.IsAchieved ? vm.IconNormal : vm.IconLocked;
            if (string.IsNullOrEmpty(iconName))
            {
                this.DownloadNextIcon();
                return;
            }

            string url = _($"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{this._GameId}/{iconName}");

            byte[] cachedBytes = API.ImageCache.GetOrDownloadImage(url);
            if (cachedBytes != null && cachedBytes.Length > 0)
            {
                try
                {
                    using MemoryStream ms = new(cachedBytes);
                    BitmapImage bmp = new();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();

                    if (vm.IsAchieved) vm.NormalBitmap = bmp;
                    else vm.LockedBitmap = bmp;
                }
                catch { }
                this.DownloadNextIcon();
                return;
            }

            this.TxtDownloadStatus.Text = API.Localization.DownloadingIcons(this._IconQueue.Count);
            if (this._IconDownloader.IsBusy == false)
            {
                this._IconDownloader.DownloadDataAsync(new Uri(url), vm);
            }
        }

        private void OnIconDownloadCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            if (e.Error == null && e.Cancelled == false && e.UserState is AchievementViewModel vm)
            {
                try
                {
                    this.TxtDownloadStatus.Text = API.Localization.DownloadingIcons(this._IconQueue.Count);
                }
                catch { }
            }
            this.DownloadNextIcon();
        }

        private void OnStoreClick(object sender, RoutedEventArgs e)
        {
            int achCount = 0;
            foreach (var ach in this._AchievementsList)
            {
                if (ach.IsModified)
                {
                    if (this._SteamClient.SteamUserStats.SetAchievement(ach.Id, ach.IsAchieved) == false)
                    {
                        MessageBox.Show(this, API.Localization.ErrorSettingState(ach.Id), API.Localization.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                        this.RefreshStats();
                        return;
                    }
                    achCount++;
                }
            }

            int statCount = 0;
            foreach (var stat in this._Statistics.Where(s => s.IsModified))
            {
                bool success = stat switch
                {
                    Stats.IntStatInfo i => this._SteamClient.SteamUserStats.SetStatValue(i.Id, i.IntValue),
                    Stats.FloatStatInfo f => this._SteamClient.SteamUserStats.SetStatValue(f.Id, f.FloatValue),
                    _ => false
                };

                if (success == false)
                {
                    MessageBox.Show(this, API.Localization.ErrorSettingValue(stat.Id), API.Localization.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    this.RefreshStats();
                    return;
                }
                statCount++;
            }

            if (this._SteamClient.SteamUserStats.StoreStats() == false)
            {
                MessageBox.Show(this, API.Localization.ErrorStoringAborting, API.Localization.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                this.RefreshStats();
                return;
            }

            MessageBox.Show(this, API.Localization.StoredAchievementsAndStats(achCount, statCount), API.Localization.Information, MessageBoxButton.OK, MessageBoxImage.Information);
            this.RefreshStats();
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e) => this.RefreshStats();

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, API.Localization.ConfirmResetStats, API.Localization.Question, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            bool resetAchievements = MessageBox.Show(this, API.Localization.ConfirmResetAchievementsToo, API.Localization.Question, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

            if (MessageBox.Show(this, API.Localization.ConfirmReallySure, API.Localization.Warning, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            if (this._SteamClient.SteamUserStats.ResetAllStats(resetAchievements) == false)
            {
                MessageBox.Show(this, API.Localization.Error, API.Localization.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.RefreshStats();
        }

        private void OnEnableStatsChecked(object sender, RoutedEventArgs e)
        {
            this.ColStatValue.IsReadOnly = this.ChkEnableStats.IsChecked != true;
        }
    }
}
