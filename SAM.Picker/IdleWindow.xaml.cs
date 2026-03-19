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
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SAM.Picker
{
    public class IdleGameViewModel : INotifyPropertyChanged
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }

        private TimeSpan _farmingTime = TimeSpan.Zero;
        public TimeSpan FarmingTime
        {
            get => this._farmingTime;
            set
            {
                if (this._farmingTime != value)
                {
                    this._farmingTime = value;
                    OnPropertyChanged(nameof(FarmingTime));
                    OnPropertyChanged(nameof(FarmingTimeText));
                }
            }
        }

        public string FarmingTimeText => $"⏱️ Время: {this.FarmingTime:hh\\:mm\\:ss}";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal class MultiIdleSession : IDisposable
    {
        public uint AppId { get; }
        public API.Client Client { get; private set; }
        public bool IsActive { get; private set; }

        public MultiIdleSession(uint appId)
        {
            this.AppId = appId;
        }

        public bool Start()
        {
            try
            {
                this.Client = new API.Client();
                this.Client.Initialize(this.AppId);

                lock (API.Steam.SteamLock)
                {
                    try
                    {
                        this.Client.SteamFriends?.SetPersonaState(7); // k_EPersonaStateInvisible
                    }
                    catch { }
                }

                this.IsActive = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start idle session for {this.AppId}: {ex.Message}");
                this.IsActive = false;
                return false;
            }
        }

        public void RunCallbacks()
        {
            if (!this.IsActive || this.Client == null) return;
            try
            {
                lock (API.Steam.SteamLock)
                {
                    this.Client.RunCallbacks(false);
                }
            }
            catch { }
        }

        public void Dispose()
        {
            this.IsActive = false;
            if (this.Client != null)
            {
                try
                {
                    this.Client.Dispose();
                }
                catch { }
                this.Client = null;
            }
        }
    }

    public partial class IdleWindow : Window
    {
        private readonly List<IdleGameViewModel> _GamesList = new();
        private readonly List<MultiIdleSession> _Sessions = new();
        private readonly DispatcherTimer _CallbackTimer;
        private readonly DispatcherTimer _ClockTimer;
        private DateTime _startTime;

        internal IdleWindow(List<GameInfo> selectedGames)
        {
            this.InitializeComponent();

            try
            {
                var uri = new Uri("pack://application:,,,/SAM.ico", UriKind.Absolute);
                this.Icon = new BitmapImage(uri);
                this.ImgAppIcon.Source = this.Icon;
            }
            catch { }

            foreach (var g in selectedGames)
            {
                this._GamesList.Add(new IdleGameViewModel
                {
                    Id = g.Id,
                    Name = g.Name,
                    ImageUrl = g.ImageUrl
                });
            }

            this.ItemsFarmingGames.ItemsSource = this._GamesList;
            this.TxtGamesCount.Text = $"Фармится игр: {this._GamesList.Count}";

            this._startTime = DateTime.Now;

            // Start idle session for each game safely
            foreach (var vm in this._GamesList)
            {
                var session = new MultiIdleSession(vm.Id);
                if (session.Start())
                {
                    this._Sessions.Add(session);
                }
            }

            this.TxtGamesCount.Text = $"Фармится игр: {this._Sessions.Count} / {selectedGames.Count}";

            // Steam Callbacks Timer (100ms)
            this._CallbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            this._CallbackTimer.Tick += (s, e) =>
            {
                foreach (var ssn in this._Sessions)
                {
                    ssn.RunCallbacks();
                }
            };
            this._CallbackTimer.Start();

            // Clock / Session Timer (1 second)
            this._ClockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            this._ClockTimer.Tick += (s, e) =>
            {
                TimeSpan elapsed = DateTime.Now - this._startTime;
                this.TxtTotalTime.Text = $"⏱️ Время фарма: {elapsed:hh\\:mm\\:ss}";
                foreach (var vm in this._GamesList)
                {
                    vm.FarmingTime = elapsed;
                }
            };
            this._ClockTimer.Start();
        }

        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
            this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void OnCloseClick(object sender, RoutedEventArgs e) => this.Close();

        private void OnStopFarmingClick(object sender, RoutedEventArgs e) => this.Close();

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            this._CallbackTimer?.Stop();
            this._ClockTimer?.Stop();

            foreach (var session in this._Sessions)
            {
                session.Dispose();
            }
            this._Sessions.Clear();
        }
    }
}
