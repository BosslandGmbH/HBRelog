/*
Copyright 2012 HighVoltz

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using HighVoltz.HBRelog.Settings;
using HighVoltz.HBRelog.Tasks;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Windows.Threading;

namespace HighVoltz.HBRelog
{
    /// <summary>
    ///   Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Timer _autoCloseTimer;
        private DispatcherTimer _minVersionCheckTimer;

        public MainWindow()
        {
            //InitializeComponent();
            Instance = this;
            LoadStyle();
            var resourceLocater = new Uri("/HBRelog;component/mainwindow.xaml", UriKind.Relative);
            Application.LoadComponent(this, resourceLocater);
        }

        public static MainWindow Instance { get; private set; }

        protected override void OnClosing(CancelEventArgs e)
        {
            var saveTimespan = HbRelogManager.Settings.SaveCompleteTimeSpan;
            if (HbRelogManager.Settings != null && saveTimespan != TimeSpan.FromSeconds(0))
            {
                e.Cancel = true;
                Title = "HBRelog : Waiting for settings to save before exiting";
                _autoCloseTimer = new Timer(
                    state =>
                    {
                        _autoCloseTimer.Dispose();
                        _autoCloseTimer = null;
                        Dispatcher.Invoke((Action)(Close));
                    },
                    null,
                    saveTimespan,
                    TimeSpan.FromMilliseconds(-1));
            }
            else
                base.OnClosing(e);
        }


        public void LoadStyle()
        {
            Uri resourceLocater = HbRelogManager.Settings.UseDarkStyle ? new Uri("/styles/ExpressionDark.xaml", UriKind.Relative) : new Uri("/styles/BureauBlue.xaml", UriKind.Relative);
            var rDict = new ResourceDictionary { Source = resourceLocater };
            Resources.MergedDictionaries.Clear();
            Resources.MergedDictionaries.Add(rDict);
        }

        private void AddAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var character = new CharacterProfile();
            if (AccountGrid.SelectedItem != null)
                character.Settings = (ProfileSettings)((CharacterProfile)AccountGrid.SelectedItem).Settings.ShadowCopy();

            if (character.Settings != null)
            {
                HbRelogManager.Settings.CharacterProfiles.Add(character);
                AccountGrid.SelectedItem = character;
                EditAccount(character.Settings);
            }
        }

        private void EditAccountButtonClick(object sender, RoutedEventArgs e)
        {
            if (AccountGrid.SelectedItem != null)
                EditAccount(((CharacterProfile)AccountGrid.SelectedItem).Settings);
        }

        private void AcountConfigCloseButtonClick(object sender, RoutedEventArgs e)
        {
            var ani = new DoubleAnimation(0, new Duration(TimeSpan.Parse("0:0:0.4"))) { AccelerationRatio = 0.7 };
            AccountConfigGrid.BeginAnimation(WidthProperty, ani);
            AccountConfig.IsEditing = false;
            HbRelogManager.Settings.Save();
        }

        private void EditAccount(ProfileSettings charSettings)
        {
            if (charSettings != null)
            {
	            var width = AccountConfigGridColumn.ActualWidth ;
				var ani = new DoubleAnimation(width, new Duration(TimeSpan.Parse("0:0:0.4"))) { DecelerationRatio = 0.7 };
                AccountConfigGrid.BeginAnimation(WidthProperty, ani);
                AccountConfig.EditAccount(charSettings);
            }
        }

        private void DeleteAccountButtonClick(object sender, RoutedEventArgs e)
        {
            if (AccountGrid.SelectedIndex >= 0 && MessageBox.Show("Are you sure you want to delete selected profile/s", "Account deletion", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                // delete all the selected profiles from the data source. 
                for (int i = AccountGrid.SelectedItems.Count - 1; i >= 0; i--)
                {
                    var character = (CharacterProfile)AccountGrid.SelectedItems[i];
                    HbRelogManager.Settings.CharacterProfiles.Remove(character);
                }
                HbRelogManager.Settings.Save();
            }
        }

        private void StartSelButtonClick(object sender, RoutedEventArgs e)
        {
            foreach (CharacterProfile character in AccountGrid.SelectedItems)
            {
                if ((!character.IsRunning || character.IsPaused) && character.Settings.IsEnabled)
                    character.Start();
            }
        }

        private void StopSelButtonClick(object sender, RoutedEventArgs e)
        {
            foreach (CharacterProfile character in AccountGrid.SelectedItems)
            {
                if (character.IsRunning)
                    character.Stop();
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            OptionsTab.IsSelected = false;
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            // add one to Revision because it uses current revision and we want this to use the next revision number.
            version = new Version(version.Major, version.Minor, version.Build);
            Log.Write("HBRelog Version {0}", version);
            Log.Write("******* Settings ******** ");
            Log.Write("\t{0,-30} {1}", "Auto AcceptTosEula:", HbRelogManager.Settings.AutoAcceptTosEula);
            Log.Write("\t{0,-30} {1}", "Auto Start:", HbRelogManager.Settings.AutoStart);
            Log.Write("\t{0,-30} {1}", "Auto Update HB:", HbRelogManager.Settings.AutoUpdateHB);
            Log.Write("\t{0,-30} {1}", "Check Hb's Responsiveness:", HbRelogManager.Settings.CheckHbResponsiveness);
            Log.Write("\t{0,-30} {1}", "Check Realm Status:", HbRelogManager.Settings.CheckRealmStatus);
            Log.Write("\t{0,-30} {1}", "HB Delay:", HbRelogManager.Settings.HBDelay);
            Log.Write("\t{0,-30} {1}", "Login Delay:", HbRelogManager.Settings.LoginDelay);
            Log.Write("\t{0,-30} {1}", "Store Settings Locally", HbRelogManager.Settings.UseLocalSettings);
			Log.Write("\t{0,-30} {1}", "Set GameWindow Title:", HbRelogManager.Settings.SetGameWindowTitle);
            Log.Write("\t{0,-30} {1}", "Wow Start Delay:", HbRelogManager.Settings.WowDelay);

            if (!Launcher.Helpers.IsUacEnabled)
                Log.Write(System.Windows.Media.Colors.Red, $"UAC is disabled. It's highly recommended that UAC is enabled to increase security.");


            // prevent user from starting any profiles until after version check is complete
            Log.Write("Checking minimum required version.");
            StartButton.IsEnabled = false;
            await CheckMinimumRequiredVersion();

            _minVersionCheckTimer = new DispatcherTimer();
            _minVersionCheckTimer.Tick += MinVersionCheckTimer_Tick;
            _minVersionCheckTimer.Interval = TimeSpan.FromMinutes(5);
            _minVersionCheckTimer.Start();

            StartButton.IsEnabled = true;

            string rawProfilesToStart;

            if (Program.GetCommandLineArgument("AutoStart", out rawProfilesToStart) 
                || HbRelogManager.Settings.AutoStart)
            {
                var profilesToStart = !string.IsNullOrEmpty(rawProfilesToStart)
                    ? new HashSet<string>(rawProfilesToStart.Trim().Split(';', '|', ',').Select(a => a.Trim()), StringComparer.OrdinalIgnoreCase)
                    : null;

                foreach (CharacterProfile character in AccountGrid.Items)
                {
                    if (profilesToStart == null)
                    {
                        if (character.Settings.IsEnabled)
                            character.Start();
                    }
                    else if (profilesToStart.Contains(character.Settings.ProfileName))
                    {
                        character.Start();
                    }
                }
            }
        }

        private void AccountGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AccountConfig != null && AccountGrid != null && AccountConfig.IsEditing && AccountGrid.SelectedItem != null)
            {
                EditAccount(((CharacterProfile)AccountGrid.SelectedItem).Settings);
            }
        }

        private void AccountGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AccountGrid.SelectedItem != null)
                EditAccount(((CharacterProfile)AccountGrid.SelectedItem).Settings);
        }

        private void PauseButtonClick(object sender, RoutedEventArgs e)
        {
            foreach (CharacterProfile character in AccountGrid.SelectedItems)
            {
                if (character.IsRunning)
                {
                    character.Status = "Paused";
                    character.Pause();
                }
            }
        }

        private void SelectAllButtonClick(object sender, RoutedEventArgs e)
        {
            // my debug button :)
            if (Environment.UserName == "highvoltz")
            {
            }
            AccountGrid.SelectAll();
        }

        private void StartThumbButtonClick(object sender, EventArgs e)
        {
            foreach (CharacterProfile character in HbRelogManager.Settings.CharacterProfiles)
            {
                if ((!character.IsRunning || character.IsPaused) && character.Settings.IsEnabled)
                    character.Start();
            }
        }

        private void PauseThumbButtonClick(object sender, EventArgs e)
        {
            foreach (CharacterProfile character in HbRelogManager.Settings.CharacterProfiles)
            {
                if (character.Settings.IsEnabled)
                    character.Pause();
            }
        }

        private void StopThumbButtonClick(object sender, EventArgs e)
        {
            foreach (CharacterProfile character in HbRelogManager.Settings.CharacterProfiles)
            {
                if (character.IsRunning && character.Settings.IsEnabled)
                    character.Stop();
            }
        }

        private void ProfileEnabledCheckBoxChecked(object sender, RoutedEventArgs e)
        {
            var settings = (ProfileSettings)((CheckBox)sender).Tag;
            settings.IsEnabled = true;
        }

        private void ProfileEnabledCheckBoxUnchecked(object sender, RoutedEventArgs e)
        {
            var settings = (ProfileSettings)((CheckBox)sender).Tag;
            settings.IsEnabled = false;
        }

        // Options And Log Tab Control
        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var tabItem = (TabItem)sender;
            if (e.OriginalSource != tabItem.Header)
                return;
            double destHeight = 200;
            if (tabItem.IsSelected)
            {
                destHeight = 20;
                OptionsAndLogTabCtrl.SelectedIndex = -1;
            }
            else
            {
                OptionsAndLogTabCtrl.SelectedItem = tabItem;
            }
            e.Handled = true;
            var ani = new DoubleAnimation(destHeight, new Duration(TimeSpan.Parse("0:0:0.300"))) { DecelerationRatio = 0.7 };
            OptionsAndLogGrid.BeginAnimation(HeightProperty, ani);
        }

        private void SkipTaskMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var profile = (CharacterProfile)((MenuItem)sender).Tag;
            BMTask currentTask = profile.TaskManager.Tasks.FirstOrDefault(t => !t.IsDone);
            if (currentTask != null)
            {
                currentTask.Stop();
            }
        }

        private void BringHbToForegroundMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var hbManager = ((CharacterProfile)((MenuItem)sender).Tag).TaskManager.HonorbuddyManager;
            if (hbManager.IsRunning && !hbManager.BotProcess.HasExitedSafe())
            {
                NativeMethods.ShowWindow(hbManager.BotProcess.MainWindowHandle, NativeMethods.ShowWindowCommands.Restore);
                NativeMethods.SetForegroundWindow(hbManager.BotProcess.MainWindowHandle);
            }
        }

        private void KillHbMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var hbManager = ((CharacterProfile)((MenuItem)sender).Tag).TaskManager.HonorbuddyManager;
            hbManager.CloseBotProcess();
        }


        private void BringWoWToForeground_Click(object sender, RoutedEventArgs e)
        {
            var wowManager = ((CharacterProfile)((MenuItem)sender).Tag).TaskManager.WowManager;
            Utility.BringWindowIntoFocus(wowManager.GameWindow, wowManager.Profile);
        }

        private void DataGridRowContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var row = (DataGridRow)sender;
            var profile = (CharacterProfile)row.Item;
            var hbManager = profile.TaskManager.HonorbuddyManager;
            var wowManager = profile.TaskManager.WowManager;

            // only show the skip task menu item if profile has > 1 task and startup sequence is complete
            var skipTaskMenuItem = (MenuItem)row.ContextMenu.Items[0];
            BMTask task = profile.TaskManager.Tasks.FirstOrDefault(t => t.IsRunning);
            if (profile.TaskManager.Tasks.Count > 1 && profile.TaskManager.StartupSequenceIsComplete && task != null)
                skipTaskMenuItem.Visibility = Visibility.Visible;
            else
                skipTaskMenuItem.Visibility = Visibility.Collapsed;

            //only show the 'Bring HB to Foreground' task menu item if hb is running.
            var bringHbToForegroundTaskMenuItem = (MenuItem)row.ContextMenu.Items[1];
            var killHBMenu = (MenuItem)row.ContextMenu.Items[2];

            if (hbManager.IsRunning && hbManager.BotProcess != null && !hbManager.BotProcess.HasExitedSafe())
            {
                bringHbToForegroundTaskMenuItem.Visibility = Visibility.Visible;
                killHBMenu.Visibility = Visibility.Visible;
            }
            else
            {
                bringHbToForegroundTaskMenuItem.Visibility = Visibility.Collapsed;
                killHBMenu.Visibility = Visibility.Collapsed;
            }

            var bringWowToForegroundMenu = (MenuItem)row.ContextMenu.Items[3];
            bringWowToForegroundMenu.Visibility = wowManager.IsRunning && wowManager.GameWindow != IntPtr.Zero 
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private const string s_minRequireVersionUrl = "https://raw.githubusercontent.com/BosslandGmbH/HBRelog/master/MinRequiredVersion.txt";

        private async void MinVersionCheckTimer_Tick(object sender, EventArgs e)
        {
            await CheckMinimumRequiredVersion();
        }

        private async Task CheckMinimumRequiredVersion()
        {
            var webClient = new WebClient();
            var text = await webClient.DownloadStringTaskAsync(s_minRequireVersionUrl);
            if (string.IsNullOrEmpty(text))
                throw new InvalidDataException("Remote MinRequiredVersion.txt is empty.");
            text = text.Trim();
            var firstSpaceI = text.IndexOf(" ");
            var versionTxt = firstSpaceI == -1 ? text : text.Substring(0, firstSpaceI);
            var minVersion = Version.Parse(versionTxt);
            var msg = firstSpaceI > 0 ? text.Substring(firstSpaceI + 1) : null;
            var currentVersion = Version.Parse(Process.GetCurrentProcess().VersionString());
            if (currentVersion.Major >= minVersion.Major && currentVersion.Minor >= minVersion.Minor && currentVersion.Build >= minVersion.Build)
                return;

            foreach (CharacterProfile profile in AccountGrid.Items)
            {
                if (profile.IsRunning)
                    profile.Stop();
            }

            msg = $"Your HBRelog version {currentVersion} is no longer compatible or safe. " +
                $"You need upgrade to version {minVersion} or higher.{(msg != null ?" " + msg : "")}";

            MessageBox.Show(msg, "Incompatible HBRelog Version");
            Process.GetCurrentProcess().Kill();
        }
    }
}