using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
//using WindowsAuthenticator;
using HighVoltz.HBRelog;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using HighVoltz.HBRelog.Tasks;
using HighVoltz.HBRelog.Settings;
using System.Windows.Input;
using HighVoltz.HBRelog.Converters;
using System.Collections;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;


namespace HighVoltz.HBRelog.Controls
{
    /// <summary>
    /// Interaction logic for AccountConfigUserControl.xaml
    /// </summary>
    public partial class AccountConfigUserControl
    {
        public bool IsEditing { get; set; }
        public AccountConfigUserControl()
        {
            InitializeComponent();
            IsEditing = false;
            // Get list of tasks via reflection
            List<Type> taskTypes = Assembly.GetExecutingAssembly().GetTypes().Where(t => typeof(BMTask).IsAssignableFrom(t) && t != typeof(BMTask)).ToList();
            foreach (var type in taskTypes)
            {
                ListBoxItem item = new ListBoxItem();
                BMTask task = (BMTask)Activator.CreateInstance(type);
                item.Content = task.Name;
                item.Tag = type;
                item.ToolTip = task.Help;
                TaskList.Items.Add(item);
            }

            ProfileTaskList.ContextMenuOpening += (sender, e) => { if (ProfileTaskList.SelectedItem == null) e.Handled = true; };
            RegionCombo.ItemsSource = Enum.GetValues(typeof(WowSettings.WowRegion));
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CharacterProfile profile = (CharacterProfile)MainWindow.Instance.AccountGrid.SelectedItem;
            profile.Tasks.Remove((BMTask)ProfileTaskList.SelectedItem);
        }

        void ProfileTaskList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void EditAccount(ProfileSettings settings)
        {
            PasswordText.Password = settings.WowSettings.Password;
            WoWFileInput.FileName = settings.WowSettings.WowPath;

            HBProfileInput.FileName = settings.HonorbuddySettings.HonorbuddyProfile;
            HBPathInput.FileName = settings.HonorbuddySettings.HonorbuddyPath;
            RegionCombo.SelectedItem = settings.WowSettings.Region;
            IsEditing = true;
        }

