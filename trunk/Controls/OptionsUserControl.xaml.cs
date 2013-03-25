using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HighVoltz.HBRelog.Settings;
using Microsoft.Win32;

namespace HighVoltz.HBRelog.Controls
{
    /// <summary>
    /// Interaction logic for OptionsUserControl.xaml
    /// </summary>
    public partial class OptionsUserControl : UserControl
    {
        public OptionsUserControl()
        {
            InitializeComponent();
        }

        private void DarkStyleCheckCheckedChanged(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.LoadStyle();
        }

        private void ImportSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = ".xml|*.xml",
                DefaultExt = ".xml",
                Title = "Browse to and select your exported settings file"
            };
            if (ofd.ShowDialog() == true)
            {
                HbRelogManager.Settings = GlobalSettings.Import(ofd.FileName);
                // re-assign the data context for main window.
                MainWindow.Instance.DataContext = HbRelogManager.Settings.CharacterProfiles;

                var options = (Controls.OptionsUserControl)MainWindow.Instance.FindName("HbrelogOptions");
                options.DataContext = HbRelogManager.Settings;

                var accountConfig = (Controls.AccountConfigUserControl) MainWindow.Instance.FindName("AccountConfig");
                var taskList = (ListBox)accountConfig.FindName("ProfileTaskList");

                //HbRelogManager.Settings.CharacterProfiles[0].Tasks.
                //ItemsSource="{Binding CharacterProfiles/Tasks, Source={x:Static HighVoltz:HbRelogManager.Settings}}
                //taskList.ItemsSource = HbRelogManager.Settings.CharacterProfiles;
                //taskList.ItemsSource = HbRelogManager.Settings.CharacterProfiles;

                // var a = (CheckBox) MainWindow.Instance.FindName("ProfileEnabledCheckBox");
                // a.DataContext = HbRelogManager.Settings.CharacterProfiles[0].Settings
                HbRelogManager.Settings.QueueSave();
            }
        }

        private void ExportSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog()
            {
                Filter = ".xml|*.xml",
                DefaultExt = ".xml",
                Title = "Export settings to file"
            };
            if (sfd.ShowDialog() == true)
            {
                var settings = HbRelogManager.Settings.Export(sfd.FileName);
                settings.Save();
            }
        }

    }
}
