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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Windows.Resources;
using HighVoltz.HBRelog;
using HighVoltz.HBRelog.Settings;

namespace HighVoltz.HBRelog
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            //InitializeComponent();
            var resourceLocater = new Uri("/HBRelog;component/mainwindow.xaml", UriKind.Relative);
            Application.LoadComponent(this, resourceLocater);
            Instance = this;
        }

        public static MainWindow Instance { get; private set; }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            HBRelogManager.Settings.Save();
        }

        private void AddAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var character = new CharacterProfile();
            if (AccountGrid.SelectedItem != null)
                character.Settings = ((CharacterProfile)AccountGrid.SelectedItem).Settings.ShadowCopy();

            if (character.Settings != null)
            {
                HBRelogManager.Settings.CharacterProfiles.Add(character);
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
            DoubleAnimation ani = new DoubleAnimation(0, new Duration(TimeSpan.Parse("0:0:0.4")));
            ani.AccelerationRatio = 0.7;
            AccountConfigGrid.BeginAnimation(Grid.WidthProperty, ani);
            AccountConfig.IsEditing = false;
            HBRelogManager.Settings.Save();
        }

        private void EditAccount(ProfileSettings charSettings)
        {
            if (charSettings != null)
            {
                DoubleAnimation ani = new DoubleAnimation(255, new Duration(TimeSpan.Parse("0:0:0.4")));
                ani.DecelerationRatio = 0.7;
                AccountConfigGrid.BeginAnimation(Grid.WidthProperty, ani);
                AccountConfig.EditAccount(charSettings);
            }
        }

        private void DeleteAccountButtonClick(object sender, RoutedEventArgs e)
        {
            if (AccountGrid.SelectedIndex >= 0 &&
                MessageBox.Show("Are you sure you want to delete selected profile/s", "Account deletion",
                                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                // delete all the selected profiles from the data source. 
                for (int i = AccountGrid.SelectedItems.Count - 1; i >= 0; i--)
                {
                    var character = (CharacterProfile)AccountGrid.SelectedItems[i];
                    HBRelogManager.Settings.CharacterProfiles.Remove(character);
                }
                HBRelogManager.Settings.Save();
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Log.Write("HBRelog Version {0}", Assembly.GetExecutingAssembly().GetName().Version);
            if (Program.AutoStart)
            {
                foreach (CharacterProfile character in AccountGrid.Items)
                {
                    if ((!character.IsRunning || character.IsPaused) && character.Settings.IsEnabled)
                        character.Start();
                }
            }
        }

        private void AccountGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AccountConfig != null && AccountGrid != null &&
                AccountConfig.IsEditing && AccountGrid.SelectedItem != null)
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
            foreach (CharacterProfile character in HBRelogManager.Settings.CharacterProfiles)
            {
                if ((!character.IsRunning || character.IsPaused) && character.Settings.IsEnabled)
                    character.Start();
            }
        }

        private void PauseThumbButtonClick(object sender, EventArgs e)
        {
            foreach (CharacterProfile character in HBRelogManager.Settings.CharacterProfiles)
            {
                if (character.Settings.IsEnabled)
                    character.Pause();
            }
        }

        private void StopThumbButtonClick(object sender, EventArgs e)
        {
            foreach (CharacterProfile character in HBRelogManager.Settings.CharacterProfiles)
            {
                if (character.IsRunning && character.Settings.IsEnabled)
                    character.Stop();
            }
        }

        private void ProfileEnabledCheckBoxChecked(object sender, RoutedEventArgs e)
        {
            ProfileSettings settings = (ProfileSettings)((CheckBox)sender).Tag;
            settings.IsEnabled = true;
        }

        private void ProfileEnabledCheckBoxUnchecked(object sender, RoutedEventArgs e)
        {
            ProfileSettings settings = (ProfileSettings)((CheckBox)sender).Tag;
            settings.IsEnabled = false;
        }

    }
}