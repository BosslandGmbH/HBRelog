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

    }
}