        private void PasswordTextPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance.AccountGrid.SelectedItem != null)
                ((CharacterProfile)MainWindow.Instance.AccountGrid.SelectedItem).Settings.WowSettings.Password = PasswordText.Password;
        }

        private void AuthenticatorCodeClicked(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance.AccountGrid.SelectedItem != null)
            {
				//RestoreForm restore = new RestoreForm();
				//if (restore.ShowDialog() != DialogResult.OK)
				//{
				//	return;
				//}

				//StringBuilder sb = new StringBuilder();
				//XmlWriter xw = XmlWriter.Create(sb);
				//restore.Authenticator.WriteToWriter(xw);
                // Build Xml with xw.
				//xw.Flush();
				//((CharacterProfile)MainWindow.Instance.AccountGrid.SelectedItem).Settings.WowSettings
				//	.Authenticator = Convert.ToBase64String(Encoding.Unicode.GetBytes(sb.ToString()));


            }
        }

        private void WoWFileInputFileNameChanged(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance.AccountGrid.SelectedItem != null)
            {
                ((CharacterProfile)MainWindow.Instance.AccountGrid.SelectedItem).Settings.WowSettings.WowPath =
                    WoWFileInput.FileName;
            }
        }

        private void HBPathInputFileNameChanged(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance.AccountGrid.SelectedItem != null)
            {
                ((CharacterProfile)MainWindow.Instance.AccountGrid.SelectedItem).Settings.HonorbuddySettings.HonorbuddyPath =
                    HBPathInput.FileName;
            }
        }

        private void HBProfileFileInputFileNameChanged(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance.AccountGrid.SelectedItem != null)
            {
                ((CharacterProfile)MainWindow.Instance.AccountGrid.SelectedItem).Settings.HonorbuddySettings.HonorbuddyProfile =
                    HBProfileInput.FileName;
            }
        }

        private void WowWindowGrabTextClick(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance.AccountGrid.SelectedItem != null)
            {
                CharacterProfile profile = (CharacterProfile)MainWindow.Instance.AccountGrid.SelectedItem;
                if (profile.TaskManager.WowManager.Memory != null)
                {
                    var Rect = Utility.GetWindowRect(profile.TaskManager.WowManager.GameProcess.MainWindowHandle);
                    profile.Settings.WowSettings.WowWindowX = Rect.Left;
                    profile.Settings.WowSettings.WowWindowY = Rect.Top;
                    profile.Settings.WowSettings.WowWindowWidth = Rect.Right - Rect.Left;
                    profile.Settings.WowSettings.WowWindowHeight = Rect.Bottom - Rect.Top;
                }
            }
        }
        private void TaskList_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && TaskList.SelectedItem != null)
            {
                ListBoxItem lbItem = (ListBoxItem)TaskList.SelectedItem;
                DragDrop.DoDragDrop(TaskList, lbItem.Tag, DragDropEffects.Move);
            }
        }

        private void ProfileTaskList_Drop(object sender, DragEventArgs e)
        {
            if (MainWindow.Instance.AccountGrid.SelectedItem != null)
            {
                CharacterProfile profile = (CharacterProfile)MainWindow.Instance.AccountGrid.SelectedItem;
                DataObject dObj = (DataObject)e.Data;
                BMTask task = null;
                bool removeSource = false;
                // drop originated from the left side 'TaskList'
                if (dObj.GetDataPresent("PersistentObject"))
                {
                    Type taskType = (Type)dObj.GetData("PersistentObject");
                    task = (BMTask)Activator.CreateInstance(taskType);
                    task.SetProfile(profile);
                }
                else if (sender == ProfileTaskList)// drop originated from itself.
                {
                    task = (BMTask)dObj.GetData(dObj.GetFormats().FirstOrDefault());
                    removeSource = true;
                }
                if (task == null)
                    return;
                ListBoxItem targetItem = FindParent<ListBoxItem>((DependencyObject)e.OriginalSource);

                if (targetItem != null)
                {
                    BMTask targetTask = (BMTask)targetItem.Content;
                    for (int i = ProfileTaskList.Items.Count - 1; i >= 0; i--)
                    {
                        if (ProfileTaskList.Items[i].Equals(targetTask))
                        {
                            if (removeSource)
                            {
                                profile.Tasks.Remove(task);
                            }
                            profile.Tasks.Insert(i, task);
                            break;
                        }
                    }
                }
                else if (!removeSource)
                    profile.Tasks.Add(task);
                _isDragging = false;
                e.Handled = true;
            }
        }


        private void ProfileTaskList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete && ProfileTaskList.SelectedItem != null)
            {
                CharacterProfile profile = (CharacterProfile)MainWindow.Instance.AccountGrid.SelectedItem;
                profile.Tasks.Remove((BMTask)ProfileTaskList.SelectedItem);
                e.Handled = true;
            }
        }

        private void ProfileTaskListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileTaskList.SelectedItem != null)
            {
                TaskPropEditor.Source = ProfileTaskList.SelectedItem;
                e.Handled = true;
            }
        }

        private void WowWindowRatioButtonClick(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance.AccountGrid.SelectedItem != null)
            {
                CharacterProfile profile = (CharacterProfile)MainWindow.Instance.AccountGrid.SelectedItem;
                if (profile.TaskManager.WowManager.Memory != null)
                {
                    var screen = Screen.FromHandle(profile.TaskManager.WowManager.GameProcess.MainWindowHandle);
                    try
                    {
                        int denominator = int.Parse(WowWindowRatioText.Text);
                        profile.Settings.WowSettings.WowWindowWidth = screen.Bounds.Width / denominator;
                        profile.Settings.WowSettings.WowWindowHeight = screen.Bounds.Height / denominator;
                        Utility.ResizeAndMoveWindow(profile.TaskManager.WowManager.GameProcess.MainWindowHandle, profile.Settings.WowSettings.WowWindowX,
                            profile.Settings.WowSettings.WowWindowY, profile.Settings.WowSettings.WowWindowWidth, profile.Settings.WowSettings.WowWindowHeight);
                    }
                    catch { }

                }
            }
        }

        bool _isDragging;
        private void ProfileTaskList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point position = e.GetPosition(null);

                if (Math.Abs(position.X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (ProfileTaskList.SelectedItem != null && e.OriginalSource.GetType() == typeof(TextBlock))
                    {
                        _isDragging = true;
                        DragDrop.DoDragDrop(TaskList, ProfileTaskList.SelectedItem, DragDropEffects.Move);
                    }
                }
            }
        }

        private void RegionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainWindow.Instance.AccountGrid.SelectedItem != null)
            {
                ((CharacterProfile)MainWindow.Instance.AccountGrid.SelectedItem).Settings.WowSettings.Region =
                    (WowSettings.WowRegion)RegionCombo.SelectedItem;
            }
        }

        Point _startPoint;
        private void ProfileTaskList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            // fix a bug with selecting a task in the task list.
            ListBoxItem targetItem = FindParent<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (targetItem != null)
            {
                ProfileTaskList.SelectedItem = targetItem.Content;
                e.Handled = true;
            }
        }

        public static T FindParent<T>(DependencyObject from) where T : Visual
        {
            T result = null;
            DependencyObject parent = VisualTreeHelper.GetParent(from);

            if (parent is T)
                result = parent as T;
            else if (parent != null)
                result = FindParent<T>(parent);

            return result;
        }

        public ProfileSettings CharacterSetting
        {
            get { return (ProfileSettings)GetValue(CharacterSettingProperty); }
            set { SetValue(CharacterSettingProperty, value); }
        }
        public static readonly DependencyProperty CharacterSettingProperty =
            DependencyProperty.Register("CharacterSettings", typeof(ProfileSettings),
                                        typeof(AccountConfigUserControl));
    }
}