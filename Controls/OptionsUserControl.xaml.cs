using System;
using System.Collections.Generic;
using System.ComponentModel;
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
			if (!DesignerProperties.GetIsInDesignMode(this))
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
                GlobalSettings.Instance.Import(ofd.FileName);
                // re-assign the data context for main window.
                MainWindow.Instance.DataContext = HbRelogManager.Settings.CharacterProfiles;

                var options = (OptionsUserControl)MainWindow.Instance.FindName("HbrelogOptions");
                options.DataContext = HbRelogManager.Settings;

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
                settings.Save(sfd.FileName);
            }
        }
    }
}
