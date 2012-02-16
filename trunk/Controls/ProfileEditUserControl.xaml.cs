using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using HighVoltz.HBRelog;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using HighVoltz.HBRelog.Tasks;
using HighVoltz.HBRelog.Settings;


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
                TaskList.Items.Add(item);
            }
            //Add 'Delete' context menu to ProfileTaskList
            ProfileTaskList.ContextMenuOpening += (sender, e) => { if (ProfileTaskList.SelectedItem == null) e.Handled = true; };
            var contextMenu = new System.Windows.Controls.ContextMenu();
            var menuItem = new MenuItem() { Header = "Delete" };
            menuItem.Click += new RoutedEventHandler((sender, e) => 
            {
                CharacterProfile profile = (CharacterProfile)MainWindow.Instance.AccountGrid.SelectedItem;
                profile.Tasks.Remove((BMTask)ProfileTaskList.SelectedItem);
            });
            contextMenu.Items.Add(menuItem);
            ProfileTaskList.ContextMenu = contextMenu;
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
            IsEditing = true;
        }

        private void PasswordTextPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance.AccountGrid.SelectedItem != null)
                ((CharacterProfile)MainWindow.Instance.AccountGrid.SelectedItem).Settings.WowSettings.Password = PasswordText.Password;
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
                if (profile.TaskManager.WowManager.WowHook != null)
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
                if (profile.TaskManager.WowManager.WowHook != null)
                {
                    var Rect = Utility.GetWindowRect(profile.TaskManager.WowManager.GameProcess.MainWindowHandle);
                    int centerX = (Rect.Left + Rect.Right) / 2;
                    int centerY = (Rect.Top + Rect.Bottom) / 2;
                    foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                    {
                        // Find the monitor that WoW is on.
                        if (centerX >= screen.Bounds.Left && centerX <= screen.Bounds.Right && centerY >= screen.Bounds.Top && centerY <= screen.Bounds.Bottom)
                        {
                            try
                            {
                                int denominator = int.Parse(WowWindowRatioText.Text);
                                profile.Settings.WowSettings.WowWindowWidth = screen.Bounds.Width / denominator;
                                profile.Settings.WowSettings.WowWindowHeight = screen.Bounds.Height / denominator;
                                Utility.ResizeAndMoveWindow(profile.TaskManager.WowManager.GameProcess.MainWindowHandle, profile.Settings.WowSettings.WowWindowX,
                                    profile.Settings.WowSettings.WowWindowY, profile.Settings.WowSettings.WowWindowWidth, profile.Settings.WowSettings.WowWindowHeight);
                            }
                            catch { }
                            break;
                        }
                    }
                }
            }
        }

        private void ProfileTaskList_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && ProfileTaskList.SelectedItem != null)
            {
                DragDrop.DoDragDrop(TaskList, ProfileTaskList.SelectedItem, DragDropEffects.Move);
                //ProfileTaskList.CaptureMouse();
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